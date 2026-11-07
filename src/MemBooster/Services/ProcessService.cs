using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using MemBooster.Models;

namespace MemBooster.Services;

public sealed class ProcessService
{
    private static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge.exe"] = "Microsoft Edge",
        ["msedgewebview2.exe"] = "Microsoft Edge WebView2",
        ["chrome.exe"] = "Google Chrome",
        ["brave.exe"] = "Brave Browser",
        ["firefox.exe"] = "Mozilla Firefox",
        ["opera.exe"] = "Opera",
        ["operagx.exe"] = "Opera GX",
        ["vivaldi.exe"] = "Vivaldi",
        ["arc.exe"] = "Arc Browser",
        ["zen.exe"] = "Zen Browser",

        ["winword.exe"] = "Microsoft Word",
        ["excel.exe"] = "Microsoft Excel",
        ["powerpnt.exe"] = "Microsoft PowerPoint",
        ["outlook.exe"] = "Microsoft Outlook",
        ["onenote.exe"] = "Microsoft OneNote",
        ["msaccess.exe"] = "Microsoft Access",
        ["mspub.exe"] = "Microsoft Publisher",
        ["visio.exe"] = "Microsoft Visio",
        ["winproj.exe"] = "Microsoft Project",
        ["teams.exe"] = "Microsoft Teams",
        ["ms-teams.exe"] = "Microsoft Teams",
        ["msteams.exe"] = "Microsoft Teams",
        ["onedrive.exe"] = "OneDrive",
        ["officeclicktorun.exe"] = "Microsoft Office Click-to-Run",

        ["widgetboard.exe"] = "Windows Widgets",
        ["widgetservice.exe"] = "Windows Widgets Service",
        ["microsoftstartfeedprovider.exe"] = "Microsoft Start Feed",
        ["yourphone.exe"] = "Phone Link",
        ["phoneexperiencehost.exe"] = "Phone Link",
        ["photos.exe"] = "Microsoft Photos",
        ["microsoft.photos.exe"] = "Microsoft Photos",
        ["copilot.exe"] = "Microsoft Copilot",
        ["windowscopilot.exe"] = "Windows Copilot",
        ["hxoutlook.exe"] = "Mail and Calendar",
        ["hxtsr.exe"] = "Microsoft Mail Background Task",
        ["clipchamp.exe"] = "Clipchamp",
        ["widgets.exe"] = "Windows Widgets",
        ["searchapp.exe"] = "Windows Search App",
        ["searchindexer.exe"] = "Windows Search Indexer",
        ["searchprotocolhost.exe"] = "Windows Search Protocol Host",
        ["searchfilterhost.exe"] = "Windows Search Filter Host",
        ["lockapp.exe"] = "Windows Lock Screen",
        ["cortana.exe"] = "Cortana",
        ["zunemusic.exe"] = "Media Player / Groove",
        ["zunevideo.exe"] = "Movies & TV",
        ["quickassist.exe"] = "Quick Assist",
        ["gethelp.exe"] = "Get Help",

        ["steam.exe"] = "Steam",
        ["steamwebhelper.exe"] = "Steam WebHelper",
        ["epicgameslauncher.exe"] = "Epic Games Launcher",
        ["eadesktop.exe"] = "EA App",
        ["eabackgroundservice.exe"] = "EA Background Service",
        ["ubisoftconnect.exe"] = "Ubisoft Connect",
        ["upc.exe"] = "Ubisoft Connect",
        ["uplay.exe"] = "Ubisoft Connect",
        ["uplaywebcore.exe"] = "Ubisoft WebCore",
        ["battle.net.exe"] = "Battle.net",
        ["battlenet.exe"] = "Battle.net",
        ["riotclientservices.exe"] = "Riot Client",
        ["goggalaxy.exe"] = "GOG Galaxy",
        ["rockstarlauncher.exe"] = "Rockstar Games Launcher",

        ["discord.exe"] = "Discord",
        ["slack.exe"] = "Slack",
        ["zoom.exe"] = "Zoom",
        ["webex.exe"] = "Webex",
        ["telegram.exe"] = "Telegram",
        ["whatsapp.exe"] = "WhatsApp",
        ["messenger.exe"] = "Messenger",
        ["signal.exe"] = "Signal",
        ["notion.exe"] = "Notion",
        ["obsidian.exe"] = "Obsidian",
        ["figma.exe"] = "Figma",
        ["code.exe"] = "Visual Studio Code",
        ["devenv.exe"] = "Visual Studio",
        ["msbuild.exe"] = "MSBuild",
        ["postman.exe"] = "Postman",
        ["docker desktop.exe"] = "Docker Desktop",
        ["githubdesktop.exe"] = "GitHub Desktop",
        ["notepad++.exe"] = "Notepad++",
        ["powertoys.exe"] = "PowerToys",
        ["powertoys.runner.exe"] = "PowerToys Run",
        ["sharex.exe"] = "ShareX",
        ["greenshot.exe"] = "Greenshot",


        // Windows, productivity, dev and utility helpers seen in real gaming workloads
        ["appactions.exe"] = "Windows App Actions",
        ["crossdeviceresume.exe"] = "Cross Device Resume",
        ["mspcmanager.exe"] = "Microsoft PC Manager",
        ["mspcmanagercore.exe"] = "Microsoft PC Manager Core",
        ["mspcmanagerservice.exe"] = "Microsoft PC Manager Service",
        ["filecoauth.exe"] = "Microsoft Office File Co-Authoring",
        ["filesynchelper.exe"] = "OneDrive File Sync Helper",
        ["windowspackagemanagerserver.exe"] = "Windows Package Manager Server",
        ["bravecrashhandler.exe"] = "Brave Crash Handler",
        ["bravecrashhandler64.exe"] = "Brave Crash Handler 64-bit",
        ["crashpad_handler.exe"] = "Crashpad Handler",
        ["windowscommandpalette.exe"] = "Windows Command Palette",
        ["microsoft.cmdpal.ui.exe"] = "Microsoft Command Palette",
        ["microsoft.cmdpal.ext.powertoys.exe"] = "PowerToys Command Palette Extension",
        ["everythingcmdpal3.exe"] = "Everything Command Palette",
        ["powermodecmdpal.exe"] = "Power Mode Command Palette",
        ["baldbeardedbuilder.weatherextension.exe"] = "Weather Command Palette Extension",
        ["hoobi.bitwardencommandpaletteextension.exe"] = "Bitwarden Command Palette Extension",
        ["jpsoftworks.toggledarkmodeextension.exe"] = "Toggle Dark Mode Extension",
        ["jpsoftworks.unitconverterextension.exe"] = "Unit Converter Extension",
        ["powertoys.advancedpaste.exe"] = "PowerToys Advanced Paste",
        ["powertoys.alwaysontop.exe"] = "PowerToys Always On Top",
        ["powertoys.awake.exe"] = "PowerToys Awake",
        ["powertoys.colorpickerui.exe"] = "PowerToys Color Picker",
        ["powertoys.cropandlock.exe"] = "PowerToys Crop and Lock",
        ["powertoys.fancyzones.exe"] = "PowerToys FancyZones",
        ["powertoys.peek.ui.exe"] = "PowerToys Peek",
        ["powertoys.powerdisplay.exe"] = "PowerToys PowerDisplay",
        ["powertoys.powerlauncher.exe"] = "PowerToys Run",
        ["powertoys.quickaccess.exe"] = "PowerToys Quick Access",
        ["devhub.exe"] = "Visual Studio Dev Hub",
        ["perfwatson2.exe"] = "Visual Studio PerfWatson",
        ["servicehub.host.extensibility.x64.exe"] = "Visual Studio ServiceHub",
        ["standardcollector.service.exe"] = "Visual Studio Diagnostics Collector",
        ["workspacelauncherforvscode.exe"] = "VS Code Workspace Launcher",
        ["vbcscompiler.exe"] = "VB/C# Compiler",
        ["fdm.exe"] = "Free Download Manager",
        ["helperservice.exe"] = "Free Download Manager Helper",
        ["wenativehost.exe"] = "Free Download Manager Native Host",
        ["flameshot.exe"] = "Flameshot",
        ["teracopyservice.exe"] = "TeraCopy Service",
        ["zima.exe"] = "Zima Client",
        ["zima-backup-v2.exe"] = "Zima Backup",
        ["pia-client.exe"] = "Private Internet Access",
        ["pia-service.exe"] = "Private Internet Access Service",
        ["pia-wgservice.exe"] = "Private Internet Access WireGuard",
        ["tailscaled.exe"] = "Tailscale Service",
        ["zerotier-one_x64.exe"] = "ZeroTier One",
        ["simplewall.exe"] = "simplewall Firewall",
        ["hwinfo.exe"] = "HWiNFO",
        ["trafficmonitor.exe"] = "TrafficMonitor",
        ["trcc.exe"] = "TRCC",
        ["usblcd.exe"] = "USB LCD",
        ["usblcdnew.exe"] = "USB LCD Service",
        ["amdappcompatsvc.exe"] = "AMD App Compatibility Service",
        ["amdfendrsr.exe"] = "AMD Crash Defender",
        ["amdow.exe"] = "AMD Overlay",
        ["amdppkgsvc.exe"] = "AMD Provisioning Service",
        ["amdrsserv.exe"] = "AMD Radeon Settings Service",
        ["amdrssrcext.exe"] = "AMD Radeon Source Extension",
        ["atieclxx.exe"] = "AMD External Events Client",
        ["atiesrxx.exe"] = "AMD External Events Service",
        ["cncmd.exe"] = "AMD CN Command",
        ["cpumetricsserver.exe"] = "AMD CPU Metrics Server",
        ["corsairdevicecontrolservice.exe"] = "Corsair Device Control Service",
        ["easytuneengineservice.exe"] = "GIGABYTE EasyTune Engine",
        ["gcc.exe"] = "GIGABYTE Control Center",
        ["gbt_dl_lib.exe"] = "GIGABYTE Download Library",
        ["rpmdaemon.exe"] = "GIGABYTE Smart Backup",
        ["rtkauduservice64.exe"] = "Realtek Audio Service",
        ["rtkbtmanserv.exe"] = "Realtek Bluetooth Manager",
        ["lghub_updater.exe"] = "Logitech G HUB Updater",
        ["logi_lamparray_service.amd64.exe"] = "Logitech Lighting Service",

        ["amdsoftware.exe"] = "AMD Software",
        ["amdinstallmanager.exe"] = "AMD Install Manager",
        ["radeonsoftware.exe"] = "AMD Radeon Software",
        ["nvidia app.exe"] = "NVIDIA App",
        ["nvcontainer.exe"] = "NVIDIA Container",
        ["nvdisplay.container.exe"] = "NVIDIA Display Container",
        ["afterburner.exe"] = "MSI Afterburner",
        ["msiafterburner.exe"] = "MSI Afterburner",
        ["rtss.exe"] = "RivaTuner Statistics Server",
        ["lghub.exe"] = "Logitech G HUB",
        ["icue.exe"] = "Corsair iCUE",
        ["armourycrate.exe"] = "Armoury Crate",
        ["lightingservice.exe"] = "Aura Lighting Service",
        ["msicenter.exe"] = "MSI Center",
        ["fancontrol.exe"] = "FanControl",
        ["signalrgb.exe"] = "SignalRGB",
        ["openrgb.exe"] = "OpenRGB",

        ["dropbox.exe"] = "Dropbox",
        ["googledrivefs.exe"] = "Google Drive",
        ["googledrivesync.exe"] = "Google Drive Sync",
        ["icloud.exe"] = "iCloud",
        ["icloudphotos.exe"] = "iCloud Photos",
        ["spotify.exe"] = "Spotify",
        ["tidal.exe"] = "TIDAL",
        ["deezer.exe"] = "Deezer",
        ["vlc.exe"] = "VLC media player",
        ["itunes.exe"] = "iTunes",

        ["acrobat.exe"] = "Adobe Acrobat",
        ["acrocef.exe"] = "Adobe Acrobat Helper",
        ["acrotray.exe"] = "Adobe Acrobat Tray",
        ["adobe desktop service.exe"] = "Adobe Desktop Service",
        ["creative cloud.exe"] = "Adobe Creative Cloud",
        ["ccxprocess.exe"] = "Adobe CCXProcess",
        ["adobecollabsync.exe"] = "Adobe Collaboration Sync",
        ["adobenotificationclient.exe"] = "Adobe Notification Client",
        ["acrobatnotificationclient.exe"] = "Adobe Acrobat Notification Client",

        ["delloptimizer.exe"] = "Dell Optimizer",
        ["supportassistagent.exe"] = "Dell SupportAssist",
        ["hpsupportassistant.exe"] = "HP Support Assistant",
        ["lenovovantage.exe"] = "Lenovo Vantage",
        ["dellupdatetray.exe"] = "Dell Update",
        ["dell.commandupdate.exe"] = "Dell Command Update",
        ["lenovosystemupdate.exe"] = "Lenovo System Update",
        ["hpnotifications.exe"] = "HP Notifications",

        ["cursor.exe"] = "Cursor",
        ["warp.exe"] = "Warp Terminal",
        ["terminal.exe"] = "Windows Terminal",
        ["windowsterminal.exe"] = "Windows Terminal",
        ["powershell.exe"] = "PowerShell",
        ["pwsh.exe"] = "PowerShell 7",
        ["cmd.exe"] = "Command Prompt",
        ["python.exe"] = "Python",
        ["node.exe"] = "Node.js",
        ["java.exe"] = "Java",
        ["fiddler.exe"] = "Fiddler",
        ["wireshark.exe"] = "Wireshark",
        ["protonvpn.exe"] = "Proton VPN",
        ["nordvpn.exe"] = "NordVPN",
        ["expressvpn.exe"] = "ExpressVPN",
        ["surfshark.exe"] = "Surfshark",
        ["openvpn-gui.exe"] = "OpenVPN GUI",
        ["tailscale-ipn.exe"] = "Tailscale",
        ["zerotier_desktop_ui.exe"] = "ZeroTier"
    };

    public IReadOnlyList<ProcessGroupSnapshot> GetProcessGroups(Action<string>? trace = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var currentPid = Environment.ProcessId;

        // One native call for every process's PID, name and memory. No Process objects,
        // no per-process kernel calls, no thread lists — see NativeProcessSnapshot.
        // Memory is the private working set: the figure Task Manager shows, and a far
        // better guide to what closing an app frees than the total working set, which
        // counts shared DLL pages once per process (20 browser processes = 20x).
        var processes = NativeProcessSnapshot.GetProcesses();
        var groups = new Dictionary<string, MutableProcessGroup>(StringComparer.OrdinalIgnoreCase);
        var protectedSkips = 0;

        foreach (var process in processes)
        {
            if (process.ProcessId == currentPid || process.ProcessId == 0 || string.IsNullOrEmpty(process.ExeName))
            {
                continue;
            }

            var exeName = SafetyRules.NormaliseProcessName(process.ExeName);
            if (string.IsNullOrWhiteSpace(exeName))
            {
                continue;
            }

            // Hide hard-blocked core rows from the normal list to keep the UI useful and safe.
            if (SafetyRules.IsBlocked(exeName))
            {
                protectedSkips++;
                continue;
            }

            if (!groups.TryGetValue(exeName, out var group))
            {
                group = new MutableProcessGroup(exeName, ClassificationFor(exeName));
                groups.Add(exeName, group);
                QueuePublisherLookup(exeName, process.ProcessId);
            }

            group.MemoryBytes += process.PrivateWorkingSetBytes;
            group.InstanceCount++;
        }

        var result = new List<ProcessGroupSnapshot>(groups.Count);
        foreach (var g in groups.Values)
        {
            result.Add(new ProcessGroupSnapshot(
                g.ExeName,
                g.Classification.DisplayName,
                PublisherFor(g.ExeName),
                g.MemoryBytes,
                g.InstanceCount,
                g.Classification.CanSelect,
                g.Classification.RiskLabel,
                g.Classification.RiskDescription));
        }

        result.Sort(static (x, y) =>
        {
            var byMemory = y.MemoryBytes.CompareTo(x.MemoryBytes);
            return byMemory != 0 ? byMemory : StringComparer.OrdinalIgnoreCase.Compare(x.DisplayName, y.DisplayName);
        });

        stopwatch.Stop();
        trace?.Invoke($"Process refresh scanned={processes.Count}; grouped={result.Count}; skippedProtected={protectedSkips}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return result;
    }

    // Friendly name + safety classification depend only on the exe name, so work them out
    // once per exe for the whole session instead of on every scan.
    private static readonly ConcurrentDictionary<string, AppClassification> ClassificationCache = new(StringComparer.OrdinalIgnoreCase);

    private static AppClassification ClassificationFor(string exeName)
    {
        return ClassificationCache.GetOrAdd(exeName, static name =>
        {
            var risk = SafetyRules.GetRisk(name);
            return new AppClassification(FriendlyNameFor(name), risk.CanSelect, risk.Label, risk.Description);
        });
    }

    private sealed record AppClassification(string DisplayName, bool CanSelect, string RiskLabel, string RiskDescription);

    /// <summary>
    /// Pages out the memory of every running process with one of these exe names, without
    /// closing anything. Used for apps tagged Respawns (Windows shell hosts and the like),
    /// which Windows restarts as soon as they close: trimming moves their pages to the
    /// standby list, where Windows hands them to a game first, and a page only comes back
    /// if the app actually touches it again.
    /// </summary>
    public TrimResult TrimWorkingSets(IReadOnlyCollection<string> executableNames, Action<string>? trace = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var targets = new HashSet<string>(
            executableNames
                .Select(SafetyRules.NormaliseProcessName)
                .Where(n => !string.IsNullOrWhiteSpace(n) && !SafetyRules.IsBlocked(n)),
            StringComparer.OrdinalIgnoreCase);
        if (targets.Count == 0)
        {
            return TrimResult.Empty;
        }

        var currentPid = Environment.ProcessId;
        var trimmed = new HashSet<int>();
        var failed = 0;
        long before = 0;

        foreach (var p in NativeProcessSnapshot.GetProcesses())
        {
            if (p.ProcessId == 0 || p.ProcessId == currentPid || string.IsNullOrEmpty(p.ExeName)
                || !targets.Contains(SafetyRules.NormaliseProcessName(p.ExeName)))
            {
                continue;
            }

            var error = TrimProcessWorkingSet(p.ProcessId);
            if (error == 0)
            {
                trimmed.Add(p.ProcessId);
                before += p.PrivateWorkingSetBytes;
            }
            else
            {
                // Usually access denied (5): an elevated or protected process while
                // Mem-Booster itself isn't elevated.
                failed++;
                trace?.Invoke($"Trim refused: {p.ExeName} pid={p.ProcessId} error={error}");
            }
        }

        long after = 0;
        if (trimmed.Count > 0)
        {
            foreach (var p in NativeProcessSnapshot.GetProcesses())
            {
                if (trimmed.Contains(p.ProcessId))
                {
                    after += p.PrivateWorkingSetBytes;
                }
            }
        }

        stopwatch.Stop();
        var result = new TrimResult(trimmed.Count, failed, before, after);
        trace?.Invoke($"Trim complete apps={targets.Count}; processes={trimmed.Count}; failed={failed}; privateWorkingSetMB={before / 1048576}->{after / 1048576}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return result;
    }

    // Returns 0 on success, otherwise the Win32 error code.
    private static int TrimProcessWorkingSet(int pid)
    {
        const uint ProcessSetQuota = 0x0100;
        const uint ProcessQueryLimitedInformation = 0x1000;
        var handle = OpenProcess(ProcessSetQuota | ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return LastErrorOrUnknown();
        }

        try
        {
            // (SIZE_T)-1 for both limits = remove as many pages as possible.
            return SetProcessWorkingSetSize(handle, -1, -1) ? 0 : LastErrorOrUnknown();
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    // A failed call should never read as success, even if Windows left no error code.
    private static int LastErrorOrUnknown()
    {
        var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        return error == 0 ? -1 : error;
    }

    public BoostResult KillProcessTreesByExecutableNames(IReadOnlyCollection<string> executableNames, TerminationOptions? options = null, Action<string>? trace = null)
    {
        options ??= TerminationOptions.Default;
        var stopwatch = Stopwatch.StartNew();
        trace?.Invoke($"Boost kill start profileEntries={executableNames.Count}; graceful={options.TryGracefulCloseFirst}; gracefulTimeoutMs={options.GracefulCloseTimeoutMs}; forceTimeoutMs={options.ForceKillTimeoutMs}");

        var targets = executableNames
            .Select(SafetyRules.NormaliseProcessName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !SafetyRules.IsBlocked(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targets.Count == 0)
        {
            trace?.Invoke("Boost kill skipped: no selectable targets.");
            return new BoostResult(0, 0, 0, 0, 0, new List<string> { "No selectable profile entries were provided." });
        }

        var nativeSnapshotWatch = Stopwatch.StartNew();
        var nativeProcesses = NativeProcessSnapshot.GetProcesses();
        nativeSnapshotWatch.Stop();
        trace?.Invoke($"Native process snapshot count={nativeProcesses.Count}; elapsedMs={nativeSnapshotWatch.ElapsedMilliseconds}");
        var runningTargetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootPids = new HashSet<int>();
        var byPid = new Dictionary<int, NativeProcessEntry>(nativeProcesses.Count);
        foreach (var p in nativeProcesses)
        {
            byPid.TryAdd(p.ProcessId, p);
            if (p.ProcessId == 0 || string.IsNullOrEmpty(p.ExeName))
            {
                continue;
            }

            var name = SafetyRules.NormaliseProcessName(p.ExeName);
            if (targets.Contains(name))
            {
                runningTargetNames.Add(name);
                rootPids.Add(p.ProcessId);
            }
        }

        // Windows keeps a process's parent PID after the parent exits, and PIDs get reused.
        // Only count a process as a child if it started after its current parent did —
        // otherwise an unrelated app could be pulled into a target's tree and closed.
        var byParent = new Dictionary<int, List<NativeProcessEntry>>();
        foreach (var p in nativeProcesses)
        {
            if (p.ProcessId == p.ParentProcessId
                || !byPid.TryGetValue(p.ParentProcessId, out var parent)
                || p.CreateTime < parent.CreateTime)
            {
                continue;
            }

            if (!byParent.TryGetValue(p.ParentProcessId, out var children))
            {
                children = new List<NativeProcessEntry>();
                byParent.Add(p.ParentProcessId, children);
            }

            children.Add(p);
        }

        trace?.Invoke($"Boost targets={targets.Count}; runningTargetNames={runningTargetNames.Count}; rootPids={rootPids.Count}");

        var allPids = new HashSet<int>();
        foreach (var rootPid in rootPids)
        {
            AddTree(rootPid, byParent, allPids);
        }

        var currentPid = Environment.ProcessId;
        var orderedPids = allPids
            .Where(pid => pid != currentPid)
            .OrderByDescending(pid => GetDepth(pid, byPid, allPids))
            .ToList();
        trace?.Invoke($"Boost process tree pids={orderedPids.Count}; currentPid={currentPid}");

        var killed = 0;
        var closedGracefully = 0;
        var skipped = 0;
        var failed = 0;
        var messages = new ConcurrentQueue<string>();

        // Resolve and safety-check every target once, up front.
        var work = new List<(int Pid, string ExeName)>(orderedPids.Count);
        foreach (var pid in orderedPids)
        {
            if (!byPid.TryGetValue(pid, out var entry))
            {
                skipped++;
                continue;
            }

            var exeName = SafetyRules.NormaliseProcessName(entry.ExeName);
            if (SafetyRules.IsBlocked(exeName))
            {
                skipped++;
                messages.Enqueue($"Skipped protected process: {exeName} ({pid})");
                trace?.Invoke($"Boost skip protected: {exeName} pid={pid}");
                continue;
            }

            work.Add((pid, exeName));
        }

        // Closing is almost entirely waiting (window close, process teardown), so the
        // old one-by-one loop paid the sum of every wait. Running side by side pays
        // roughly the longest single wait instead. Bounded so we never flood the system.
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount * 2, 4, 32) };
        var handled = new ConcurrentDictionary<int, byte>();
        var closedNames = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // Phase 1 — graceful: ask windowed processes to close so apps can save state.
        if (options.TryGracefulCloseFirst && options.GracefulCloseTimeoutMs > 0)
        {
            Parallel.ForEach(work, parallel, item =>
            {
                try
                {
                    using var process = Process.GetProcessById(item.Pid);
                    if (!IsSameProcess(process, item.ExeName))
                    {
                        // Exited, or the PID was reused by an unrelated process: never touch it.
                        handled.TryAdd(item.Pid, 0);
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    if (process.MainWindowHandle != IntPtr.Zero
                        && process.CloseMainWindow()
                        && process.WaitForExit(options.GracefulCloseTimeoutMs))
                    {
                        handled.TryAdd(item.Pid, 0);
                        closedNames.TryAdd(item.ExeName, 0);
                        Interlocked.Increment(ref closedGracefully);
                        trace?.Invoke($"Boost closed gracefully: {item.ExeName} pid={item.Pid}");
                    }
                }
                catch (ArgumentException)
                {
                    handled.TryAdd(item.Pid, 0);
                    Interlocked.Increment(ref skipped); // Already gone.
                }
                catch
                {
                    // Fall through to force-kill in phase 2.
                }
            });
        }

        // Phase 2 — force: everything still alive.
        Parallel.ForEach(work, parallel, item =>
        {
            if (handled.ContainsKey(item.Pid))
            {
                return;
            }

            try
            {
                using var process = Process.GetProcessById(item.Pid);
                if (!IsSameProcess(process, item.ExeName))
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                process.Kill(entireProcessTree: false);
                if (process.WaitForExit(options.ForceKillTimeoutMs))
                {
                    Interlocked.Increment(ref killed);
                    closedNames.TryAdd(item.ExeName, 0);
                    trace?.Invoke($"Boost force killed: {item.ExeName} pid={item.Pid}");
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    messages.Enqueue($"Timed out closing: {item.ExeName} ({item.Pid})");
                    trace?.Invoke($"Boost timeout: {item.ExeName} pid={item.Pid}");
                }
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref skipped); // Exited on its own (e.g. parent took it down).
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref skipped); // Exited between lookup and kill.
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                messages.Enqueue($"Failed {item.ExeName} ({item.Pid}): {ex.Message}");
                trace?.Invoke($"Boost failed: {item.ExeName} pid={item.Pid}; {ex.Message}");
            }
        });

        var missing = targets.Count(name => !runningTargetNames.Contains(name));
        stopwatch.Stop();
        trace?.Invoke($"Boost kill complete killed={killed}; graceful={closedGracefully}; skipped={skipped}; failed={failed}; missing={missing}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return new BoostResult(killed, closedGracefully, skipped, failed, missing, messages.ToList())
        {
            ClosedExeNames = closedNames.Keys.ToArray()
        };
    }

    // Guards against PID reuse: between the snapshot and the kill, a target may exit and
    // Windows may hand its PID to an unrelated process. Only act if the name still matches.
    private static bool IsSameProcess(Process process, string expectedExeName)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
        }
        catch
        {
            // HasExited can be access-denied for some processes; fall through to the name check.
        }

        try
        {
            return string.Equals(
                SafetyRules.NormaliseProcessName(process.ProcessName),
                expectedExeName,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false; // Exited (ProcessName throws once a process is gone).
        }
    }

    private static void AddTree(int pid, Dictionary<int, List<NativeProcessEntry>> byParent, HashSet<int> results)
    {
        if (!results.Add(pid))
        {
            return;
        }

        if (!byParent.TryGetValue(pid, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            AddTree(child.ProcessId, byParent, results);
        }
    }

    private static int GetDepth(int pid, Dictionary<int, NativeProcessEntry> byPid, HashSet<int> selectedPids)
    {
        var depth = 0;
        var seen = new HashSet<int>();
        var current = pid;

        while (byPid.TryGetValue(current, out var entry) && selectedPids.Contains(entry.ParentProcessId) && seen.Add(current))
        {
            depth++;
            current = entry.ParentProcessId;
        }

        return depth;
    }

    // exeName -> publisher (CompanyName). Session-lifetime cache: each unique exe is
    // looked up once. Failures are cached as "Unknown publisher" so they are not retried
    // on every refresh.
    private const string UnknownPublisher = "Unknown publisher";
    private static readonly ConcurrentDictionary<string, string> PublisherCache = new(StringComparer.OrdinalIgnoreCase);

    // exeName -> one PID to read it from. Filled during the scan, drained by
    // ResolvePendingPublishers. Keeps the scan itself free of file/disk work so the
    // list can appear immediately.
    private static readonly ConcurrentDictionary<string, int> PendingPublisherPids = new(StringComparer.OrdinalIgnoreCase);

    private static void QueuePublisherLookup(string exeName, int pid)
    {
        if (!PublisherCache.ContainsKey(exeName))
        {
            PendingPublisherPids.TryAdd(exeName, pid);
        }
    }

    private static string PublisherFor(string exeName)
    {
        // Empty = still being looked up; the UI fills it in when resolution finishes.
        return PublisherCache.TryGetValue(exeName, out var value) ? value : string.Empty;
    }

    public bool HasPendingPublishers => !PendingPublisherPids.IsEmpty;

    /// <summary>
    /// Resolves publishers queued by the last scan. Runs off the UI thread, after the
    /// list is already showing. Returns only the entries resolved by this call.
    /// </summary>
    public IReadOnlyDictionary<string, string> ResolvePendingPublishers()
    {
        var work = new List<KeyValuePair<string, int>>();
        foreach (var pending in PendingPublisherPids)
        {
            if (PendingPublisherPids.TryRemove(pending.Key, out var pid))
            {
                work.Add(new KeyValuePair<string, int>(pending.Key, pid));
            }
        }

        var resolved = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 16) }, item =>
        {
            var publisher = UnknownPublisher;
            var path = TryGetImagePath(item.Value);
            if (string.IsNullOrWhiteSpace(path))
            {
                // Process exited or denied access. Don't cache: the next scan re-queues it
                // from a fresh PID, which costs only one OpenProcess call.
                resolved[item.Key] = publisher;
                return;
            }

            try
            {
                var company = FileVersionInfo.GetVersionInfo(path).CompanyName?.Trim();
                if (!string.IsNullOrEmpty(company))
                {
                    publisher = company;
                }
            }
            catch
            {
                // Missing file or no version resource: stays "Unknown publisher".
            }

            PublisherCache[item.Key] = publisher;
            resolved[item.Key] = publisher;
        });

        return resolved;
    }

    // QueryFullProcessImageNameW with limited-query rights: one cheap kernel call, and it
    // works on more processes (including many elevated ones) than Process.MainModule,
    // which enumerates every module loaded in the target process.
    private static string? TryGetImagePath(int pid)
    {
        const uint ProcessQueryLimitedInformation = 0x1000;
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new char[1024];
            var size = buffer.Length;
            return QueryFullProcessImageNameW(handle, 0, buffer, ref size) && size > 0
                ? new string(buffer, 0, size)
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool inheritHandle, int processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, [System.Runtime.InteropServices.Out] char[] exeName, ref int size);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimumWorkingSetSize, nint maximumWorkingSetSize);

    private static string FriendlyNameFor(string exeName)
    {
        if (FriendlyNames.TryGetValue(exeName, out var friendlyName))
        {
            return friendlyName;
        }

        var withoutExtension = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeName[..^4]
            : exeName;

        return withoutExtension
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();
    }

    private sealed class MutableProcessGroup
    {
        public string ExeName { get; }
        public AppClassification Classification { get; }
        public long MemoryBytes { get; set; }
        public int InstanceCount { get; set; }

        public MutableProcessGroup(string exeName, AppClassification classification)
        {
            ExeName = exeName;
            Classification = classification;
        }
    }
}

/// <param name="BytesBefore">Private working set of the trimmed processes just before.</param>
/// <param name="BytesAfter">The same processes straight after the trim.</param>
public sealed record TrimResult(int Trimmed, int Failed, long BytesBefore, long BytesAfter)
{
    public static TrimResult Empty { get; } = new(0, 0, 0, 0);

    public long BytesTrimmed => Math.Max(0, BytesBefore - BytesAfter);
}

public sealed record TerminationOptions(bool TryGracefulCloseFirst, int GracefulCloseTimeoutMs, int ForceKillTimeoutMs)
{
    public static TerminationOptions Default { get; } = new(true, 900, 1500);
}

public sealed record BoostResult(int Killed, int ClosedGracefully, int Skipped, int Failed, int MissingProfileEntries, IReadOnlyList<string> Messages)
{
    /// <summary>Exe names with at least one process closed by this boost.</summary>
    public IReadOnlyCollection<string> ClosedExeNames { get; init; } = Array.Empty<string>();

    public static BoostResult Empty { get; } = new(0, 0, 0, 0, 0, Array.Empty<string>());

    /// <summary>Adds up two kill passes from the same boost.</summary>
    public static BoostResult Combine(BoostResult first, BoostResult second)
    {
        return new BoostResult(
            first.Killed + second.Killed,
            first.ClosedGracefully + second.ClosedGracefully,
            first.Skipped + second.Skipped,
            first.Failed + second.Failed,
            first.MissingProfileEntries + second.MissingProfileEntries,
            first.Messages.Concat(second.Messages).ToList())
        {
            ClosedExeNames = first.ClosedExeNames
                .Concat(second.ClosedExeNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public string Summary => $"Force-killed: {Killed}, closed cleanly: {ClosedGracefully}, skipped: {Skipped}, failed: {Failed}, profile entries not running: {MissingProfileEntries}";
}

