namespace MemBooster.Services;

public static class SafetyRules
{
    private static readonly HashSet<string> BlockedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "idle",
        "registry",
        "smss.exe",
        "csrss.exe",
        "wininit.exe",
        "services.exe",
        "lsass.exe",
        "lsaiso.exe",
        "svchost.exe",
        "winlogon.exe",
        "fontdrvhost.exe",
        "dwm.exe",
        "memory compression",
        "secure system",
        "system idle process",
        "mem-booster.exe",
        "membooster.exe"
    };

    private static readonly HashSet<string> CautionProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe",
        "steam.exe",
        "steamwebhelper.exe",
        "epicgameslauncher.exe",
        "eadesktop.exe",
        "riotclientservices.exe",
        "battle.net.exe",
        "battlenet.exe",
        "discord.exe",
        "amdsoftware.exe",
        "radeonsoftware.exe",
        "amdow.exe",
        "amdrsserv.exe",
        "nvidia app.exe",
        "nvcontainer.exe",
        "nvdisplay.container.exe",
        "afterburner.exe",
        "rtss.exe",
        "lghub.exe",
        "icue.exe",
        "armourycrate.exe",
        "lightingservice.exe",
        "msi center.exe",
        "fancontrol.exe",
        "signalrgb.exe"
    };

    private static readonly HashSet<string> RecommendedGamingBoostNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge.exe",
        "chrome.exe",
        "brave.exe",
        "firefox.exe",
        "opera.exe",
        "operagx.exe",
        "teams.exe",
        "ms-teams.exe",
        "onedrive.exe",
        "widgetboard.exe",
        "widgetservice.exe",
        "microsoftstartfeedprovider.exe",
        "xboxapp.exe",
        "xboxappservices.exe",
        "gamebar.exe",
        "gamebarftserver.exe",
        "yourphone.exe",
        "phoneexperiencehost.exe",
        "photos.exe",
        "microsoft.photos.exe",
        "amdinstallmanager.exe"
    };

    public static bool IsBlocked(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        return BlockedProcessNames.Contains(normalised);
    }

    public static bool IsRecommendedForGamingBoost(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        return !IsBlocked(normalised) && RecommendedGamingBoostNames.Contains(normalised);
    }

    public static ProcessRisk GetRisk(string processName)
    {
        var normalised = NormaliseProcessName(processName);

        if (IsBlocked(normalised))
        {
            return new ProcessRisk(false, "Blocked", "Protected Windows/core process");
        }

        if (CautionProcessNames.Contains(normalised))
        {
            return new ProcessRisk(true, "Caution", "May be needed for games, drivers, overlays, RGB, fan control or shell features");
        }

        if (RecommendedGamingBoostNames.Contains(normalised))
        {
            return new ProcessRisk(true, "Safe pick", "Common background app often safe to close before gaming");
        }

        return new ProcessRisk(true, "Low", "User-level app");
    }

    public static string NormaliseProcessName(string processName)
    {
        var value = processName.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("system", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("idle", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("registry", StringComparison.OrdinalIgnoreCase)
            && !value.Contains(' '))
        {
            value += ".exe";
        }

        return value;
    }
}

public sealed record ProcessRisk(bool CanSelect, string Label, string Description);
