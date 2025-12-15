using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;

namespace GW_Launcher.Guildwars;

public static class GwDownloader
{
	public static async Task<(FileResponse? Response, string Error)> GetLatestGwExeInfoAsync(CancellationToken cancellationToken = default)
	{
		var guildWarsClient = new GuildwarsClient();
		var result = await guildWarsClient.Connect(cancellationToken);
		if (!result.HasValue)
		{
			return (null, "Failed to connect to ArenaNet servers");
		}

		var (context, manifest) = result.Value;

		try
		{
			await guildWarsClient.Send(new FileRequest
			{
				Field1 = 0x3F2,
				Field2 = 0xC,
				FileId = manifest.LatestExe,
				Version = 0
			}, context, cancellationToken);

			var metadata = await guildWarsClient.ReceiveWait<FileMetadataResponse>(context, cancellationToken);
			if (metadata.Field1 == 0x4F2)
			{
				return (null, "Error 0x4F2: Could not find file");
			}
			else if (metadata.Field1 != 0x5F2)
			{
				return (null, $"Error: Unexpected field response; expected 0x5F2, got 0x{metadata.Field1:X}");
			}

			var response = await guildWarsClient.ReceiveWait<FileResponse>(context, cancellationToken);
			return (response, string.Empty);
		}
		finally
		{
			context.Socket?.Dispose();
		}
	}

    private static async Task<(string? filePath, string? Error)> DownloadGwExeAsync(IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken = default)
    {
        var installer = new IntegratedGuildwarsInstaller();
        string destinationPath = Path.Combine(Directory.GetCurrentDirectory(), "GwTemp");
        Directory.CreateDirectory(destinationPath);

        var result = await installer.InstallGuildwars(destinationPath, progress, cancellationToken);
        if(result.Error != null)
        {
            return (null, result.Error);
        }

        string gwExePath = result.filePath;
        if (!File.Exists(gwExePath))
        {
			return (null, "Gw.exe not found after installation");
        }

        return (gwExePath, null);
    }

    private static void CopyGwExeToAccountPaths(IEnumerable<string> accountPaths,
        IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        int totalAccounts = accountPaths.Count();
        int completedAccounts = 0;
        string sourceGwExePath = Path.Combine(Directory.GetCurrentDirectory(), "GwTemp");
        string gwExePath = Path.Combine(sourceGwExePath, "Gw.exe");
        Directory.CreateDirectory(sourceGwExePath);

        foreach (var accountPath in accountPaths)
        {
            File.Copy(gwExePath, accountPath, true);

            completedAccounts++;
            progress?.Report((double)completedAccounts / totalAccounts);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public static async Task UpdateClients(IEnumerable<Account>? accountsToUpdate, IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken = default)
    {
        try
        {
            await DownloadGwExeAsync(progress, cancellationToken);

            accountsToUpdate ??= Program.accounts;

            var uniquePaths = accountsToUpdate.Select(a => a.gwpath).Distinct().ToList();
            progress.Report(("Copying Gw.exe to client paths", 0.9));
            CopyGwExeToAccountPaths(uniquePaths, new Progress<double>(p => progress.Report(("Copying Gw.exe to client paths", 0.9 + p * 0.1))), cancellationToken);

            progress.Report(("Update completed", 1));
        }
        catch (Exception ex)
        {
            // Handle or log the exception as needed
            throw new Exception("Failed to update clients", ex);
        }
    }
}
