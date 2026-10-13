using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;

namespace GW_Launcher.Guildwars;

public static class GwDownloader
{
private static async Task<(string? filePath, string? Error)> DownloadGwExeAsync(IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken = default)
    {
        var installer = new IntegratedGuildwarsInstaller();
        string destinationPath = Path.Combine(AppContext.BaseDirectory, "GwTemp");
        Directory.CreateDirectory(destinationPath);

        var result = await installer.InstallGuildwars(destinationPath, progress, cancellationToken);
        if(result.Error != null)
        {
            return (null, result.Error);
        }

        string gwExePath = result.filePath!;
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
        string sourceGwExePath = Path.Combine(AppContext.BaseDirectory, "GwTemp");
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

    public static async Task UpdateClients(IEnumerable<Account> accountsToUpdate, IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken = default)
    {
        try
        {
            await DownloadGwExeAsync(progress, cancellationToken);

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
