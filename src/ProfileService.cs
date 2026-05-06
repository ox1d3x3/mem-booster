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
                new XAttribute("version", "0.4"),
                new XAttribute("name", string.IsNullOrWhiteSpace(profile.Name) ? "Gaming Boost" : profile.Name),
                new XAttribute("createdUtc", DateTime.UtcNow.ToString("O")),
                new XAttribute("author", "Ox1d3x3"),
                new XAttribute("github", "https://github.com/ox1d3x3"),
                new XElement(ProcessesName,
                    names.Select(name => new XElement(ProcessName,
                        new XAttribute("executable", name))))));

        document.Save(path);
    }
}

public sealed record ProfileData(string Name, IEnumerable<string> ExecutableNames);
