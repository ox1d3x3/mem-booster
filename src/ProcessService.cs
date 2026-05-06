using System.Diagnostics;
using MemBooster.Models;

namespace MemBooster.Services;

public sealed class ProcessService
{
    private static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge.exe"] = "Microsoft Edge",
        ["chrome.exe"] = "Google Chrome",
        ["brave.exe"] = "Brave Browser",
        ["firefox.exe"] = "Mozilla Firefox",
        ["opera.exe"] = "Opera",
        ["operagx.exe"] = "Opera GX",
        ["steam.exe"] = "Steam",
        ["steamwebhelper.exe"] = "Steam WebHelper",
        ["discord.exe"] = "Discord",
        ["teams.exe"] = "Microsoft Teams",
        ["ms-teams.exe"] = "Microsoft Teams",
        ["onedrive.exe"] = "OneDrive",
        ["widgetboard.exe"] = "Windows Widgets",
        ["widgetservice.exe"] = "Windows Widgets Service",
        ["microsoftstartfeedprovider.exe"] = "Microsoft Start Feed",
        ["xboxapp.exe"] = "Xbox App",
        ["xboxappservices.exe"] = "Xbox App Services",
        ["gamebar.exe"] = "Xbox Game Bar",
        ["gamebarftserver.exe"] = "Xbox Game Bar Full Trust",
        ["amdsoftware.exe"] = "AMD Software",
        ["amdinstallmanager.exe"] = "AMD Install Manager",
        ["radeonsoftware.exe"] = "AMD Radeon Software",
        ["epicgameslauncher.exe"] = "Epic Games Launcher",
        ["eadesktop.exe"] = "EA App",
        ["battle.net.exe"] = "Battle.net",
        ["battlenet.exe"] = "Battle.net",
        ["riotclientservices.exe"] = "Riot Client",
        ["lghub.exe"] = "Logitech G HUB",
        ["icue.exe"] = "Corsair iCUE",
        ["afterburner.exe"] = "MSI Afterburner",
        ["rtss.exe"] = "RivaTuner Statistics Server",
        ["phoneexperiencehost.exe"] = "Phone Link",
        ["yourphone.exe"] = "Phone Link"
    };

    public IReadOnlyList<ProcessGroupSnapshot> GetProcessGroups()
    {
        var groups = new Dictionary<string, MutableProcessGroup>(StringComparer.OrdinalIgnoreCase);
        var currentPid = Environment.ProcessId;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
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
                // Some protected processes deny access. Skip them instead of slowing or crashing the UI.
            }
            finally
            {
                process.Dispose();
            }
        }

        return groups.Values
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
    }

    public BoostResult KillProcessTreesByExecutableNames(IReadOnlyCollection<string> executableNames, TerminationOptions? options = null)
    {
        options ??= TerminationOptions.Default;

        var targets = executableNames
            .Select(SafetyRules.NormaliseProcessName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !SafetyRules.IsBlocked(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targets.Count == 0)
        {
            return new BoostResult(0, 0, 0, 0, 0, new List<string> { "No selectable profile entries were provided." });
        }

        var nativeProcesses = NativeProcessSnapshot.GetProcesses();
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
                }
                else
                {
                    failed++;
                    messages.Add($"Timed out closing: {exeName} ({pid})");
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
            }
        }

        var missing = targets.Count(name => !runningTargetNames.Contains(name));
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
