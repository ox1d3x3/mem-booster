using System.IO;
using System.Xml.Linq;

namespace MemBooster.Services;

public sealed class ProfileService
{
    private static readonly XName RootName = "MemBoosterProfile";
    private static readonly XName ProcessesName = "Processes";
    private static readonly XName ProcessName = "Process";

    public string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Mem-Booster");

    public string LocalProfilePath => Path.Combine(AppDataDirectory, "local-profile.xml");

    public string SettingsPath => Path.Combine(AppDataDirectory, "settings.xml");

    public string LastRestoreSessionPath => Path.Combine(AppDataDirectory, "last-restore-session.xml");


    public string LoadThemePreference() => LoadSettings().Theme;

    public void SaveThemePreference(string theme)
    {
        SaveSettings(LoadSettings() with { Theme = IsValidTheme(theme) ? theme : "Dark" });
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            return AppSettings.Default;
        }

        try
        {
            var root = XDocument.Load(SettingsPath, LoadOptions.None).Root;
            var theme = (string?)root?.Attribute("theme") ?? "Dark";
            DateTime? lastCheck = DateTime.TryParse(
                (string?)root?.Attribute("lastUpdateCheckUtc"),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : null;

            return new AppSettings(
                IsValidTheme(theme) ? theme : "Dark",
                lastCheck,
                (string?)root?.Attribute("latestRelease"),
                (string?)root?.Attribute("latestReleaseUrl"));
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            var root = new XElement("MemBoosterSettings",
                new XAttribute("version", AppInfo.Version),
                new XAttribute("theme", IsValidTheme(settings.Theme) ? settings.Theme : "Dark"));

            if (settings.LastUpdateCheckUtc is { } lastCheck)
            {
                root.Add(new XAttribute("lastUpdateCheckUtc", lastCheck.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            }

            if (!string.IsNullOrWhiteSpace(settings.LatestReleaseVersion))
            {
                root.Add(new XAttribute("latestRelease", settings.LatestReleaseVersion));
            }

            if (!string.IsNullOrWhiteSpace(settings.LatestReleaseUrl))
            {
                root.Add(new XAttribute("latestReleaseUrl", settings.LatestReleaseUrl));
            }

            new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(SettingsPath);
        }
        catch
        {
            // Settings are a convenience; never fail the app over them.
        }
    }

    private static bool IsValidTheme(string theme)
    {
        return string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
    }

    public ProfileData LoadLocalProfile()
    {
        if (!File.Exists(LocalProfilePath))
        {
            return new ProfileData("Local", Array.Empty<string>());
        }

        return LoadProfile(LocalProfilePath);
    }

    public void SaveLocalProfile(IEnumerable<string> executableNames)
    {
        Directory.CreateDirectory(AppDataDirectory);
        SaveProfile(LocalProfilePath, new ProfileData("Local Gaming Boost", executableNames));
    }

    public ProfileData LoadProfile(string path)
    {
        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root;
        if (root is null || root.Name != RootName)
        {
            throw new InvalidDataException("This does not look like a Mem-Booster XML profile.");
        }

        var profileName = (string?)root.Attribute("name") ?? Path.GetFileNameWithoutExtension(path);
        var names = root.Element(ProcessesName)?
            .Elements(ProcessName)
            .Select(e => (string?)e.Attribute("executable"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => SafetyRules.NormaliseProcessName(v!))
            .Where(v => !SafetyRules.IsBlocked(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        return new ProfileData(profileName, names);
    }

    public void SaveProfile(string path, ProfileData profile)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var names = profile.ExecutableNames
            .Select(SafetyRules.NormaliseProcessName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !SafetyRules.IsBlocked(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(RootName,
                new XAttribute("version", AppInfo.Version),
                new XAttribute("name", string.IsNullOrWhiteSpace(profile.Name) ? "Gaming Boost" : profile.Name),
                new XAttribute("createdUtc", DateTime.UtcNow.ToString("O")),
                new XAttribute("author", AppInfo.Author),
                new XAttribute("github", AppInfo.RepositoryUrl),
                new XElement(ProcessesName,
                    names.Select(name => new XElement(ProcessName,
                        new XAttribute("executable", name))))));

        document.Save(path);
    }

    public void ClearRestoreSession()
    {
        if (File.Exists(LastRestoreSessionPath))
        {
            File.Delete(LastRestoreSessionPath);
        }
    }

}

public sealed record ProfileData(string Name, IEnumerable<string> ExecutableNames);

/// <param name="LastUpdateCheckUtc">When GitHub was last asked for the latest release.</param>
/// <param name="LatestReleaseVersion">What it said, reused until the next check.</param>
public sealed record AppSettings(string Theme, DateTime? LastUpdateCheckUtc, string? LatestReleaseVersion, string? LatestReleaseUrl)
{
    public static AppSettings Default { get; } = new("Dark", null, null, null);
}
