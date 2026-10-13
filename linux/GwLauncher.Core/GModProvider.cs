using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GwLauncher.Linux;

// Raised when gMod.dll can't be downloaded or found. The message says why and what to do, so it can be shown as-is.
public sealed class GModUnavailableException : InvalidOperationException
{
    public GModUnavailableException(string message) : base(message)
    {
    }
}

// Gets gMod.dll the way the Windows launcher does: the newest non-prerelease gwdevhub/gMod release, saved next to the
// launcher's own executable, which is where texmods look for it.
public static class GModProvider
{
    private const string AssetName = "gMod.dll";
    private const string ReleasesUrl = "https://api.github.com/repos/gwdevhub/gMod/releases";
    private static readonly HttpClient Http = CreateClient();

    public static string LocalPath => Path.Combine(AppContext.BaseDirectory, AssetName);

    // Returns the local copy's path. Downloads it when it's missing or broken, or when it differs from the release's
    // SHA-256 digest. A release without a digest keeps a valid copy, so it isn't downloaded again for no reason.
    public static async Task<string> RefreshAsync(IProgress<ProgressUpdate>? progress = null, CancellationToken cancellationToken = default)
    {
        var asset = await LatestAssetAsync(cancellationToken).ConfigureAwait(false);
        if (asset == null)
        {
            throw Unavailable($"The gwdevhub/gMod releases on GitHub don't include {AssetName}.");
        }

        if (IsValid(LocalPath) && (asset.Sha256 == null || Sha256Of(LocalPath).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)))
        {
            return LocalPath;
        }

        await DownloadAsync(asset, progress, cancellationToken).ConfigureAwait(false);
        return LocalPath;
    }

    // A Windows x86 DLL: MZ header, then a PE header for i386. An error page or truncated file counts as missing.
    public static bool IsValid(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt16() != 0x5A4D)
            {
                return false;
            }

            stream.Position = 0x3C;
            stream.Position = reader.ReadInt32();
            if (reader.ReadUInt32() != 0x00004550)
            {
                return false;
            }

            return reader.ReadUInt16() == 0x14C;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GWLauncher");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static async Task<AssetInfo?> LatestAssetAsync(CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await Http.GetStringAsync(new Uri(ReleasesUrl), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw NetworkFailure(ex, "looking for");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable("GitHub didn't answer in time while looking for gMod.dll. Check the internet connection and try again.");
        }

        try
        {
            foreach (var release in JArray.Parse(json))
            {
                if ((bool?)release["prerelease"] == true || (bool?)release["draft"] == true)
                {
                    continue;
                }

                foreach (var asset in (JArray?)release["assets"] ?? new JArray())
                {
                    if ((string?)asset["name"] == AssetName)
                    {
                        return new AssetInfo((string?)asset["browser_download_url"] ?? "", ParseSha256((string?)asset["digest"]));
                    }
                }
            }
        }
        catch (JsonException)
        {
            throw Unavailable("GitHub sent a reply that wasn't a release list, so gMod.dll couldn't be found.");
        }

        return null;
    }

    private static async Task DownloadAsync(AssetInfo asset, IProgress<ProgressUpdate>? progress, CancellationToken cancellationToken)
    {
        if (asset.Url == "")
        {
            throw Unavailable("The gMod.dll release asset has no download address.");
        }

        var temp = LocalPath + ".download";
        try
        {
            Directory.CreateDirectory(AppContext.BaseDirectory);

            using var response = await Http.GetAsync(new Uri(asset.Url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = File.Create(temp))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress?.Report(new ProgressUpdate(
                        total is > 0 ? (double)received / total.Value : null,
                        total is > 0
                            ? $"{Megabytes(received)} of {Megabytes(total.Value)} MB"
                            : $"{Megabytes(received)} MB downloaded"));
                }
            }

            if (asset.Sha256 != null && !Sha256Of(temp).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw Unavailable("The downloaded gMod.dll didn't match its published SHA-256 digest, so it was discarded. Try again.");
            }

            if (!IsValid(temp))
            {
                throw Unavailable("The downloaded gMod.dll isn't a 32-bit Windows DLL, so it was discarded.");
            }

            File.Move(temp, LocalPath, overwrite: true);
        }
        catch (HttpRequestException ex)
        {
            throw NetworkFailure(ex, "downloading");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable("GitHub didn't finish sending gMod.dll in time. Check the internet connection and try again.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Unavailable($"Couldn't save gMod.dll to {AppContext.BaseDirectory} ({ex.Message}). Check that folder's permissions, or copy gMod.dll there yourself.");
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static GModUnavailableException NetworkFailure(HttpRequestException ex, string action)
    {
        if (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            return Unavailable($"GitHub refused the request while {action} gMod.dll (HTTP {(int)ex.StatusCode}), most likely its rate limit. Try again later, or copy gMod.dll next to the launcher yourself.");
        }

        return Unavailable($"Couldn't reach GitHub while {action} gMod.dll ({ex.Message}). Check the internet connection, or copy gMod.dll next to the launcher yourself.");
    }

    private static string Megabytes(long bytes) => (bytes / 1048576.0).ToString("0.0");

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string? ParseSha256(string? digest)
    {
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return digest["sha256:".Length..].ToLowerInvariant();
    }

    private static GModUnavailableException Unavailable(string message) => new(message);

    private sealed record AssetInfo(string Url, string? Sha256);
}
