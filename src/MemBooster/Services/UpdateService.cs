using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MemBooster.Services;

public sealed class UpdateService
{
    public const string RepositoryUrl = "https://github.com/ox1d3x3/mem-booster";

    private const string LatestReleaseApiUrl = "https://api.github.com/repos/ox1d3x3/mem-booster/releases/latest";
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    public async Task<UpdateCheckResult> CheckLatestAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.UserAgent.ParseAdd("Mem-Booster/0.5.26");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new UpdateCheckResult(
                LatestVersion: string.Empty,
                ReleaseName: "No published release found",
                ReleaseUrl: RepositoryUrl + "/releases",
                IsUpdateAvailable: false);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var tagName = GetString(root, "tag_name");
        var releaseName = GetString(root, "name");
        var releaseUrl = GetString(root, "html_url");

        var latestVersion = NormaliseVersion(tagName);
        var installedVersion = NormaliseVersion(currentVersion);
        var hasNewerVersion = CompareVersionStrings(latestVersion, installedVersion) > 0;

        return new UpdateCheckResult(
            string.IsNullOrWhiteSpace(latestVersion) ? tagName : latestVersion,
            releaseName,
            releaseUrl,
            hasNewerVersion);
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string NormaliseVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = Regex.Match(value, @"\d+(?:\.\d+){0,3}");
        return match.Success ? match.Value : value.Trim().TrimStart('v', 'V');
    }

    private static int CompareVersionStrings(string latest, string current)
    {
        if (!TryParseVersion(latest, out var latestVersion) || !TryParseVersion(current, out var currentVersion))
        {
            return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase);
        }

        return latestVersion.CompareTo(currentVersion);
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        version = new Version(0, 0, 0, 0);

        var parts = NormaliseVersion(value)
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(4)
            .ToList();

        if (parts.Count == 0 || parts.Any(part => !int.TryParse(part, out _)))
        {
            return false;
        }

        while (parts.Count < 4)
        {
            parts.Add("0");
        }

        version = new Version(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]),
            int.Parse(parts[3]));
        return true;
    }
}

public sealed record UpdateCheckResult(
    string LatestVersion,
    string ReleaseName,
    string ReleaseUrl,
    bool IsUpdateAvailable);
