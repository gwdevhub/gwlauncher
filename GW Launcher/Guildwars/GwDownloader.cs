using GW_Launcher.Guildwars.Utils;

namespace GW_Launcher.Guildwars
{
    public static class GwDownloader
    {
        public static async Task<string> DownloadGwExeAsync(IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            var guildWarsClient = new GuildwarsClient();
            var result = await guildWarsClient.Connect(cancellationToken);
            if (!result.HasValue)
            {
                throw new InvalidOperationException("Failed to connect to ArenaNet servers");
            }

            var (context, manifest) = result.Value;
            using (context)
            {
                var fileStream = await guildWarsClient.GetFileStream(context, manifest.LatestExe, 0, cancellationToken);
                if (fileStream == null)
                {
                    throw new InvalidOperationException("Failed to get download stream");
                }

                string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Gw.exe");
                using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    var buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;
                        progress?.Report((double)totalBytesRead / fileStream.Length);
                    }
                }

                return outputPath;
            }
        }
    }
}
