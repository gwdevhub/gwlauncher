using Daybreak.Models.Progress;
using Daybreak.Services.ExecutableManagement;
using Daybreak.Services.Guildwars.Models;
using Daybreak.Services.Guildwars.Utils;
using Daybreak.Services.Notifications;
using Microsoft.Extensions.Logging;
using System;
using System.Core.Extensions;
using System.Diagnostics;
using System.Extensions;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Daybreak.Services.Guildwars;
internal sealed class IntegratedGuildwarsInstaller : IGuildwarsInstaller
{
    private const string ExeName = "Gw.exe";
    private const string TempExeName = "Gw.exe.temp";

    private readonly IGuildWarsExecutableManager guildWarsExecutableManager;
    private readonly INotificationService notificationService;
    private readonly ILogger<IntegratedGuildwarsInstaller> logger;

    public IntegratedGuildwarsInstaller(
        IGuildWarsExecutableManager guildWarsExecutableManager,
        INotificationService notificationService,
        ILogger<IntegratedGuildwarsInstaller> logger)
    {
        this.guildWarsExecutableManager = guildWarsExecutableManager.ThrowIfNull();
        this.notificationService = notificationService.ThrowIfNull();
        this.logger = logger.ThrowIfNull();
    }

    public async Task<bool> InstallGuildwars(string destinationPath, GuildwarsInstallationStatus installationStatus, CancellationToken cancellationToken)
    {
        return await new TaskFactory().StartNew(_ => {
            return this.InstallGuildwarsInternal(destinationPath, installationStatus, cancellationToken);
        }, TaskCreationOptions.LongRunning, cancellationToken).Unwrap();
    }

    private async Task<bool> InstallGuildwarsInternal(string destinationPath, GuildwarsInstallationStatus installationStatus, CancellationToken cancellationToken)
    {
        var scopedLogger = this.logger.CreateScopedLogger(nameof(this.InstallGuildwarsInternal), destinationPath);
        GuildwarsClientContext? maybeContext = default;
        try
        {
            var tempName = Path.Combine(destinationPath, TempExeName);
            var exeName = Path.Combine(destinationPath, ExeName);

            // Initialize the download client
            var guildWarsClient = new GuildwarsClient();
            var result = await guildWarsClient.Connect(cancellationToken);
            if (!result.HasValue)
            {
                scopedLogger.LogError("Failed to connect to ArenaNet servers");
                installationStatus.CurrentStep = GuildwarsInstallationStatus.FailedDownload;
                return false;
            }

            (var context, var manifest) = result.Value;
            maybeContext = context;
            (var downloadResult, var expectedFinalSize) = await this.DownloadCompressedExecutable(tempName, guildWarsClient, context, manifest, installationStatus, cancellationToken);
            if (!downloadResult)
            {
                scopedLogger.LogError("Failed to download compressed executable");
                installationStatus.CurrentStep = GuildwarsInstallationStatus.FailedDownload;
                return false;
            }

            if (!this.DecompressExecutable(tempName, exeName, expectedFinalSize, installationStatus))
            {
                scopedLogger.LogError("Failed to decompress executable");
                installationStatus.CurrentStep = GuildwarsInstallationStatus.FailedDownload;
                return false;
            }

            var filePath = Path.GetFullPath(exeName);
            installationStatus.CurrentStep = GuildwarsInstallationStatus.StartingExecutable;
            await Task.Delay(100, cancellationToken);
            using var process = Process.Start(filePath);
            scopedLogger.LogInformation("Starting executable. Waiting for the process to end before finishing installation");
            while (!process.HasExited)
            {
                await Task.Delay(1000, cancellationToken);
            }

            this.guildWarsExecutableManager.AddExecutable(filePath);
            File.Delete(tempName);
            installationStatus.CurrentStep = GuildwarsInstallationStatus.Finished;
            return true;
        }
        catch (Exception e)
        {
            this.notificationService.NotifyError(
                title: "Download exception",
                description: $"Encountered exception while downloading: {e}");
            installationStatus.CurrentStep = GuildwarsInstallationStatus.FailedDownload;
            this.logger.LogError(e, "Download failed. Encountered exception");
            return false;
        }
        finally
        {
            if (maybeContext.HasValue)
            {
                maybeContext.Value.Dispose();
            }
        }
    }

    private async Task<(bool Success, int ExpectedSize)> DownloadCompressedExecutable(
        string fileName,
        GuildwarsClient guildWarsClient,
        GuildwarsClientContext context,
        ManifestResponse manifest,
        GuildwarsInstallationStatus installationStatus,
        CancellationToken cancellationToken)
    {
        var scopedLogger = this.logger.CreateScopedLogger(nameof(this.DownloadCompressedExecutable), fileName);
        var maybeStream = await guildWarsClient.GetFileStream(context, manifest.LatestExe, 0, cancellationToken);
        if (maybeStream is null)
        {
            scopedLogger.LogError("Failed to get download stream");
            return (false, -1);
        }

        using var downloadStream = maybeStream;
        using var writeFileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        var expectedFinalSize = downloadStream.SizeDecompressed;
        var buffer = new Memory<byte>(new byte[2048]);
        var readBytes = 0;
        do
        {
            readBytes = await downloadStream.ReadAsync(buffer, cancellationToken);
            await writeFileStream.WriteAsync(buffer, cancellationToken);
            installationStatus.CurrentStep = GuildwarsInstallationStatus.Downloading((double)downloadStream.Position / downloadStream.Length, default);
        } while (readBytes > 0);

        return (true, expectedFinalSize);
    }

    private bool DecompressExecutable(
        string tempName,
        string exeName,
        int expectedFinalSize,
        GuildwarsInstallationStatus installationStatus)
    {
        var scopedLogger = this.logger.CreateScopedLogger(nameof(this.DecompressExecutable), tempName);
        try
        {
            var byteBuffer = new Memory<byte>(new byte[1]);
            using var readFileStream = new FileStream(tempName, FileMode.Open, FileAccess.Read);
            using var finalExeStream = new FileStream(exeName, FileMode.Create, FileAccess.ReadWrite);
            var bitStream = new BitStream(readFileStream);
            bitStream.Consume(4);
            var first4Bits = bitStream.Read(4);
            while (finalExeStream.Length < expectedFinalSize)
            {
                installationStatus.CurrentStep = GuildwarsInstallationStatus.Unpacking((double)finalExeStream.Length / expectedFinalSize, default);
                var litHuffman = HuffmanTable.BuildHuffmanTable(bitStream);
                var distHuffman = HuffmanTable.BuildHuffmanTable(bitStream);
                var blockSize = (bitStream.Read(4) + 1) * 4096;
                for (var i = 0; i < blockSize; i++)
                {
                    if (finalExeStream.Length == expectedFinalSize)
                    {
                        break;
                    }

                    var code = litHuffman.GetNextCode(bitStream);
                    if (code < 0x100)
                    {
                        finalExeStream.WriteByte((byte)code);
                    }
                    else
                    {
                        var blen = Huffman.ExtraBitsLength[code - 256];
                        code = Huffman.Table3[code - 256];
                        if (blen > 0)
                        {
                            code |= bitStream.Read((int)blen);
                        }

                        var backtrackCount = first4Bits + code + 1;
                        code = distHuffman.GetNextCode(bitStream);
                        blen = Huffman.ExtraBitsDistance[code];
                        var backtrack = Huffman.BacktrackTable[code];
                        if (blen > 0)
                        {
                            backtrack |= bitStream.Read((int)blen);
                        }

                        if (backtrack >= finalExeStream.Length)
                        {
                            throw new InvalidOperationException("Failed to decompress executable. backtrack >= finalExeStream.Length");
                        }

                        var src = finalExeStream.Length - (backtrack + 1);
                        for (var j = src; j < src + backtrackCount; j++)
                        {
                            finalExeStream.Seek(j, SeekOrigin.Begin);
                            var b = finalExeStream.ReadByte();
                            finalExeStream.Seek(0, SeekOrigin.End);
                            finalExeStream.WriteByte((byte)b);
                        }
                    }
                }
            }

            return true;
        }
        catch(Exception e)
        {
            scopedLogger.LogError(e, "Encountered exception when decompressing executable");
            this.notificationService.NotifyError(
                title: "Failed to decompress",
                description: $"Encountered exception while decompressing: {e}");
            return false;
        }
    }
}
