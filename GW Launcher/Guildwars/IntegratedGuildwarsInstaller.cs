using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;
using Microsoft.Win32;

namespace GW_Launcher.Guildwars;
internal sealed class IntegratedGuildwarsInstaller
{
    private const string ExeName = "Gw.exe";
    private const string TempExeName = "Gw.exe.temp";
    
    public async Task<bool> InstallGuildwars(string destinationPath, IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken)
    {
        return await new TaskFactory().StartNew(_ => InstallGuildwarsInternal(destinationPath, progress, cancellationToken), TaskCreationOptions.LongRunning, cancellationToken).Unwrap();
    }

    private async Task<bool> InstallGuildwarsInternal(string destinationPath, IProgress<(string Stage, double Progress)> progress, CancellationToken cancellationToken)
    {
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
                MessageBox.Show("Failed to connect to ArenaNet servers");
                return false;
            }

            var (context, manifest) = result.Value;
            maybeContext = context;
            if (File.Exists(exeName) && FileIdFinder.GetFileId(exeName) == manifest.LatestExe)
            {
                progress.Report(("Exe already downloaded", 0.9));
                return true;
            }
            var (downloadResult, expectedFinalSize) = await DownloadCompressedExecutable(tempName, guildWarsClient, context, manifest, progress, cancellationToken);
            if (!downloadResult)
            {
                MessageBox.Show("Failed to download compressed executable");
                return false;
            }

            if (!DecompressExecutable(tempName, exeName, expectedFinalSize, progress))
            {
                MessageBox.Show("Failed to decompress executable");
                return false;
            }
            File.Delete(tempName);
            return true;
        }
        catch (Exception e)
        {
            MessageBox.Show("Download failed. Encountered exception" + e.Message);
            return false;
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
        var maybeStream = await guildWarsClient.GetFileStream(context, manifest.LatestExe, 0, cancellationToken);
        if (maybeStream is null)
        {
            MessageBox.Show("Failed to get download stream");
            return (false, -1);
        }

        await using var downloadStream = maybeStream;
        var expectedFinalSize = downloadStream.SizeDecompressed;

        await using var writeFileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        var buffer = new Memory<byte>(new byte[2048]);
        var readBytes = 0;
        var totalBytesRead = 0;
        do
        {
            readBytes = await downloadStream.ReadAsync(buffer, cancellationToken);
            await writeFileStream.WriteAsync(buffer.Slice(0, readBytes), cancellationToken);
            totalBytesRead += readBytes;
            progress?.Report(("Downloading compressed executable", (double)totalBytesRead / downloadStream.Length * 0.45));
        } while (readBytes > 0);

        progress?.Report(("Downloaded compressed executable", 0.45));
        return (true, expectedFinalSize);
    }

    private bool DecompressExecutable(
        string tempName,
        string exeName,
        int expectedFinalSize,
        IProgress<(string Stage, double Progress)> progress)
    {
        try
        {
            using var readFileStream = new FileStream(tempName, FileMode.Open, FileAccess.Read);
            using var finalExeStream = new FileStream(exeName, FileMode.Create, FileAccess.ReadWrite);
            var bitStream = new BitStream(readFileStream);
            bitStream.Consume(4);
            var first4Bits = bitStream.Read(4);
            progress?.Report(("Decompressing downloaded executable", 0.45));
            while (finalExeStream.Length < expectedFinalSize)
            {
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

                    // Report progress
                    if (i % 1000 == 0) // Update progress every 1000 iterations to avoid excessive updates
                    {
                        double progressPercentage = 0.45 + ((double)finalExeStream.Length / expectedFinalSize) * 0.45;
                        progress?.Report(("Decompressing downloaded executable", progressPercentage));
                    }
                }
            }

            // Ensure 90% progress is reported at the end of decompression
            progress?.Report(("Decompressed downloaded executable", 0.9));

            return true;
        }
        catch(Exception e)
        {
            MessageBox.Show($"Encountered exception while decompressing: {e}");
            return false;
        }
    }
}
