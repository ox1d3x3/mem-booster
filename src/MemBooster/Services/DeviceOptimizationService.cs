using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace MemBooster.Services;

public sealed class DeviceOptimizationService
{
    private const string StateVersion = "0.6.14";
    private static readonly Guid UltimatePerformanceTemplateGuid = Guid.Parse("e9a42b02-d5df-448d-aa00-03f14749eb61");
    private readonly string _statePath;

    public const string UltimatePowerPlanOptionId = "ultimate-performance-power-plan";
    public const string GameModeOptionId = "game-mode";
    public const string GameCaptureOptionId = "disable-game-capture";
    public const string VisualEffectsOptionId = "reduce-visual-effects";
    public const string WidgetsOptionId = "hide-widgets";
    public const string SearchIndexingOptionId = "pause-search-indexing";
    public const string MousePrecisionOptionId = "disable-pointer-precision";
    public const string MultimediaSchedulerOptionId = "gaming-multimedia-scheduler";
    public const string HardwareGpuSchedulingOptionId = "hardware-gpu-scheduling";

    private static readonly DeviceOptimizationOption[] AvailableOptions =
    {
        new(
            UltimatePowerPlanOptionId,
            "Ultimate Performance power plan",
            "Power",
            "Creates a Mem-Booster Ultimate Performance plan and activates it for the gaming session.",
            true,
            false,
            false,
            "May increase power draw and heat while active."),
        new(
            GameModeOptionId,
            "Windows Game Mode",
            "Gaming",
            "Enables Windows Game Mode and automatic Game Mode handling.",
            true,
            false,
            false,
            null),
        new(
            GameCaptureOptionId,
            "Disable Xbox/Game DVR capture",
            "Capture",
            "Disables Windows app capture/background recording settings that can consume resources while gaming.",
            true,
            false,
            false,
            "Do not select this if you rely on Xbox/Game Bar recording."),
        new(
            VisualEffectsOptionId,
            "Reduce visual effects",
            "Desktop",
            "Disables transparency and minimise/maximise animation to reduce desktop composition overhead.",
            true,
            false,
            false,
            "Some visual changes may need sign-out or restart to fully refresh."),
        new(
            WidgetsOptionId,
            "Hide Widgets taskbar button",
            "Desktop",
            "Hides the Windows Widgets taskbar button for a cleaner low-distraction gaming session.",
            true,
            false,
            false,
            "Taskbar changes may need Explorer refresh, sign-out or restart to visually update."),
        new(
            SearchIndexingOptionId,
            "Pause Windows Search indexing",
            "Background services",
            "Stops Windows Search indexing only if it is currently running, then restores it on revert.",
            true,
            false,
            false,
            "Search results may be less fresh while this is paused."),
        new(
            MousePrecisionOptionId,
            "Disable enhanced pointer precision",
            "Input",
            "Turns off Windows mouse acceleration for more consistent competitive aiming.",
            false,
            true,
            false,
            "Changes mouse feel. May need sign-out/restart to fully apply across all apps."),
        new(
            MultimediaSchedulerOptionId,
            "Gaming multimedia scheduler profile",
            "Advanced",
            "Applies reversible Windows multimedia scheduler values commonly used by the built-in Games profile.",
            false,
            true,
            false,
            "Advanced tweak. Reversible, but test with your own games and audio setup."),
        new(
            HardwareGpuSchedulingOptionId,
            "Hardware-accelerated GPU scheduling",
            "Advanced",
            "Enables Windows hardware-accelerated GPU scheduling when supported by the driver/GPU.",
            false,
            true,
            true,
            "Requires a Windows restart. It can help or hurt depending on GPU/driver/game, so leave unchecked unless you want to test it.")
    };

    private static readonly RegistryOptimisation[] RegistryOptimisations =
    {
        new(GameModeOptionId, RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", RegistryValueKind.DWord, 1, "Enable Windows Game Mode"),
        new(GameModeOptionId, RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", RegistryValueKind.DWord, 1, "Allow automatic Game Mode"),

        new(GameCaptureOptionId, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValueKind.DWord, 0, "Disable Xbox/Game DVR app capture"),
        new(GameCaptureOptionId, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", RegistryValueKind.DWord, 0, "Disable Xbox/Game DVR background capture history"),
        new(GameCaptureOptionId, RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", RegistryValueKind.DWord, 0, "Disable Game DVR background recording"),

        new(VisualEffectsOptionId, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", RegistryValueKind.DWord, 0, "Disable transparency effects"),
        new(VisualEffectsOptionId, RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", RegistryValueKind.String, "0", "Disable minimise/maximise window animation"),

        new(WidgetsOptionId, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", RegistryValueKind.DWord, 0, "Hide Widgets from the taskbar"),

        new(MousePrecisionOptionId, RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", RegistryValueKind.String, "0", "Disable enhanced pointer precision"),
        new(MousePrecisionOptionId, RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", RegistryValueKind.String, "0", "Disable enhanced pointer precision threshold 1"),
        new(MousePrecisionOptionId, RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", RegistryValueKind.String, "0", "Disable enhanced pointer precision threshold 2"),

        new(MultimediaSchedulerOptionId, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", RegistryValueKind.DWord, 0, "Set multimedia SystemResponsiveness for gaming"),
        new(MultimediaSchedulerOptionId, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", RegistryValueKind.DWord, unchecked((int)0xffffffff), "Disable multimedia network throttling"),
        new(MultimediaSchedulerOptionId, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", RegistryValueKind.DWord, 8, "Set Games GPU priority"),
        new(MultimediaSchedulerOptionId, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", RegistryValueKind.DWord, 6, "Set Games CPU priority"),
        new(MultimediaSchedulerOptionId, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", RegistryValueKind.String, "High", "Set Games scheduling category"),
        new(MultimediaSchedulerOptionId, RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", RegistryValueKind.String, "High", "Set Games SFIO priority"),

        new(HardwareGpuSchedulingOptionId, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", RegistryValueKind.DWord, 2, "Enable hardware-accelerated GPU scheduling")
    };

    public DeviceOptimizationService(string appDataDirectory)
    {
        Directory.CreateDirectory(appDataDirectory);
        _statePath = Path.Combine(appDataDirectory, "device-optimise-state.xml");
    }

    public string StatePath => _statePath;

    public bool HasActiveState() => File.Exists(_statePath);

    public IReadOnlyList<DeviceOptimizationOption> GetAvailableOptions() => AvailableOptions;

    public IReadOnlyList<DeviceOptimizationOption> GetAppliedOptions()
    {
        if (!File.Exists(_statePath))
        {
            return Array.Empty<DeviceOptimizationOption>();
        }

        try
        {
            var state = LoadState(_statePath);
            var appliedIds = NormaliseOptionIds(state.AppliedOptionIds);
            if (appliedIds.Count == 0)
            {
                appliedIds = InferAppliedOptionIds(state);
            }

            return AvailableOptions.Where(option => appliedIds.Contains(option.Id)).ToArray();
        }
        catch
        {
            return Array.Empty<DeviceOptimizationOption>();
        }
    }

    public bool IsWindows11OrLater(out string description)
    {
        var version = Environment.OSVersion.Version;
        description = $"{Environment.OSVersion.VersionString} (build {version.Build})";
        return OperatingSystem.IsWindows() && version.Major >= 10 && version.Build >= 22000;
    }

    public DeviceOptimizationResult Apply(IEnumerable<string> selectedOptionIds, Action<string> log, Action<string, double>? progress = null)
    {
        var requestedOptions = NormaliseOptionIds(selectedOptionIds);
        var selectedOptions = AvailableOptions.Where(option => requestedOptions.Contains(option.Id)).ToArray();
        var messages = new List<string>();
        void Log(string message)
        {
            messages.Add(message);
            log(message);
        }

        if (!IsWindows11OrLater(out var osDescription))
        {
            Log($"Device optimisation blocked: Windows 11 required. Detected {osDescription}.");
            return new DeviceOptimizationResult(false, "Device Optimise is Windows 11 only.", messages);
        }

        if (selectedOptions.Length == 0)
        {
            Log("Device optimisation cancelled: no optimisation options selected.");
            return new DeviceOptimizationResult(false, "No Device Optimise options were selected.", messages);
        }

        if (HasActiveState())
        {
            Log($"Existing device optimisation state found: {_statePath}");
            return new DeviceOptimizationResult(true, "Device Optimise is already active. Use Revert Device Optimise before applying again.", messages);
        }

        Log("Selected Device Optimise options: " + string.Join(", ", selectedOptions.Select(option => option.Id)));

        progress?.Invoke("Capturing selected Windows 11 gaming settings...", 8);
        var state = DeviceOptimizationState.CaptureEmpty();
        state.AppliedOptionIds.AddRange(selectedOptions.Select(option => option.Id));

        if (requestedOptions.Contains(UltimatePowerPlanOptionId))
        {
            state.OriginalPowerSchemeGuid = GetActivePowerSchemeGuid(Log);
        }

        var selectedRegistryOptimisations = RegistryOptimisations
            .Where(optimisation => requestedOptions.Contains(optimisation.OptionId))
            .ToArray();
        state.RegistryBackups.AddRange(selectedRegistryOptimisations.Select(CaptureRegistryBackup));

        if (requestedOptions.Contains(SearchIndexingOptionId))
        {
            state.WindowsSearchWasRunning = QueryServiceRunning("WSearch", Log);
            state.WindowsSearchExists = state.WindowsSearchWasRunning.HasValue;
        }

        SaveState(state, Log);
        Log($"Captured baseline state: options={state.AppliedOptionIds.Count}; originalPowerScheme={state.OriginalPowerSchemeGuid ?? "not-selected"}; registryValues={state.RegistryBackups.Count}; WSearchExists={state.WindowsSearchExists}; WSearchWasRunning={state.WindowsSearchWasRunning}");

        var failures = 0;

        if (requestedOptions.Contains(UltimatePowerPlanOptionId))
        {
            progress?.Invoke("Applying Ultimate Performance power plan...", 22);
            try
            {
                var createdGuid = CreateUltimatePerformancePlan(Log);
                if (!string.IsNullOrWhiteSpace(createdGuid))
                {
                    state.CreatedPowerSchemeGuid = createdGuid;
                    SaveState(state, Log);
                    var setResult = RunCommand("powercfg.exe", $"/setactive {createdGuid}", TimeSpan.FromSeconds(8), Log);
                    if (setResult.ExitCode == 0)
                    {
                        Log($"Activated Ultimate Performance power scheme: {createdGuid}");
                    }
                    else
                    {
                        failures++;
                        Log($"Failed to activate Ultimate Performance scheme. ExitCode={setResult.ExitCode}; Output={setResult.CombinedOutput}");
                    }
                }
                else
                {
                    failures++;
                    Log("Could not create Ultimate Performance power scheme.");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Log("Power plan optimisation failed: " + ex.Message);
            }
        }
        else
        {
            Log("Power plan option not selected. Skipping powercfg changes.");
        }

        progress?.Invoke("Applying selected registry-backed optimisations...", 50);
        foreach (var optimisation in selectedRegistryOptimisations)
        {
            try
            {
                SetRegistryValue(optimisation);
                Log($"Applied registry optimisation: option={optimisation.OptionId}; {optimisation.Description} | {optimisation.Hive}\\{optimisation.Path} | {optimisation.Name}={optimisation.NewValue}");
            }
            catch (Exception ex)
            {
                failures++;
                Log($"Registry optimisation failed: option={optimisation.OptionId}; {optimisation.Description} | {ex.Message}");
            }
        }

        if (requestedOptions.Contains(SearchIndexingOptionId))
        {
            progress?.Invoke("Pausing Windows Search indexing for the gaming session...", 76);
            try
            {
                if (state.WindowsSearchWasRunning == true)
                {
                    var stopResult = RunCommand("sc.exe", "stop WSearch", TimeSpan.FromSeconds(10), Log);
                    Log($"Requested Windows Search service stop. ExitCode={stopResult.ExitCode}; Output={SingleLine(stopResult.CombinedOutput)}");
                }
                else if (state.WindowsSearchWasRunning == false)
                {
                    Log("Windows Search service was not running. No stop needed.");
                }
                else
                {
                    Log("Windows Search service was not found or could not be queried. Skipping service stop.");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Log("Windows Search pause failed: " + ex.Message);
            }
        }
        else
        {
            Log("Windows Search pause option not selected. Skipping service stop.");
        }

        state.AppliedUtc = DateTime.UtcNow;
        SaveState(state, Log);
        progress?.Invoke("Device optimisation complete.", 100);

        var restartRequired = selectedOptions.Any(option => option.RequiresRestart);
        var summary = failures == 0
            ? restartRequired
                ? "Device Optimise applied. One or more selected options require a Windows restart to fully take effect."
                : "Device Optimise applied. Use Revert Device Optimise to restore captured settings."
            : $"Device Optimise finished with {failures} warning(s). Check logs before relying on it.";

        return new DeviceOptimizationResult(failures == 0, summary, messages);
    }

    public DeviceOptimizationResult Revert(Action<string> log, Action<string, double>? progress = null)
    {
        var messages = new List<string>();
        void Log(string message)
        {
            messages.Add(message);
            log(message);
        }

        if (!File.Exists(_statePath))
        {
            Log("No device optimisation state file found. Nothing to revert.");
            return new DeviceOptimizationResult(true, "No Device Optimise changes were recorded on this PC.", messages);
        }

        var failures = 0;
        progress?.Invoke("Loading captured device optimisation state...", 8);
        var state = LoadState(_statePath);
        var appliedOptions = NormaliseOptionIds(state.AppliedOptionIds);
        var legacyState = appliedOptions.Count == 0;
        if (legacyState)
        {
            appliedOptions = InferAppliedOptionIds(state);
        }
        Log($"Loaded device optimisation state createdUtc={state.CreatedUtc:O}; appliedOptions={string.Join(",", appliedOptions)}; legacyState={legacyState}; originalPowerScheme={state.OriginalPowerSchemeGuid}; createdPowerScheme={state.CreatedPowerSchemeGuid}; registryBackups={state.RegistryBackups.Count}");

        if (legacyState || appliedOptions.Contains(UltimatePowerPlanOptionId))
        {
            progress?.Invoke("Restoring previous power plan...", 24);
            try
            {
                if (!string.IsNullOrWhiteSpace(state.OriginalPowerSchemeGuid))
                {
                    var setResult = RunCommand("powercfg.exe", $"/setactive {state.OriginalPowerSchemeGuid}", TimeSpan.FromSeconds(8), Log);
                    Log($"Restored original power scheme. ExitCode={setResult.ExitCode}; Output={SingleLine(setResult.CombinedOutput)}");
                    if (setResult.ExitCode != 0)
                    {
                        failures++;
                    }
                }
                else
                {
                    Log("Original power scheme was not captured. Skipping power-plan restore.");
                }

                if (!string.IsNullOrWhiteSpace(state.CreatedPowerSchemeGuid)
                    && !string.Equals(state.CreatedPowerSchemeGuid, state.OriginalPowerSchemeGuid, StringComparison.OrdinalIgnoreCase))
                {
                    var deleteResult = RunCommand("powercfg.exe", $"/delete {state.CreatedPowerSchemeGuid}", TimeSpan.FromSeconds(8), Log);
                    Log($"Removed created Ultimate Performance scheme. ExitCode={deleteResult.ExitCode}; Output={SingleLine(deleteResult.CombinedOutput)}");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Log("Power-plan revert failed: " + ex.Message);
            }
        }
        else
        {
            Log("Power plan was not applied in this state. Skipping power-plan revert.");
        }

        progress?.Invoke("Restoring registry settings...", 50);
        foreach (var backup in state.RegistryBackups)
        {
            try
            {
                RestoreRegistryValue(backup);
                Log($"Restored registry value: {backup.Hive}\\{backup.Path} | {backup.Name} | existed={backup.Existed}");
            }
            catch (Exception ex)
            {
                failures++;
                Log($"Registry revert failed: {backup.Hive}\\{backup.Path} | {backup.Name} | {ex.Message}");
            }
        }

        if (legacyState || appliedOptions.Contains(SearchIndexingOptionId))
        {
            progress?.Invoke("Restoring Windows Search indexing state...", 76);
            try
            {
                if (state.WindowsSearchWasRunning == true)
                {
                    var startResult = RunCommand("sc.exe", "start WSearch", TimeSpan.FromSeconds(10), Log);
                    Log($"Requested Windows Search service start. ExitCode={startResult.ExitCode}; Output={SingleLine(startResult.CombinedOutput)}");
                }
                else
                {
                    Log("Windows Search was not running before optimisation, so it was not restarted.");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Log("Windows Search restore failed: " + ex.Message);
            }
        }
        else
        {
            Log("Windows Search pause was not applied in this state. Skipping service restore.");
        }

        progress?.Invoke("Cleaning optimisation state...", 92);
        if (failures == 0)
        {
            try
            {
                File.Delete(_statePath);
                Log("Device optimisation state file removed after successful revert.");
            }
            catch (Exception ex)
            {
                failures++;
                Log("Could not delete device optimisation state file: " + ex.Message);
            }
        }
        else
        {
            Log("Device optimisation state kept because one or more revert steps failed.");
        }

        progress?.Invoke("Device optimisation revert complete.", 100);
        var requiresRestart = GetOptionsById(appliedOptions).Any(option => option.RequiresRestart);
        var summary = failures == 0
            ? requiresRestart
                ? "Device Optimise reverted. Restart Windows to fully clear restart-required changes."
                : "Device Optimise reverted. Restart Windows if anything still feels unusual."
            : $"Revert finished with {failures} warning(s). State file kept so you can retry.";

        return new DeviceOptimizationResult(failures == 0, summary, messages);
    }


    private static HashSet<string> InferAppliedOptionIds(DeviceOptimizationState state)
    {
        var inferred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(state.OriginalPowerSchemeGuid) || !string.IsNullOrWhiteSpace(state.CreatedPowerSchemeGuid))
        {
            inferred.Add(UltimatePowerPlanOptionId);
        }

        if (state.WindowsSearchWasRunning.HasValue)
        {
            inferred.Add(SearchIndexingOptionId);
        }

        foreach (var backup in state.RegistryBackups)
        {
            var optionId = RegistryOptimisations.FirstOrDefault(optimisation =>
                optimisation.Hive == backup.Hive
                && string.Equals(optimisation.Path, backup.Path, StringComparison.OrdinalIgnoreCase)
                && string.Equals(optimisation.Name, backup.Name, StringComparison.OrdinalIgnoreCase))?.OptionId;

            if (!string.IsNullOrWhiteSpace(optionId))
            {
                inferred.Add(optionId);
            }
        }

        return inferred;
    }

    private static HashSet<string> NormaliseOptionIds(IEnumerable<string> optionIds)
    {
        var validIds = AvailableOptions.Select(option => option.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return optionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(validIds.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DeviceOptimizationOption> GetOptionsById(ISet<string> optionIds)
    {
        if (optionIds.Count == 0)
        {
            return Array.Empty<DeviceOptimizationOption>();
        }

        return AvailableOptions.Where(option => optionIds.Contains(option.Id)).ToArray();
    }

    private static RegistryBackup CaptureRegistryBackup(RegistryOptimisation optimisation)
    {
        using var baseKey = RegistryKey.OpenBaseKey(optimisation.Hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(optimisation.Path, writable: false);
        if (key is null || !key.GetValueNames().Contains(optimisation.Name, StringComparer.OrdinalIgnoreCase))
        {
            return new RegistryBackup(optimisation.OptionId, optimisation.Hive, optimisation.Path, optimisation.Name, RegistryValueKind.Unknown, null, false);
        }

        var value = key.GetValue(optimisation.Name);
        var kind = key.GetValueKind(optimisation.Name);
        return new RegistryBackup(optimisation.OptionId, optimisation.Hive, optimisation.Path, optimisation.Name, kind, value, true);
    }

    private static void SetRegistryValue(RegistryOptimisation optimisation)
    {
        using var baseKey = RegistryKey.OpenBaseKey(optimisation.Hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(optimisation.Path, writable: true)
            ?? throw new InvalidOperationException("Could not open or create registry key.");
        key.SetValue(optimisation.Name, optimisation.NewValue, optimisation.NewValueKind);
    }

    private static void RestoreRegistryValue(RegistryBackup backup)
    {
        using var baseKey = RegistryKey.OpenBaseKey(backup.Hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(backup.Path, writable: true)
            ?? throw new InvalidOperationException("Could not open or create registry key.");

        if (!backup.Existed)
        {
            key.DeleteValue(backup.Name, throwOnMissingValue: false);
            return;
        }

        var kind = backup.Kind == RegistryValueKind.Unknown ? RegistryValueKind.String : backup.Kind;
        var value = backup.Value ?? string.Empty;
        key.SetValue(backup.Name, value, kind);
    }

    private static string? GetActivePowerSchemeGuid(Action<string> log)
    {
        var result = RunCommand("powercfg.exe", "/getactivescheme", TimeSpan.FromSeconds(6), log);
        var match = Regex.Match(result.CombinedOutput, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
        if (!match.Success)
        {
            log("Could not detect active power scheme from powercfg output: " + SingleLine(result.CombinedOutput));
            return null;
        }

        return match.Groups[1].Value;
    }

    private static string? CreateUltimatePerformancePlan(Action<string> log)
    {
        var duplicate = RunCommand("powercfg.exe", $"/duplicatescheme {UltimatePerformanceTemplateGuid}", TimeSpan.FromSeconds(8), log);
        var match = Regex.Match(duplicate.CombinedOutput, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        log("Ultimate Performance duplicate output did not include a GUID. Output=" + SingleLine(duplicate.CombinedOutput));
        return null;
    }

    private static bool? QueryServiceRunning(string serviceName, Action<string> log)
    {
        var result = RunCommand("sc.exe", $"query {serviceName}", TimeSpan.FromSeconds(6), log);
        if (result.ExitCode != 0 || result.CombinedOutput.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
        {
            log($"Service query failed or service missing: {serviceName}; ExitCode={result.ExitCode}; Output={SingleLine(result.CombinedOutput)}");
            return null;
        }

        if (Regex.IsMatch(result.CombinedOutput, @"STATE\s*:\s*\d+\s+RUNNING", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(result.CombinedOutput, @"STATE\s*:\s*\d+\s+STOPPED", RegexOptions.IgnoreCase))
        {
            return false;
        }

        log($"Service state could not be parsed: {serviceName}; Output={SingleLine(result.CombinedOutput)}");
        return null;
    }

    private static CommandResult RunCommand(string fileName, string arguments, TimeSpan timeout, Action<string> log)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort only.
            }

            throw new TimeoutException($"Command timed out: {fileName} {arguments}");
        }

        stopwatch.Stop();
        Task.WaitAll(outputTask, errorTask);
        var output = outputTask.Result ?? string.Empty;
        var error = errorTask.Result ?? string.Empty;
        var combined = (output + Environment.NewLine + error).Trim();
        log($"Command: {fileName} {arguments}; exit={process.ExitCode}; elapsedMs={stopwatch.ElapsedMilliseconds}; output={SingleLine(combined)}");
        return new CommandResult(process.ExitCode, combined, stopwatch.Elapsed);
    }

    private void SaveState(DeviceOptimizationState state, Action<string> log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("MemBoosterDeviceOptimiseState",
                new XAttribute("version", StateVersion),
                new XAttribute("createdUtc", state.CreatedUtc.ToString("O")),
                new XAttribute("appliedUtc", state.AppliedUtc?.ToString("O") ?? string.Empty),
                new XAttribute("machine", Environment.MachineName),
                new XAttribute("user", Environment.UserName),
                new XAttribute("originalPowerSchemeGuid", state.OriginalPowerSchemeGuid ?? string.Empty),
                new XAttribute("createdPowerSchemeGuid", state.CreatedPowerSchemeGuid ?? string.Empty),
                new XAttribute("windowsSearchExists", state.WindowsSearchExists),
                new XAttribute("windowsSearchWasRunning", state.WindowsSearchWasRunning?.ToString() ?? string.Empty),
                new XElement("AppliedOptions",
                    state.AppliedOptionIds.Select(id => new XElement("Option", new XAttribute("id", id)))),
                new XElement("RegistryValues",
                    state.RegistryBackups.Select(backup => new XElement("Value",
                        new XAttribute("optionId", backup.OptionId),
                        new XAttribute("hive", backup.Hive.ToString()),
                        new XAttribute("path", backup.Path),
                        new XAttribute("name", backup.Name),
                        new XAttribute("existed", backup.Existed),
                        new XAttribute("kind", backup.Kind.ToString()),
                        new XAttribute("value", RegistryValueToString(backup.Value, backup.Kind)))))));

        document.Save(_statePath);
        log($"Device optimisation state saved: {_statePath}");
    }

    private static DeviceOptimizationState LoadState(string path)
    {
        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("Device optimisation state file is empty.");
        if (root.Name != "MemBoosterDeviceOptimiseState")
        {
            throw new InvalidDataException("This does not look like a Mem-Booster Device Optimise state file.");
        }

        static bool? ParseNullableBool(string value)
        {
            return bool.TryParse(value, out var parsed) ? parsed : null;
        }

        var state = DeviceOptimizationState.CaptureEmpty();
        state.CreatedUtc = DateTime.TryParse((string?)root.Attribute("createdUtc"), out var createdUtc) ? createdUtc : DateTime.UtcNow;
        state.AppliedUtc = DateTime.TryParse((string?)root.Attribute("appliedUtc"), out var appliedUtc) ? appliedUtc : null;
        state.OriginalPowerSchemeGuid = EmptyToNull((string?)root.Attribute("originalPowerSchemeGuid"));
        state.CreatedPowerSchemeGuid = EmptyToNull((string?)root.Attribute("createdPowerSchemeGuid"));
        state.WindowsSearchExists = bool.TryParse((string?)root.Attribute("windowsSearchExists"), out var exists) && exists;
        state.WindowsSearchWasRunning = ParseNullableBool((string?)root.Attribute("windowsSearchWasRunning") ?? string.Empty);

        var appliedIds = root.Element("AppliedOptions")?
            .Elements("Option")
            .Select(element => (string?)element.Attribute("id") ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
        state.AppliedOptionIds.AddRange(appliedIds);

        var backups = root.Element("RegistryValues")?
            .Elements("Value")
            .Select(element =>
            {
                var kind = Enum.TryParse((string?)element.Attribute("kind"), ignoreCase: true, out RegistryValueKind parsedKind)
                    ? parsedKind
                    : RegistryValueKind.String;
                return new RegistryBackup(
                    (string?)element.Attribute("optionId") ?? string.Empty,
                    Enum.TryParse((string?)element.Attribute("hive"), ignoreCase: true, out RegistryHive hive) ? hive : RegistryHive.CurrentUser,
                    (string?)element.Attribute("path") ?? string.Empty,
                    (string?)element.Attribute("name") ?? string.Empty,
                    kind,
                    ConvertStoredRegistryValue((string?)element.Attribute("value") ?? string.Empty, kind),
                    bool.TryParse((string?)element.Attribute("existed"), out var existed) && existed);
            })
            .Where(backup => !string.IsNullOrWhiteSpace(backup.Path) && !string.IsNullOrWhiteSpace(backup.Name))
            .ToArray() ?? Array.Empty<RegistryBackup>();

        state.RegistryBackups.AddRange(backups);
        return state;
    }

    private static string RegistryValueToString(object? value, RegistryValueKind kind)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (kind == RegistryValueKind.MultiString && value is string[] multi)
        {
            return string.Join('\n', multi);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }

    private static object ConvertStoredRegistryValue(string value, RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.DWord => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dword) ? dword : 0,
            RegistryValueKind.QWord => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qword) ? qword : 0L,
            RegistryValueKind.MultiString => value.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            _ => value
        };
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string SingleLine(string value)
    {
        return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
    }

    private sealed record RegistryOptimisation(string OptionId, RegistryHive Hive, string Path, string Name, RegistryValueKind NewValueKind, object NewValue, string Description);

    private sealed record RegistryBackup(string OptionId, RegistryHive Hive, string Path, string Name, RegistryValueKind Kind, object? Value, bool Existed);

    private sealed record CommandResult(int ExitCode, string CombinedOutput, TimeSpan Elapsed);

    private sealed class DeviceOptimizationState
    {
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? AppliedUtc { get; set; }
        public string? OriginalPowerSchemeGuid { get; set; }
        public string? CreatedPowerSchemeGuid { get; set; }
        public bool WindowsSearchExists { get; set; }
        public bool? WindowsSearchWasRunning { get; set; }
        public List<string> AppliedOptionIds { get; } = new();
        public List<RegistryBackup> RegistryBackups { get; } = new();
        public static DeviceOptimizationState CaptureEmpty() => new();
    }
}

public sealed record DeviceOptimizationOption(
    string Id,
    string Name,
    string Category,
    string Description,
    bool DefaultSelected,
    bool Advanced,
    bool RequiresRestart,
    string? WarningText);

public sealed record DeviceOptimizationResult(bool Success, string Summary, IReadOnlyList<string> Messages);
