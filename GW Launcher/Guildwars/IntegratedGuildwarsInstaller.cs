using GW_Launcher.Guildwars.Models;
using GW_Launcher.Guildwars.Utils;

namespace GW_Launcher.Guildwars;
internal sealed class IntegratedGuildwarsInstaller
{
    private const string ExeName = "Gw.exe";
    private const string TempExeName = "Gw.exe.temp";
    
    public async Task<bool> InstallGuildwars(string destinationPath, CancellationToken cancellationToken)
    {
        return await new TaskFactory().StartNew(_ => {
            return this.InstallGuildwarsInternal(destinationPath, cancellationToken);
        }, TaskCreationOptions.LongRunning, cancellationToken).Unwrap();
    }

    private async Task<bool> InstallGuildwarsInternal(string destinationPath, CancellationToken cancellationToken)
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

            (var context, var manifest) = result.Value;
            maybeContext = context;
            var (downloadResult, expectedFinalSize) = await DownloadCompressedExecutable(tempName, guildWarsClient, context, manifest, cancellationToken);
            if (!downloadResult)
            {
                MessageBox.Show("Failed to download compressed executable");
                return false;
            }

            if (!this.DecompressExecutable(tempName, exeName, expectedFinalSize))
            {
                MessageBox.Show("Failed to decompress executable");
                return false;
            }

            var filePath = Path.GetFullPath(exeName);
            await Task.Delay(100, cancellationToken);
            using var process = Process.Start(filePath);
            MessageBox.Show("Starting executable. Waiting for the process to end before finishing installation");
            while (!process.HasExited)
            {
                await Task.Delay(1000, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var maybeStream = await guildWarsClient.GetFileStream(context, manifest.LatestExe, 0, cancellationToken);
        if (maybeStream is null)
        {
            MessageBox.Show("Failed to get download stream");
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
        } while (readBytes > 0);

        return (true, expectedFinalSize);
    }

    private bool DecompressExecutable(
        string tempName,
        string exeName,
        int expectedFinalSize)
    {
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
            MessageBox.Show($"Encountered exception while decompressing: {e}");
            return false;
        }
    }
}
