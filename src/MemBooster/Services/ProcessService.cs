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
        var groups = new Dictionary<string, MutableProcessGroup>(StringComparer.OrdinalIgnoreCase);
        var currentPid = Environment.ProcessId;
        var scanned = 0;
        var skippedProtected = 0;
        var skippedDeniedOrExited = 0;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                scanned++;
                if (process.Id == currentPid)
                {
                    continue;
                }

                var exeName = SafetyRules.NormaliseProcessName(process.ProcessName);
                if (string.IsNullOrWhiteSpace(exeName))
                {
                    continue;
                }

                // Hide hard-blocked core rows from the normal list to keep the UI useful and safe.
                if (SafetyRules.IsBlocked(exeName))
                {
                    skippedProtected++;
                    continue;
                }

                if (!groups.TryGetValue(exeName, out var group))
                {
                    var risk = SafetyRules.GetRisk(exeName);
                    group = new MutableProcessGroup(
                        exeName,
                        FriendlyNameFor(exeName),
                        risk.CanSelect,
                        SafetyRules.IsRecommendedForGamingBoost(exeName),
                        risk.Label,
                        risk.Description);
                    groups.Add(exeName, group);
                }

                group.WorkingSetBytes += SafeWorkingSet(process);
                group.InstanceCount++;
            }
            catch
            {
                skippedDeniedOrExited++;
                // Some protected processes deny access. Skip them instead of slowing or crashing the UI.
            }
            finally
            {
                process.Dispose();
            }
        }

        var result = groups.Values
            .Select(g => new ProcessGroupSnapshot(
                g.ExeName,
                g.DisplayName,
                g.WorkingSetBytes,
                g.InstanceCount,
                g.CanSelect,
                g.IsRecommended,
                g.RiskLabel,
                g.RiskDescription))
            .OrderByDescending(g => g.WorkingSetBytes)
            .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        stopwatch.Stop();
        trace?.Invoke($"Process refresh scanned={scanned}; grouped={result.Count}; skippedProtected={skippedProtected}; skippedDeniedOrExited={skippedDeniedOrExited}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return result;
    }

    public IReadOnlyList<RestoreEntry> GetRestorableProcessesByExecutableNames(IReadOnlyCollection<string> executableNames, Action<string>? trace = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var targets = executableNames
            .Select(SafetyRules.NormaliseProcessName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !SafetyRules.IsBlocked(n))
            .Where(SafetyRules.IsRestoreCandidate)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targets.Count == 0)
        {
            trace?.Invoke("Restore capture skipped: no restore targets.");
            return Array.Empty<RestoreEntry>();
        }

        var currentPid = Environment.ProcessId;
        var entries = new Dictionary<string, RestoreEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentPid)
                {
                    continue;
                }

                var exeName = SafetyRules.NormaliseProcessName(process.ProcessName);
                if (!targets.Contains(exeName) || SafetyRules.IsBlocked(exeName) || !SafetyRules.IsRestoreCandidate(exeName))
                {
                    continue;
                }

                var filePath = TryGetMainModulePath(process);
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    continue;
                }

                if (!entries.ContainsKey(filePath))
                {
                    entries.Add(filePath, new RestoreEntry(
                        exeName,
                        FriendlyNameFor(exeName),
                        filePath,
                        DateTime.UtcNow));
                }
            }
            catch
            {
                // Restore capture is best effort. Skip protected or short-lived processes.
            }
            finally
            {
                process.Dispose();
            }
        }

        var result = entries.Values
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ExeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        stopwatch.Stop();
        trace?.Invoke($"Restore capture targets={targets.Count}; restorable={result.Count}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return result;
    }

    public RestoreResult RestoreProcesses(IReadOnlyCollection<RestoreEntry> entries, Action<string>? trace = null)
    {
        var stopwatch = Stopwatch.StartNew();
        trace?.Invoke($"Restore start entries={entries.Count}");

        if (entries.Count == 0)
        {
            return new RestoreResult(0, 0, 0, new List<string> { "No restore entries were available." });
        }

        var runningNames = Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    return SafetyRules.NormaliseProcessName(p.ProcessName);
                }
                catch
                {
                    return string.Empty;
                }
                finally
                {
                    p.Dispose();
                }
            })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var restored = 0;
        var skipped = 0;
        var failed = 0;
        var messages = new List<string>();

        foreach (var entry in entries
            .Where(e => !string.IsNullOrWhiteSpace(e.FilePath))
            .DistinctBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var exeName = SafetyRules.NormaliseProcessName(entry.ExeName);
            if (SafetyRules.IsBlocked(exeName) || !SafetyRules.IsRestoreCandidate(exeName))
            {
                skipped++;
                messages.Add($"Skipped helper/protected app: {entry.DisplayName}");
                trace?.Invoke($"Restore skip helper/protected: {entry.DisplayName} | {exeName}");
                continue;
            }

            if (runningNames.Contains(exeName))
            {
                skipped++;
                messages.Add($"Already running: {entry.DisplayName}");
                trace?.Invoke($"Restore skip already running: {entry.DisplayName} | {exeName}");
                continue;
            }

            if (!SafetyRules.IsRestorePathAllowed(entry.FilePath))
            {
                skipped++;
                messages.Add($"Skipped packaged/helper app: {entry.DisplayName}");
                trace?.Invoke($"Restore skip packaged/helper path: {entry.DisplayName} | {entry.FilePath}");
                continue;
            }

            if (!File.Exists(entry.FilePath))
            {
                failed++;
                messages.Add($"Missing file: {entry.DisplayName}");
                trace?.Invoke($"Restore missing file: {entry.DisplayName} | {entry.FilePath}");
                continue;
            }

            try
            {
                var startInfo = new ProcessStartInfo(entry.FilePath)
                {
                    UseShellExecute = true
                };

                var directory = Path.GetDirectoryName(entry.FilePath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    startInfo.WorkingDirectory = directory;
                }

                Process.Start(startInfo);
                runningNames.Add(exeName);
                restored++;
                messages.Add($"Started: {entry.DisplayName}");
                trace?.Invoke($"Restore started: {entry.DisplayName} | {entry.FilePath}");
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add($"Failed {entry.DisplayName}: {ex.Message}");
                trace?.Invoke($"Restore failed: {entry.DisplayName} | {ex.Message}");
            }
        }

        stopwatch.Stop();
        trace?.Invoke($"Restore complete restored={restored}; skipped={skipped}; failed={failed}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return new RestoreResult(restored, skipped, failed, messages);
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
        var runningTargetNames = nativeProcesses
            .Select(p => SafetyRules.NormaliseProcessName(p.ExeName))
            .Where(targets.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var byParent = nativeProcesses
            .GroupBy(p => p.ParentProcessId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var byPid = nativeProcesses.ToDictionary(p => p.ProcessId, p => p);
        var rootPids = nativeProcesses
            .Where(p => runningTargetNames.Contains(SafetyRules.NormaliseProcessName(p.ExeName)))
            .Select(p => p.ProcessId)
            .ToHashSet();
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
        var messages = new List<string>();

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
                messages.Add($"Skipped protected process: {exeName} ({pid})");
                trace?.Invoke($"Boost skip protected: {exeName} pid={pid}");
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(pid);

                if (process.HasExited)
                {
                    skipped++;
                    continue;
                }

                if (options.TryGracefulCloseFirst && process.MainWindowHandle != IntPtr.Zero)
                {
                    try
                    {
                        if (process.CloseMainWindow() && process.WaitForExit(options.GracefulCloseTimeoutMs))
                        {
                            closedGracefully++;
                            trace?.Invoke($"Boost closed gracefully: {exeName} pid={pid}");
                            continue;
                        }
                    }
                    catch
                    {
                        // Fall back to force-kill below.
                    }
                }

                process.Kill(entireProcessTree: false);
                if (process.WaitForExit(options.ForceKillTimeoutMs))
                {
                    killed++;
                    trace?.Invoke($"Boost force killed: {exeName} pid={pid}");
                }
                else
                {
                    failed++;
                    messages.Add($"Timed out closing: {exeName} ({pid})");
                    trace?.Invoke($"Boost timeout: {exeName} pid={pid}");
                }
            }
            catch (ArgumentException)
            {
                skipped++;
            }
            catch (Exception ex)
            {
                failed++;
                messages.Add($"Failed {exeName} ({pid}): {ex.Message}");
                trace?.Invoke($"Boost failed: {exeName} pid={pid}; {ex.Message}");
            }
        }

        var missing = targets.Count(name => !runningTargetNames.Contains(name));
        stopwatch.Stop();
        trace?.Invoke($"Boost kill complete killed={killed}; graceful={closedGracefully}; skipped={skipped}; failed={failed}; missing={missing}; elapsedMs={stopwatch.ElapsedMilliseconds}");
        return new BoostResult(killed, closedGracefully, skipped, failed, missing, messages);
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

    private static long SafeWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private static string? TryGetMainModulePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

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
        public string DisplayName { get; }
        public long WorkingSetBytes { get; set; }
        public int InstanceCount { get; set; }
        public bool CanSelect { get; }
        public bool IsRecommended { get; }
        public string RiskLabel { get; }
        public string RiskDescription { get; }

        public MutableProcessGroup(string exeName, string displayName, bool canSelect, bool isRecommended, string riskLabel, string riskDescription)
        {
            ExeName = exeName;
            DisplayName = displayName;
            CanSelect = canSelect;
            IsRecommended = isRecommended;
            RiskLabel = riskLabel;
            RiskDescription = riskDescription;
        }
    }
}

public sealed record TerminationOptions(bool TryGracefulCloseFirst, int GracefulCloseTimeoutMs, int ForceKillTimeoutMs)
{
    public static TerminationOptions Default { get; } = new(true, 900, 1500);
}

public sealed record BoostResult(int Killed, int ClosedGracefully, int Skipped, int Failed, int MissingProfileEntries, IReadOnlyList<string> Messages)
{
    public string Summary => $"Force-killed: {Killed}, closed cleanly: {ClosedGracefully}, skipped: {Skipped}, failed: {Failed}, profile entries not running: {MissingProfileEntries}";
}

public sealed record RestoreEntry(string ExeName, string DisplayName, string FilePath, DateTime CapturedUtc);

public sealed record RestoreResult(int Restored, int Skipped, int Failed, IReadOnlyList<string> Messages)
{
    public string Summary => $"Restored: {Restored}, skipped: {Skipped}, failed: {Failed}";
}
