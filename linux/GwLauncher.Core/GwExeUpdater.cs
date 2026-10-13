using GW_Launcher.Guildwars;
using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;

namespace GwLauncher.Linux;

// Checks and updates Gw.exe the way the Windows launcher does: ask ArenaNet for the latest build, then compare it with
// the build ID inside the local exe.
public static class GwExeUpdater
{
    private const string ConnectFailure = "Failed to connect to ArenaNet servers";

    public static async Task<bool> IsOutdatedAsync(string gwExePath, CancellationToken cancellationToken = default)
    {
        var (latestFileId, _) = await LatestExeAsync(cancellationToken).ConfigureAwait(false);
        var localFileId = await Task.Run(() => new GuildWarsExecutableParser(gwExePath).GetFileId(), cancellationToken).ConfigureAwait(false);

        // 0 means the build-ID signature wasn't found, which is what an exe too old to match the current pattern gives.
        return localFileId != latestFileId;
    }

    // Downloads the latest Gw.exe, then writes it over gwExePath. Writes through symlinks, so a Windows-drive copy is updated in place.
    public static async Task UpdateAsync(string gwExePath, IProgress<ProgressUpdate>? progress = null, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(AppPaths.DataDir, "GwTemp");
        Directory.CreateDirectory(workDir);
        var compressed = Path.Combine(workDir, "Gw.exe.compressed");
        var decompressed = Path.Combine(workDir, "Gw.exe");

        try
        {
            var expectedSize = await DownloadAsync(compressed, progress, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ProgressUpdate(null, "Decompressing Gw.exe"));
            await Task.Run(() => GuildwarsExeDecoder.Decompress(compressed, decompressed, expectedSize,
                fraction => progress?.Report(new ProgressUpdate(0.5 + 0.5 * fraction, "Decompressing Gw.exe"))), cancellationToken).ConfigureAwait(false);

            progress?.Report(new ProgressUpdate(null, "Copying Gw.exe to the account's folder"));
            File.Copy(decompressed, gwExePath, overwrite: true);
        }
        finally
        {
            File.Delete(compressed);
            File.Delete(decompressed);
        }
    }

private static async Task<(int FileId, int SizeDecompressed)> LatestExeAsync(CancellationToken cancellationToken)
    {
        var (response, error) = await GuildwarsLatestExe.GetAsync(cancellationToken).ConfigureAwait(false);
        if (response == null)
        {
            throw new InvalidOperationException(error);
        }

        return (response.Value.FileId, response.Value.SizeDecompressed);
    }

private static async Task<int> DownloadAsync(string destination, IProgress<ProgressUpdate>? progress, CancellationToken cancellationToken)
    {
        var client = new GuildwarsClient();
        var connected = await client.Connect(cancellationToken).ConfigureAwait(false);
        if (!connected.HasValue)
        {
            throw new InvalidOperationException(ConnectFailure);
        }

        var (context, manifest) = connected.Value;
        using (context)
        {
            var size = await GuildwarsExeDownloader.DownloadCompressedAsync(client, context, manifest, destination,
                (received, total) => progress?.Report(new ProgressUpdate(
                    total > 0 ? (double)received / total : null,
                    $"Downloading Gw.exe: {received / 1048576.0:0.0} of {total / 1048576.0:0.0} MB")),
                cancellationToken).ConfigureAwait(false);
            return size ?? throw new InvalidOperationException("ArenaNet didn't offer the latest Gw.exe for download.");
        }
    }
}
