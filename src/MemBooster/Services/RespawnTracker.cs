using System.IO;
using System.Text.Json;

namespace MemBooster.Services;

/// <summary>
/// Remembers apps that come straight back after a boost closes them (Windows shell
/// hosts, apps restarted by a watchdog or scheduled task, and so on).
///
/// An app is tagged when it was closed by a boost and is running again at the
/// post-boost re-scan. Boosts trim tagged apps instead of closing them, so a tag can't
/// be disproved by a later boost; instead each tag expires <see cref="TagLifetime"/>
/// after it was last confirmed. The next boost then closes the app once more and re-tags it only if it
/// comes back again.
/// </summary>
public sealed class RespawnTracker
{
    public static readonly TimeSpan TagLifetime = TimeSpan.FromDays(7);

    private readonly string _path;
    private readonly Dictionary<string, DateTime> _confirmedUtc;

    public RespawnTracker(string appDataDirectory)
    {
        _path = Path.Combine(appDataDirectory, "respawners.json");
        _confirmedUtc = Load(_path, out var legacyFormat);
        if (legacyFormat)
        {
            // Write the dated format straight away. Otherwise every launch would treat the
            // old undated tags as "confirmed now" and they would never expire.
            Save();
        }
    }

    public bool IsRespawner(string exeName)
    {
        return _confirmedUtc.TryGetValue(exeName, out var confirmed)
            && DateTime.UtcNow - confirmed < TagLifetime;
    }

    /// <summary>
    /// Updates tags from one boost. Returns the apps newly tagged by this call.
    /// </summary>
    public IReadOnlyList<string> Update(IEnumerable<string> closedByBoost, ISet<string> runningNow)
    {
        var newlyTagged = new List<string>();
        var changed = false;
        var now = DateTime.UtcNow;

        foreach (var name in closedByBoost.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (runningNow.Contains(name))
            {
                if (!IsRespawner(name))
                {
                    newlyTagged.Add(name);
                }

                _confirmedUtc[name] = now; // (Re)confirm: restarts the expiry clock.
                changed = true;
            }
            else if (_confirmedUtc.Remove(name))
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

    private static Dictionary<string, DateTime> Load(string path, out bool legacyFormat)
    {
        legacyFormat = false;
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(path))
            {
                return result;
            }

            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Current format: { "exe": "2026-11-01T03:42:00Z", ... }
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(property.Name)
                        && property.Value.ValueKind == JsonValueKind.String
                        && property.Value.TryGetDateTime(out var confirmed))
                    {
                        result[property.Name] = confirmed.ToUniversalTime();
                    }
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                // v0.6.19 format: ["exe", ...] with no dates. Treat as confirmed now so
                // existing tags carry over and expire on the normal schedule.
                legacyFormat = true;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var name = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result[name] = DateTime.UtcNow;
                    }
                }
            }
        }
        catch
        {
            // Corrupt or unreadable: start fresh.
        }

        return result;
    }

    private void Save()
    {
        try
        {
            if (_confirmedUtc.Count == 0)
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var ordered = _confirmedUtc
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString("O"));
            File.WriteAllText(_path, JsonSerializer.Serialize(ordered));
        }
        catch
        {
            // Best effort; a failed save only means the tag is relearned next boost.
        }
    }
}
