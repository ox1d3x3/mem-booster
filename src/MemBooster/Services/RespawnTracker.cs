using System.IO;
using System.Text.Json;

namespace MemBooster.Services;

/// <summary>
/// Remembers apps that come straight back after a boost closes them (Windows shell
/// hosts, apps restarted by a watchdog or scheduled task, and so on).
///
/// An app is tagged when it was closed by a boost and is running again at the
/// post-boost re-scan. The tag is removed the next time a boost closes it and it stays
/// closed, so the list corrects itself.
/// </summary>
public sealed class RespawnTracker
{
    private readonly string _path;
    private readonly HashSet<string> _respawners;

    public RespawnTracker(string appDataDirectory)
    {
        _path = Path.Combine(appDataDirectory, "respawners.json");
        _respawners = Load(_path);
    }

    public bool IsRespawner(string exeName) => _respawners.Contains(exeName);

    /// <summary>
    /// Updates tags from one boost. Returns the apps newly tagged by this call.
    /// </summary>
    public IReadOnlyList<string> Update(IEnumerable<string> closedByBoost, ISet<string> runningNow)
    {
        var newlyTagged = new List<string>();
        var changed = false;

        foreach (var name in closedByBoost.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (runningNow.Contains(name))
            {
                if (_respawners.Add(name))
                {
                    newlyTagged.Add(name);
                    changed = true;
                }
            }
            else if (_respawners.Remove(name))
            {
                changed = true; // It stayed closed this time.
            }
        }

        if (changed)
        {
            Save();
        }

        return newlyTagged;
    }

    private static HashSet<string> Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var names = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
                if (names is not null)
                {
                    return new HashSet<string>(names.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            // Corrupt or unreadable: start fresh.
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            if (_respawners.Count == 0)
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_respawners.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()));
        }
        catch
        {
            // Best effort; a failed save only means the tag is relearned next boost.
        }
    }
}
