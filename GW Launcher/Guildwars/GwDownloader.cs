using GW_Launcher.Guildwars.Utils;

namespace GW_Launcher.Guildwars;

public static class GwDownloader
{
    public static async Task<int> GetLatestGwExeFileIdAsync(CancellationToken cancellationToken = default)
    {
        // Initialize the download client
        var guildWarsClient = new GuildwarsClient();
        var result = await guildWarsClient.Connect(cancellationToken);
        if (!result.HasValue)
        {
            MessageBox.Show("Failed to connect to ArenaNet servers");
            return 0;
        }
        var (_, manifest) = result.Value;
        return manifest.LatestExe;
    }

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

    public static async Task CopyGwExeToAccountPaths(IEnumerable<string> accountPaths,
        IProgress<double> progress = null, CancellationToken cancellationToken = default)
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
}
