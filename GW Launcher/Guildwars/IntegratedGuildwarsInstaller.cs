using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;
using Microsoft.Win32;

namespace GW_Launcher.Guildwars;
internal sealed class IntegratedGuildwarsInstaller
{
    private const string ExeName = "Gw.exe";
    private const string TempExeName = "Gw.exe.temp";
    
    public async Task<(string? filePath, string? Error)> InstallGuildwars(string destinationPath, IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken)
    {
        return await new TaskFactory().StartNew(_ => InstallGuildwarsInternal(destinationPath, progress, cancellationToken), TaskCreationOptions.LongRunning, cancellationToken).Unwrap();
    }

	private async Task<(string? filePath, string? Error)> InstallGuildwarsInternal(string destinationPath, IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken)
	{
		GuildwarsClientContext? maybeContext = default;
		try
		{
			var tempName = Path.Combine(destinationPath, TempExeName);
			var exeName = Path.Combine(destinationPath, ExeName);

			// Get latest exe info
			var (fileResponse, error) = await GuildwarsLatestExe.GetAsync(cancellationToken);
			if (fileResponse == null || !string.IsNullOrEmpty(error))
			{
				return (null, error ?? "Failed to get latest exe info");
			}

			var expectedSize = fileResponse.Value.SizeDecompressed;

			// Check if exe already exists with correct size
			if (File.Exists(exeName))
			{
                try
                {
					var fileInfo = new FileInfo(exeName);
					if (fileInfo.Length == expectedSize)
                    {
						var parser = new GuildWarsExecutableParser(exeName);
						var currentFileId = parser.GetFileId();
						if (currentFileId == fileResponse.Value.FileId)
						{
							progress.Report(("Exe already downloaded", 0.9));
							return (destinationPath, null);
						}
					}
			
                }
                catch (Exception)
                {
                    // Silent fail, just re-download
                }
			}

			// Initialize the download client
			var guildWarsClient = new GuildwarsClient();
			var result = await guildWarsClient.Connect(cancellationToken);
			if (!result.HasValue)
			{
				return (null, "Failed to connect to ArenaNet servers");
			}
			var (context, manifest) = result.Value;
			maybeContext = context;

			var (downloadResult, expectedFinalSize) = await DownloadCompressedExecutable(tempName, guildWarsClient, context, manifest, progress, cancellationToken);
			if (!downloadResult)
			{
				return (null, "Failed to download compressed executable");
			}
			if (!DecompressExecutable(tempName, exeName, expectedFinalSize, progress))
			{
				return (null, "Failed to decompress executable");
			}
			File.Delete(tempName);
			return (destinationPath, null);
		}
		catch (Exception e)
		{
			return (null, "Download failed. Encountered exception: " + e.Message);
		}
		finally
		{
			maybeContext?.Dispose();
		}
	}

private async Task<(bool Success, int ExpectedSize)> DownloadCompressedExecutable(
        string fileName,
        GuildwarsClient guildWarsClient,
        GuildwarsClientContext context,
        ManifestResponse manifest,
        IProgress<(string Stage, double Progress)> progress,
        CancellationToken cancellationToken)
    {
        var expectedFinalSize = await GuildwarsExeDownloader.DownloadCompressedAsync(guildWarsClient, context, manifest, fileName,
            (received, total) => progress?.Report(("Downloading compressed executable", (double)received / total * 0.45)),
            cancellationToken);
        if (expectedFinalSize is null)
        {
            MessageBox.Show("Failed to get download stream");
            return (false, -1);
        }

        progress?.Report(("Downloaded compressed executable", 0.45));
        return (true, expectedFinalSize.Value);
    }

private bool DecompressExecutable(
        string tempName,
        string exeName,
        int expectedFinalSize,
        IProgress<(string Stage, double Progress)> progress)
    {
        try
        {
            progress?.Report(("Decompressing downloaded executable", 0.45));
            GuildwarsExeDecoder.Decompress(tempName, exeName, expectedFinalSize,
                fraction => progress?.Report(("Decompressing downloaded executable", 0.45 + fraction * 0.45)));
            progress?.Report(("Decompressed downloaded executable", 0.9));
            return true;
        }
        catch (Exception e)
        {
            MessageBox.Show($"Encountered exception while decompressing: {e}");
            return false;
        }
    }
}
