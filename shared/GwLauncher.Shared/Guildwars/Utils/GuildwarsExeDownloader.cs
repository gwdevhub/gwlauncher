using GW_Launcher.Guildwars.Models;

namespace GW_Launcher.Guildwars.Utils;

// Streams the compressed Gw.exe to disk. Has no UI so both launchers can call it.
internal static class GuildwarsExeDownloader
{
    // Returns the decompressed size, or null when ArenaNet offers no stream for the latest exe.
    public static async Task<int?> DownloadCompressedAsync(GuildwarsClient client, GuildwarsClientContext context, ManifestResponse manifest,
        string destination, Action<long, long>? onProgress, CancellationToken cancellationToken)
    {
        var stream = await client.GetFileStream(context, manifest.LatestExe, 0, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        await using var source = stream;
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write);
        var buffer = new byte[64 * 1024];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            onProgress?.Invoke(received, source.Length);
        }

        return source.SizeDecompressed;
    }
}
