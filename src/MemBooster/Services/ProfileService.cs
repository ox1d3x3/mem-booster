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


    public string LoadThemePreference()
    {
        if (!File.Exists(SettingsPath))
        {
            return "Dark";
        }

        try
        {
            var document = XDocument.Load(SettingsPath, LoadOptions.None);
            var theme = (string?)document.Root?.Attribute("theme") ?? "Dark";
            return IsValidTheme(theme) ? theme : "Dark";
        }
        catch
        {
            return "Dark";
        }
    }

    public void SaveThemePreference(string theme)
    {
        Directory.CreateDirectory(AppDataDirectory);
        var safeTheme = IsValidTheme(theme) ? theme : "Dark";
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("MemBoosterSettings",
                new XAttribute("version", "0.5.22"),
                new XAttribute("theme", safeTheme)));

        document.Save(SettingsPath);
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
                new XAttribute("version", "0.5.22"),
                new XAttribute("name", string.IsNullOrWhiteSpace(profile.Name) ? "Gaming Boost" : profile.Name),
                new XAttribute("createdUtc", DateTime.UtcNow.ToString("O")),
                new XAttribute("author", "Ox1d3x3"),
                new XAttribute("github", "https://github.com/ox1d3x3/mem-booster"),
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

    public bool HasRestoreSession()
    {
        if (!File.Exists(LastRestoreSessionPath))
        {
            return false;
        }

        try
        {
            return LoadRestoreSession().Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<RestoreEntry> LoadRestoreSession()
    {
        if (!File.Exists(LastRestoreSessionPath))
        {
            return Array.Empty<RestoreEntry>();
        }

        var document = XDocument.Load(LastRestoreSessionPath, LoadOptions.None);
        var root = document.Root;
        if (root is null || root.Name != "MemBoosterRestoreSession")
        {
            throw new InvalidDataException("This does not look like a Mem-Booster restore session.");
        }

        return root.Element("Apps")?
            .Elements("App")
            .Select(element => new RestoreEntry(
                SafetyRules.NormaliseProcessName((string?)element.Attribute("executable") ?? string.Empty),
                (string?)element.Attribute("displayName") ?? (string?)element.Attribute("executable") ?? "App",
                (string?)element.Attribute("filePath") ?? string.Empty,
                DateTime.TryParse((string?)element.Attribute("capturedUtc"), out var capturedUtc) ? capturedUtc : DateTime.UtcNow))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ExeName))
            .Where(entry => !SafetyRules.IsBlocked(entry.ExeName))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FilePath))
            .DistinctBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<RestoreEntry>();
    }

    public void SaveRestoreSession(IEnumerable<RestoreEntry> entries)
    {
        Directory.CreateDirectory(AppDataDirectory);

        var safeEntries = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ExeName))
            .Where(entry => !SafetyRules.IsBlocked(entry.ExeName))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FilePath))
            .DistinctBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (safeEntries.Length == 0)
        {
            if (File.Exists(LastRestoreSessionPath))
            {
                File.Delete(LastRestoreSessionPath);
            }
            return;
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("MemBoosterRestoreSession",
                new XAttribute("version", "0.5.22"),
                new XAttribute("createdUtc", DateTime.UtcNow.ToString("O")),
                new XElement("Apps",
                    safeEntries.Select(entry => new XElement("App",
                        new XAttribute("executable", SafetyRules.NormaliseProcessName(entry.ExeName)),
                        new XAttribute("displayName", entry.DisplayName),
                        new XAttribute("filePath", entry.FilePath),
                        new XAttribute("capturedUtc", entry.CapturedUtc.ToString("O")))))));

        document.Save(LastRestoreSessionPath);
    }
}

public sealed record ProfileData(string Name, IEnumerable<string> ExecutableNames);
