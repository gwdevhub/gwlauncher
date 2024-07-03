namespace GW_Launcher.Guildwars;

public static class GwDownloader
{
    public static async Task<string> DownloadGwExeAsync(CancellationToken cancellationToken = default)
    {
        var installer = new IntegratedGuildwarsInstaller();
        string destinationPath = Path.Combine(Directory.GetCurrentDirectory(), "GwTemp");
        Directory.CreateDirectory(destinationPath);

        bool result = await installer.InstallGuildwars(destinationPath, cancellationToken);
        if (!result)
        {
            throw new InvalidOperationException("Failed to download and install Guild Wars executable");
        }

        string gwExePath = Path.Combine(destinationPath, "Gw.exe");
        if (!File.Exists(gwExePath))
        {
            throw new FileNotFoundException("Gw.exe not found after installation");
        }

        return gwExePath;
    }

    public static async Task CopyGwExeToAccountPaths(string sourceGwExePath, IEnumerable<string> accountPaths,
        IProgress<double> progress = null, CancellationToken cancellationToken = default)
    {
        int totalAccounts = accountPaths.Count();
        int completedAccounts = 0;

        foreach (string accountPath in accountPaths)
        {
            string destinationPath = Path.Combine(Path.GetDirectoryName(accountPath)!, "Gw.exe");
            File.Copy(sourceGwExePath, destinationPath, true);

            completedAccounts++;
            progress?.Report((double)completedAccounts / totalAccounts);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
