using Daybreak.Models.Progress;
using Daybreak.Services.Downloads;
using Daybreak.Services.Privilege;
using Daybreak.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Core.Extensions;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Daybreak.Services.Guildwars;
internal sealed class GuildwarsInstaller : IGuildwarsInstaller
{
    private const string GuildwarsDownloadUri = "https://cloudfront.guildwars2.com/client/GwSetup.exe";
    private const string InstallationFileName = "GwSetup.exe";

    private readonly IDownloadService downloadService;
    private readonly IPrivilegeManager privilegeManager;
    private readonly ILogger<GuildwarsInstaller> logger;

    public GuildwarsInstaller(
        IPrivilegeManager privilegeManager,
        IDownloadService downloadService,
        ILogger<GuildwarsInstaller> logger)
    {
        this.privilegeManager = privilegeManager.ThrowIfNull();
        this.downloadService = downloadService.ThrowIfNull();
        this.logger = logger.ThrowIfNull();
    }

    public async Task<bool> InstallGuildwars(string destinationPath, GuildwarsInstallationStatus installationStatus, CancellationToken cancellationToken)
    {
        if (this.privilegeManager.AdminPrivileges is false)
        {
            this.privilegeManager.RequestAdminPrivileges<LauncherView>("Daybreak needs admin privileges to download and install Guildwars");
            return false;
        }

        var exePath = Path.Combine(destinationPath, InstallationFileName);
        if (!File.Exists(exePath))
        {
            if ((await this.DownloadGuildwarsInstaller(exePath, installationStatus)) is false)
            {
                throw new InvalidOperationException("Failed to download executable");
            }
        }

        var installationProcess = Process.Start(exePath);
        while (installationProcess.HasExited is false)
        {
            await Task.Delay(1000);
        }

        installationStatus.CurrentStep = GuildwarsInstallationStatus.Finished;
        this.logger.LogInformation($"Installation finished with status code {installationProcess.ExitCode}");
        return true;
    }

    private Task<bool> DownloadGuildwarsInstaller(string destinationPath, GuildwarsInstallationStatus installationStatus)
    {
        return this.downloadService.DownloadFile(GuildwarsDownloadUri, destinationPath, installationStatus);
    }
}
