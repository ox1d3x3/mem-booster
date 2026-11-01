using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;

namespace MemBooster.Services;

/// <summary>
/// Stops and restores a curated allowlist of non-essential Windows services for a
/// gaming session.
///
/// Safety model ("nothing breaks"):
///   * Allowlist only — a service that is not in <see cref="Catalog"/> can never be
///     stopped, even if its name is passed in directly.
///   * Stop only, never reconfigure — start types are never changed, so a reboot
///     always returns every service to normal on its own.
///   * Never cascade — a service with any running dependent is skipped instead of
///     letting the SCM stop the dependents too.
///   * Every service Mem-Booster stops is recorded on disk, so Restore Services works
///     even after the app is closed and reopened.
/// </summary>
public sealed class ServiceControlService
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlyList<ServiceCandidate> Catalog = new List<ServiceCandidate>
    {
        // Recommended: background telemetry, updaters and features a gaming session never needs.
        new("DiagTrack", "Connected User Experiences and Telemetry", "Windows telemetry upload.", ServiceTier.Recommended),
        new("dmwappushservice", "Device Management WAP Push", "Routes device-management and telemetry messages.", ServiceTier.Recommended),
        new("MapsBroker", "Downloaded Maps Manager", "Background offline-map updates.", ServiceTier.Recommended),
        new("Fax", "Fax", "Fax sending and receiving.", ServiceTier.Recommended),
        new("RetailDemo", "Retail Demo", "Store demo mode.", ServiceTier.Recommended),
        new("WMPNetworkSvc", "Windows Media Player Network Sharing", "Shares media libraries over the network.", ServiceTier.Recommended),
        new("TrkWks", "Distributed Link Tracking Client", "Tracks moved NTFS shortcut targets.", ServiceTier.Recommended),
        new("PcaSvc", "Program Compatibility Assistant", "Watches app launches for compatibility issues.", ServiceTier.Recommended),
        new("WerSvc", "Windows Error Reporting", "Collects and uploads crash reports.", ServiceTier.Recommended),
        new("PhoneSvc", "Phone Service", "Telephony state for Phone Link.", ServiceTier.Recommended),
        new("lfsvc", "Geolocation", "Location tracking for apps.", ServiceTier.Recommended),
        new("edgeupdate", "Microsoft Edge Update", "Edge background updater.", ServiceTier.Recommended),
        new("edgeupdatem", "Microsoft Edge Update (on-demand)", "Edge background updater.", ServiceTier.Recommended),
        new("gupdate", "Google Update", "Chrome background updater.", ServiceTier.Recommended),
        new("gupdatem", "Google Update (on-demand)", "Chrome background updater.", ServiceTier.Recommended),
        new("brave", "Brave Update", "Brave background updater.", ServiceTier.Recommended),
        new("bravem", "Brave Update (on-demand)", "Brave background updater.", ServiceTier.Recommended),
        new("AdobeARMservice", "Adobe Acrobat Update", "Adobe background updater.", ServiceTier.Recommended),

        // Optional: useful outside a gaming session; unticked by default.
        new("SysMain", "SysMain (Superfetch)", "Preloads apps into RAM. Stopping can cut disk activity but may slow app launches.", ServiceTier.Optional),
        new("Spooler", "Print Spooler", "Printing is unavailable until the service is restored.", ServiceTier.Optional),
        new("BITS", "Background Intelligent Transfer", "Pauses background downloads. Windows may restart it on demand.", ServiceTier.Optional),
        new("wuauserv", "Windows Update", "Pauses update activity. Windows may restart it on demand.", ServiceTier.Optional),
        new("DoSvc", "Delivery Optimization", "Peer-to-peer update sharing. Windows often protects this service, so stopping can be refused.", ServiceTier.Optional),
    };

    private static readonly Dictionary<string, ServiceCandidate> CatalogByName =
        Catalog.ToDictionary(c => c.ServiceName, StringComparer.OrdinalIgnoreCase);

    private readonly string _statePath;
    private readonly object _stateSync = new();

    public ServiceControlService(string appDataDirectory)
    {
        _statePath = Path.Combine(appDataDirectory, "stopped-services.json");
    }

    public bool HasStoppedServices()
    {
        lock (_stateSync)
        {
            return LoadState().Count > 0;
        }
    }

    /// <summary>
    /// Drops recorded services that Windows has already brought back (usually after a
    /// restart), or that no longer exist. Returns how many were dropped. Services still
    /// stopped are kept, so Restore Services only lights up when it has work to do.
    /// </summary>
    public int PruneRestartedServices(Action<string>? trace = null)
    {
        lock (_stateSync)
        {
            var recorded = LoadState();
            if (recorded.Count == 0)
            {
                return 0;
            }

            var stillStopped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in recorded)
            {
                try
                {
                    using var service = new ServiceController(name);
                    service.Refresh();
                    if (service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                    {
                        continue; // Back already.
                    }

                    stillStopped.Add(name);
                }
                catch (InvalidOperationException ex) when (IsServiceMissing(ex))
                {
                    // Uninstalled: nothing to restore.
                }
                catch
                {
                    stillStopped.Add(name); // Unsure: keep it so the user can still restore.
                }
            }

            var dropped = recorded.Count - stillStopped.Count;
            if (dropped > 0)
            {
                SaveState(stillStopped);
                trace?.Invoke($"Pruned {dropped} recorded service(s) already running again; {stillStopped.Count} still stopped.");
            }

            return dropped;
        }
    }

    /// <summary>
    /// Allowlisted services that are running right now and can be stopped safely.
    /// One SCM enumeration — fast, no per-service process spawns.
    /// </summary>
    public IReadOnlyList<RunningServiceInfo> GetStoppableRunningServices(Action<string>? trace = null)
    {
        var result = new List<RunningServiceInfo>();
        ServiceController[] all;
        try
        {
            all = ServiceController.GetServices();
        }
        catch (Exception ex)
        {
            trace?.Invoke($"Service enumeration failed: {ex.Message}");
            return result;
        }

        foreach (var service in all)
        {
            try
            {
                if (!CatalogByName.TryGetValue(service.ServiceName, out var candidate))
                {
                    continue;
                }

                if (service.Status != ServiceControllerStatus.Running || !service.CanStop)
                {
                    continue;
                }

                if (HasActiveDependents(service, out var blocker))
                {
                    trace?.Invoke($"Service not offered (running dependent {blocker}): {service.ServiceName}");
                    continue;
                }

                result.Add(new RunningServiceInfo(candidate, service.DisplayName));
            }
            catch (Exception ex)
            {
                trace?.Invoke($"Service inspect failed: {service.ServiceName}; {ex.Message}");
            }
            finally
            {
                service.Dispose();
            }
        }

        return result
            .OrderBy(s => s.Candidate.Tier)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ServiceStopResult StopServices(IReadOnlyCollection<string> serviceNames, Action<string>? trace = null)
    {
        // Defence in depth: re-check every name against the allowlist, whatever the caller sent.
        var targets = serviceNames
            .Where(n => CatalogByName.ContainsKey(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var refused = serviceNames.Count - targets.Count;
        return StopCore(
            targets,
            refused,
            refused > 0 ? $"Refused {refused} service name(s) not on the allowlist." : null,
            trace);
    }

    /// <summary>
    /// Finds running Windows services whose own executable is one of the selected apps
    /// (e.g. tailscaled.exe, pia-service.exe). Killing such a process is pointless:
    /// Windows' service recovery restarts it within seconds. Stopping the service is the
    /// only way to keep it closed.
    ///
    /// Only services that are safe to stop are returned: own-process services (never a
    /// shared svchost host), not protected by SafetyRules, accepting stop requests, and
    /// with no running dependents.
    /// </summary>
    public IReadOnlyList<AppServiceTarget> FindServicesForApps(IEnumerable<string> exeNames, Action<string>? trace = null)
    {
        var wanted = new HashSet<string>(
            exeNames.Select(SafetyRules.NormaliseProcessName).Where(n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.OrdinalIgnoreCase);
        var result = new List<AppServiceTarget>();
        if (wanted.Count == 0)
        {
            return result;
        }

        ServiceController[] all;
        try
        {
            all = ServiceController.GetServices();
        }
        catch (Exception ex)
        {
            trace?.Invoke($"App service lookup failed: {ex.Message}");
            return result;
        }

        foreach (var service in all)
        {
            try
            {
                if (service.Status != ServiceControllerStatus.Running || !service.CanStop)
                {
                    continue;
                }

                if (!IsOwnProcessService(service))
                {
                    continue;
                }

                var exe = GetServiceExecutableName(service.ServiceName);
                if (exe is null || !wanted.Contains(exe) || SafetyRules.IsBlocked(exe))
                {
                    continue;
                }

                if (HasActiveDependents(service, out var blocker))
                {
                    trace?.Invoke($"App service not stopped (running dependent {blocker}): {service.ServiceName} [{exe}]");
                    continue;
                }

                result.Add(new AppServiceTarget(service.ServiceName, service.DisplayName, exe));
            }
            catch (Exception ex)
            {
                trace?.Invoke($"App service inspect failed: {service.ServiceName}; {ex.Message}");
            }
            finally
            {
                service.Dispose();
            }
        }

        return result;
    }

    /// <summary>
    /// Stops services found by <see cref="FindServicesForApps"/>. Each one is re-verified
    /// at stop time (still own-process, still the same executable, still not protected),
    /// then recorded so Restore Services brings it back.
    /// </summary>
    public ServiceStopResult StopAppServices(IReadOnlyCollection<AppServiceTarget> targets, Action<string>? trace = null)
    {
        var verified = new List<string>();
        var refused = 0;
        foreach (var target in targets)
        {
            try
            {
                using var service = new ServiceController(target.ServiceName);
                var exe = GetServiceExecutableName(target.ServiceName);
                if (IsOwnProcessService(service)
                    && exe is not null
                    && string.Equals(exe, target.ExeName, StringComparison.OrdinalIgnoreCase)
                    && !SafetyRules.IsBlocked(exe))
                {
                    verified.Add(target.ServiceName);
                    continue;
                }
            }
            catch
            {
                // Missing or unreadable: refuse.
            }

            refused++;
        }

        return StopCore(
            verified.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            refused,
            refused > 0 ? $"Refused {refused} app service(s) that changed since they were checked." : null,
            trace);
    }

    private ServiceStopResult StopCore(IReadOnlyList<string> targets, int refused, string? refusedMessage, Action<string>? trace)
    {
        var watch = Stopwatch.StartNew();
        var stopped = new ConcurrentBag<string>();
        var messages = new ConcurrentQueue<string>();
        var alreadyStopped = 0;
        var skipped = refused;
        var failed = 0;
        if (refusedMessage is not null)
        {
            messages.Enqueue(refusedMessage);
        }

        // Each stop is an independent SCM call that mostly waits, so run them side by side.
        Parallel.ForEach(targets, new ParallelOptions { MaxDegreeOfParallelism = 8 }, name =>
        {
            try
            {
                using var service = new ServiceController(name);
                service.Refresh();

                if (service.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                {
                    Interlocked.Increment(ref alreadyStopped);
                    return;
                }

                if (!service.CanStop)
                {
                    Interlocked.Increment(ref skipped);
                    messages.Enqueue($"Skipped {name}: service does not accept stop requests.");
                    return;
                }

                // Re-checked at stop time: dependents may have started since the dialog opened.
                if (HasActiveDependents(service, out var blocker))
                {
                    Interlocked.Increment(ref skipped);
                    messages.Enqueue($"Skipped {name}: dependent service {blocker} is running.");
                    return;
                }

                // Ask Windows directly. Unlike ServiceController.Stop(), this never stops
                // dependent services: if one started since the check above, Windows refuses
                // with ERROR_DEPENDENT_SERVICES_RUNNING and we skip instead of cascading.
                var stopError = SendStopControl(name);
                if (stopError == ErrorServiceNotActive)
                {
                    Interlocked.Increment(ref alreadyStopped);
                    return;
                }

                if (stopError == ErrorDependentServicesRunning)
                {
                    Interlocked.Increment(ref skipped);
                    messages.Enqueue($"Skipped {name}: a dependent service started.");
                    return;
                }

                if (stopError != 0)
                {
                    throw new System.ComponentModel.Win32Exception(stopError);
                }

                try
                {
                    service.WaitForStatus(ServiceControllerStatus.Stopped, StopTimeout);
                    stopped.Add(name);
                    trace?.Invoke($"Service stopped: {name}");
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    service.Refresh();
                    if (service.Status == ServiceControllerStatus.StopPending)
                    {
                        // Still winding down; it will stop, so it must be restorable.
                        stopped.Add(name);
                        messages.Enqueue($"{name} is still stopping (slow shutdown).");
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        messages.Enqueue($"Timed out stopping {name}.");
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                // Missing service, access denied (e.g. protected DoSvc) or already transitioning.
                Interlocked.Increment(ref failed);
                messages.Enqueue($"Could not stop {name}: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                messages.Enqueue($"Could not stop {name}: {ex.Message}");
            }
        });

        var stoppedList = stopped.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (stoppedList.Count > 0)
        {
            lock (_stateSync)
            {
                var state = LoadState();
                state.UnionWith(stoppedList);
                SaveState(state);
            }
        }

        watch.Stop();
        trace?.Invoke($"Service stop complete stopped={stoppedList.Count}; alreadyStopped={alreadyStopped}; skipped={skipped}; failed={failed}; elapsedMs={watch.ElapsedMilliseconds}");
        return new ServiceStopResult(stoppedList.Count, alreadyStopped, skipped, failed, stoppedList, messages.ToList());
    }

    public ServiceRestoreResult RestoreServices(Action<string>? trace = null)
    {
        var watch = Stopwatch.StartNew();
        HashSet<string> recorded;
        lock (_stateSync)
        {
            recorded = LoadState();
        }

        var started = 0;
        var skipped = 0;
        var failed = 0;
        var messages = new ConcurrentQueue<string>();
        var stillPending = new ConcurrentBag<string>();

        Parallel.ForEach(recorded, new ParallelOptions { MaxDegreeOfParallelism = 8 }, name =>
        {
            try
            {
                using var service = new ServiceController(name);
                service.Refresh();

                if (service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                {
                    Interlocked.Increment(ref skipped); // Windows already brought it back.
                    return;
                }

                if (service.StartType == ServiceStartMode.Disabled)
                {
                    // Something else disabled it after we stopped it; respect that.
                    Interlocked.Increment(ref skipped);
                    messages.Enqueue($"Left {name} stopped: it is now disabled.");
                    return;
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, StartTimeout);
                Interlocked.Increment(ref started);
                trace?.Invoke($"Service restored: {name}");
            }
            catch (InvalidOperationException ex) when (IsServiceMissing(ex))
            {
                // Uninstalled since it was stopped; nothing to restore.
                Interlocked.Increment(ref skipped);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                stillPending.Add(name); // Keep it so the user can retry.
                messages.Enqueue($"Could not restore {name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        });

        lock (_stateSync)
        {
            SaveState(new HashSet<string>(stillPending, StringComparer.OrdinalIgnoreCase));
        }

        watch.Stop();
        trace?.Invoke($"Service restore complete started={started}; skipped={skipped}; failed={failed}; elapsedMs={watch.ElapsedMilliseconds}");
        return new ServiceRestoreResult(started, skipped, failed, messages.ToList());
    }

    private static bool HasActiveDependents(ServiceController service, out string blocker)
    {
        blocker = string.Empty;
        ServiceController[] dependents;
        try
        {
            dependents = service.DependentServices;
        }
        catch
        {
            blocker = "(unreadable)";
            return true; // Can't prove it's safe, so treat as unsafe.
        }

        // The array is cached and owned by `service` — do NOT dispose its entries.
        // (v0.6.17-0.6.19 disposed them, which broke any later call on `service` that
        // walks DependentServices, e.g. stopping Windows Search.)
        foreach (var dependent in dependents)
        {
            try
            {
                dependent.Refresh();
                if (dependent.Status != ServiceControllerStatus.Stopped)
                {
                    blocker = dependent.ServiceName;
                    return true;
                }
            }
            catch
            {
                blocker = $"{SafeName(dependent)} (unreadable)";
                return true; // Can't prove it's stopped, so treat as unsafe.
            }
        }

        return false;
    }

    private const int ErrorDependentServicesRunning = 1051;
    private const int ErrorServiceNotActive = 1062;

    // Returns 0 on success, otherwise the Win32 error code.
    private static int SendStopControl(string serviceName)
    {
        const uint ScManagerConnect = 0x0001;
        const uint ServiceStop = 0x0020;
        const uint ServiceQueryStatus = 0x0004;
        const uint ServiceControlStop = 0x00000001;

        var scm = OpenSCManagerW(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero)
        {
            return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        }

        try
        {
            var handle = OpenServiceW(scm, serviceName, ServiceStop | ServiceQueryStatus);
            if (handle == IntPtr.Zero)
            {
                return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            }

            try
            {
                return ControlService(handle, ServiceControlStop, out _)
                    ? 0
                    : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            }
            finally
            {
                CloseServiceHandle(handle);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    private static string SafeName(ServiceController controller)
    {
        try
        {
            return controller.ServiceName;
        }
        catch
        {
            return "dependent service";
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public int dwServiceType;
        public int dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    }

    [System.Runtime.InteropServices.DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr OpenServiceW(IntPtr scManager, string serviceName, uint desiredAccess);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ControlService(IntPtr service, uint control, out SERVICE_STATUS status);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    private static bool IsOwnProcessService(ServiceController service)
    {
        var type = service.ServiceType;
        return (type & ServiceType.Win32OwnProcess) != 0 && (type & ServiceType.Win32ShareProcess) == 0;
    }

    // Executable file name from the service's ImagePath, e.g.
    //   "C:\Program Files\Tailscale\tailscaled.exe"            -> tailscaled.exe
    //   C:\Program Files\PIA\pia-service.exe --flag             -> pia-service.exe
    //   %SystemRoot%\System32\svchost.exe -k netsvcs             -> svchost.exe
    private static string? GetServiceExecutableName(string serviceName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("ImagePath") is not string raw || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var value = Environment.ExpandEnvironmentVariables(raw.Trim());
            string path;
            if (value.StartsWith('"'))
            {
                var end = value.IndexOf('"', 1);
                path = end > 1 ? value[1..end] : value.Trim('"');
            }
            else
            {
                var exeAt = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                path = exeAt >= 0 ? value[..(exeAt + 4)] : value.Split(' ')[0];
            }

            var file = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(file) ? null : SafetyRules.NormaliseProcessName(file);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPlausibleServiceName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.Length <= 256
            && name.IndexOfAny(new[] { '\\', '/' }) < 0;
    }

    private static bool IsServiceMissing(InvalidOperationException ex)
    {
        // ERROR_SERVICE_DOES_NOT_EXIST = 1060
        return ex.InnerException is System.ComponentModel.Win32Exception { NativeErrorCode: 1060 };
    }

    private HashSet<string> LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var names = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_statePath)) ?? new List<string>();
            // Holds both catalog services and app services stopped during a boost. Restore
            // only ever *starts* names Mem-Booster itself stopped, so no allowlist filter here.
            return new HashSet<string>(names.Where(IsPlausibleServiceName), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveState(HashSet<string> names)
    {
        try
        {
            if (names.Count == 0)
            {
                if (File.Exists(_statePath))
                {
                    File.Delete(_statePath);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var json = JsonSerializer.Serialize(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
            File.WriteAllText(_statePath, json);
        }
        catch
        {
            // State is best-effort; a failed write must not break the stop/restore itself.
        }
    }
}

public enum ServiceTier
{
    Recommended = 0,
    Optional = 1
}

public sealed record ServiceCandidate(string ServiceName, string FriendlyName, string Description, ServiceTier Tier)
{
    public bool DefaultSelected => Tier == ServiceTier.Recommended;
}

public sealed record RunningServiceInfo(ServiceCandidate Candidate, string DisplayName);

public sealed record AppServiceTarget(string ServiceName, string DisplayName, string ExeName);

public sealed record ServiceStopResult(int Stopped, int AlreadyStopped, int Skipped, int Failed, IReadOnlyList<string> StoppedServiceNames, IReadOnlyList<string> Messages)
{
    public string Summary => $"Services stopped: {Stopped}, already stopped: {AlreadyStopped}, skipped: {Skipped}, failed: {Failed}";
}

public sealed record ServiceRestoreResult(int Started, int Skipped, int Failed, IReadOnlyList<string> Messages)
{
    public string Summary => $"Services restored: {Started}, skipped: {Skipped}, failed: {Failed}";
}
