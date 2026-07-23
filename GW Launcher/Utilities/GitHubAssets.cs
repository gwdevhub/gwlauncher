using Newtonsoft.Json.Linq;

namespace GW_Launcher.Utilities;

internal sealed record GitHubAsset(string Name, string DownloadUrl, string? Sha256);

internal sealed record GitHubRelease(
    string TagName,
    bool Prerelease,
    bool Draft,
    string HtmlUrl,
    IReadOnlyList<GitHubAsset> Assets);

internal static class GitHubAssets
{
    public static async Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(string owner, string repo)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GWLauncher");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var json = await http.GetStringAsync($"https://api.github.com/repos/{owner}/{repo}/releases");

        var releases = new List<GitHubRelease>();
        foreach (var r in JArray.Parse(json))
        {
            var assets = new List<GitHubAsset>();
            foreach (var a in (JArray?)r["assets"] ?? new JArray())
            {
                assets.Add(new GitHubAsset(
                    (string?)a["name"] ?? "",
                    (string?)a["browser_download_url"] ?? "",
                    ParseSha256((string?)a["digest"])));
            }

            releases.Add(new GitHubRelease(
                (string?)r["tag_name"] ?? "",
                (bool?)r["prerelease"] ?? false,
                (bool?)r["draft"] ?? false,
                (string?)r["html_url"] ?? "",
                assets));
        }

        return releases;
    }

    public static string? ComputeSha256(string path)
    {
        if (!File.Exists(path))
            return null;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    // True when the local file already matches the asset's published sha256. When the release exposes no
    // digest we can't verify, so we keep the existing file rather than re-download it on every launch.
    public static bool IsUpToDate(string localPath, string? expectedSha256)
    {
        var local = ComputeSha256(localPath);
        if (local == null)
            return false;
        return expectedSha256 == null || string.Equals(local, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    // Short, human-readable prefix of a sha256 hex string for display in update prompts.
    public static string? ShortSha(string? sha256, int chars = 8)
    {
        if (string.IsNullOrEmpty(sha256))
            return null;
        return sha256.Length <= chars ? sha256 : sha256[..chars];
    }

    private static string? ParseSha256(string? digest)
    {
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            return null;
        return digest["sha256:".Length..].ToLowerInvariant();
    }
}
