using System.Text.RegularExpressions;
using System.Text.Json;

namespace Trimx.Core.Updates;

/// <summary>
/// Asks the GitHub Releases API whether a newer version exists. Every failure (offline, no release yet,
/// rate limit, odd data) means "no update": checking must never disturb the user.
/// </summary>
public static partial class UpdateChecker
{
    // A release's JSON is a few KB; this only guards against a runaway response.
    private const int MaxJsonBytes = 1024 * 1024;

    public const long MaxZipBytes = 1L << 30;

    [GeneratedRegex(@"^Trimx-.*-win-x64\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex ZipNamePattern();

    [GeneratedRegex(@"^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Pattern();

    /// <param name="http">The client to use.</param>
    /// <param name="latestReleaseUrl">The "latest release" API URL of the repository.</param>
    /// <param name="current">The running version.</param>
    public static async Task<UpdateInfo?> CheckAsync(HttpClient http, Uri latestReleaseUrl, Version current, CancellationToken cancellationToken)
    {
        try
        {
            var json = await GetTextAsync(http, latestReleaseUrl, cancellationToken);
            if (json is null || Parse(json, current) is not { } candidate)
            {
                return null;
            }

            if (candidate.Info.Sha256.Length > 0)
            {
                return candidate.Info;
            }

            // No digest in the release data: fall back to the "<zip>.sha256" file next to the zip.
            if (candidate.HashUrl is null || await GetTextAsync(http, candidate.HashUrl, cancellationToken) is not { } text)
            {
                return null;
            }

            var hash = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return Sha256Pattern().IsMatch(hash) ? candidate.Info with { Sha256 = hash } : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    internal sealed record Candidate(UpdateInfo Info, Uri? HashUrl);

    /// <summary>Reads release JSON. Returns null if it is not a newer, installable release.</summary>
    internal static Candidate? Parse(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || IsTrue(root, "draft")
            || IsTrue(root, "prerelease")
            || !root.TryGetProperty("tag_name", out var tag)
            || ParseVersion(tag.GetString()) is not { } version
            || Normalize(version) <= Normalize(current)
            || !root.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? zip = null;
        var sidecars = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null || !asset.TryGetProperty("browser_download_url", out var u) || !TryHttps(u.GetString(), out var url))
            {
                continue;
            }

            if (ZipNamePattern().IsMatch(name))
            {
                zip ??= asset;
            }
            else if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
            {
                sidecars[name] = url;
            }
        }

        if (zip is not { } zipAsset)
        {
            return null;
        }

        var zipName = zipAsset.GetProperty("name").GetString()!;
        TryHttps(zipAsset.GetProperty("browser_download_url").GetString(), out var zipUrl);
        var size = zipAsset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
        if (size <= 0 || size > MaxZipBytes)
        {
            return null;
        }

        // GitHub reports "sha256:<hex>" for uploaded assets.
        var digest = zipAsset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        var hash = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : "";
        if (!Sha256Pattern().IsMatch(hash))
        {
            hash = "";
        }

        sidecars.TryGetValue(zipName + ".sha256", out var hashUrl);
        return new Candidate(new UpdateInfo(version, zipUrl!, zipName, size, hash), hashUrl);
    }

    /// <summary>Parses "v1.2.3" / "1.2.3-beta" style tags; null if it is not a version.</summary>
    internal static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim().TrimStart('v', 'V');
        var end = text.IndexOfAny(['-', '+']);
        if (end >= 0)
        {
            text = text[..end];
        }

        return Version.TryParse(text, out var version) && version.Build >= 0 ? version : Version.TryParse(text + ".0", out var padded) ? padded : null;
    }

    // 0.1.0 and 0.1.0.0 are the same version, but Version compares them as different.
    internal static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static bool IsTrue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryHttps(string? text, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(text, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps && (uri = parsed) is not null;
    }

    private static async Task<string?> GetTextAsync(HttpClient http, Uri url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxJsonBytes)
            {
                return null;
            }
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
