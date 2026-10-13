using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GwLauncher.Wof;

namespace GwLauncher.Resolve;

public enum ResolveMode
{
    // Moves the compressed original into a "compressed" folder beside it, and puts the plain copy under the original name.
    DecompressNextToOriginal,

    // Writes the copy beside the file, verifies it, then swaps it in for the compressed file.
    ReplaceOriginal
}

public sealed record ResolveProgress(double Fraction, string Detail);

// Decompresses one Windows-compacted (WOF) file. Returns the path of the plain file, or "" if the user cancelled.
// Reading the raw NTFS volume needs root, so ntfsinfo and ntfscat run through pkexec (one password prompt per run).
public static class WofResolver
{
    private const string StreamMarker = "@@WOFSTREAM@@";
    private const uint WofTag = 0x80000017;
    private const int TableEntrySize = 4;

    public static async Task<string> ResolveAsync(
        string compressedPath,
        ResolveMode mode,
        IProgress<ResolveProgress>? progress,
        CancellationToken ct)
    {
        var algorithm = WofAlgorithm(compressedPath)
            ?? throw new InvalidOperationException($"'{compressedPath}' is not a Windows-compacted file.");
        var (device, relativePath) = VolumeFor(compressedPath);
        var workPath = compressedPath + ".decompressing";
        if (mode == ResolveMode.DecompressNextToOriginal && File.Exists(KeptOriginalPath(compressedPath)))
        {
            throw new IOException($"'{KeptOriginalPath(compressedPath)}' already exists. Move it away before decompressing again.");
        }

        Process? pkexec = null;
        // The privileged read runs as root, so it can't be signalled from here. Closing our end of its pipe
        // makes its next write fail, and it exits on its own.
        using var registration = ct.Register(() => CloseOutput(pkexec));
        try
        {
            pkexec = StartPrivilegedRead(device, relativePath);
            var info = ReadVolumeInfo(pkexec);
            ct.ThrowIfCancellationRequested();

            var chunkSize = ChunkSize(algorithm);
            CheckFreeSpace(compressedPath, info.UncompressedSize);

            progress?.Report(new ResolveProgress(0, "Decompressing…"));
            await Task.Run(() => DecodeStream(pkexec.StandardOutput.BaseStream, info, algorithm, chunkSize, workPath, progress, ct), ct);

            if (mode == ResolveMode.ReplaceOriginal)
            {
                // The verified copy stays beside the original until the original is gone, so a failure can't lose it.
                var stagedPath = DecompressedName(compressedPath);
                File.Move(workPath, stagedPath, overwrite: true);
                File.Delete(compressedPath);
                File.Move(stagedPath, compressedPath, overwrite: true);
                progress?.Report(new ResolveProgress(1, "Done."));
                return compressedPath;
            }

            // Keep mode: the compressed original moves into a "compressed" folder beside it, and the plain copy takes its
            // name, so the game finds the file where it expects it.
            var keptPath = KeptOriginalPath(compressedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(keptPath)!);
            File.Move(compressedPath, keptPath);
            try
            {
                File.Move(workPath, compressedPath);
            }
            catch
            {
                // Put the original back, so the file is never left missing.
                File.Move(keptPath, compressedPath);
                throw;
            }

            progress?.Report(new ResolveProgress(1, "Done."));
            return compressedPath;
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            Cleanup(workPath);
            return "";
        }
        catch (Exception)
        {
            Cleanup(workPath);
            throw;
        }
        finally
        {
            ReleaseProcess(pkexec);
        }
    }

    // The stream is read sequentially, straight from the privileged process, so no temporary copy is written.
    private static void DecodeStream(
        Stream stream, VolumeInfo info, int algorithm, int chunkSize,
        string workPath, IProgress<ResolveProgress>? progress, CancellationToken ct)
    {
        var chunkCount = (info.UncompressedSize + chunkSize - 1) / chunkSize;
        var tableBytes = (chunkCount - 1) * TableEntrySize;

        var table = new byte[tableBytes];
        stream.ReadExactly(table);
        var ends = new long[chunkCount];
        for (var i = 0; i < chunkCount - 1; i++)
        {
            ends[i] = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(i * TableEntrySize, 4));
        }

        ends[chunkCount - 1] = info.StreamSize - tableBytes;

        using var output = new FileStream(workPath, FileMode.Create, FileAccess.Write, FileShare.None);
        long start = 0;
        for (var i = 0; i < chunkCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var expect = i < chunkCount - 1 ? chunkSize : (int)(info.UncompressedSize - (chunkCount - 1) * (long)chunkSize);
            var size = (int)(ends[i] - start);
            start = ends[i];

            if (size == 0)
            {
                output.Write(new byte[expect]);
            }
            else
            {
                var raw = new byte[size];
                stream.ReadExactly(raw);
                if (size == expect)
                {
                    output.Write(raw);
                }
                else
                {
                    output.Write(Decode(raw, expect, algorithm, i));
                }
            }

            if (i % 64 == 0 || i == chunkCount - 1)
            {
                var done = (double)i / chunkCount;
                progress?.Report(new ResolveProgress(done, $"{(long)(i + 1) * chunkSize / 1048576} of {info.UncompressedSize / 1048576} MB"));
            }
        }
    }

    private static byte[] Decode(byte[] raw, int expect, int algorithm, int chunk)
    {
        try
        {
            if (algorithm == 1)
            {
                var decoded = LzxWimDecoder.Decode(raw, expect, out var bitsConsumed);
                // LZX must use its chunk to within padding; a larger gap means the chunk was misread.
                var unused = (raw.Length + (raw.Length & 1)) * 8L - bitsConsumed;
                if (unused < 0 || unused > 16)
                {
                    throw new InvalidDataException($"Chunk {chunk} did not decode cleanly.");
                }

                return decoded;
            }

            return XpressDecoder.Decode(raw, expect, out _);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            throw new InvalidDataException($"Chunk {chunk} could not be decompressed: {ex.Message}", ex);
        }
    }

    private static Process StartPrivilegedRead(string device, string relativePath)
    {
        var quotedPath = Quote(relativePath);
        var quotedDevice = Quote(device);
        // The volume info is printed first, then a marker, then the raw stream. "exec" keeps ntfscat as the process that gets killed on cancel.
        var script =
            $"INFO=$(ntfsinfo -f -F {quotedPath} {quotedDevice} 2>/dev/null); " +
            $"printf '%s\\n{StreamMarker}\\n' \"$INFO\"; " +
            "INO=$(printf '%s\\n' \"$INFO\" | sed -n 's/.*Dumping Inode \\([0-9]*\\).*/\\1/p' | head -1); " +
            $"exec ntfscat -f -q -i \"$INO\" -n WofCompressedData -a 0x80 {quotedDevice}";

        var psi = new ProcessStartInfo
        {
            FileName = "pkexec",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("sh");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(script);
        return Process.Start(psi) ?? throw new InvalidOperationException("pkexec could not be started.");
    }

    private sealed record VolumeInfo(long UncompressedSize, long StreamSize, long Inode);

    private static VolumeInfo ReadVolumeInfo(Process process)
    {
        var header = ReadUntilMarker(process.StandardOutput.BaseStream);
        var inode = long.Parse(Regex.Match(header, @"Dumping Inode (\d+)").Groups[1].Value);

        var streamSize = Regex.Match(header, @"'WofCompressedData'[\s\S]*?Data size:\s+(\d+)");
        if (!streamSize.Success)
        {
            throw new InvalidOperationException("The file has no compressed data stream (is it really compacted?).");
        }

        // The unnamed $DATA attribute holds the uncompressed size.
        long? uncompressed = null;
        foreach (var block in header.Split("Dumping attribute $DATA").Skip(1))
        {
            if (block.Contains("Attribute name:"))
            {
                continue;
            }

            var size = Regex.Match(block, @"Data size:\s+(\d+)");
            if (size.Success)
            {
                uncompressed = long.Parse(size.Groups[1].Value);
                break;
            }
        }

        return new VolumeInfo(
            uncompressed ?? throw new InvalidOperationException("Could not read the file's uncompressed size."),
            long.Parse(streamSize.Groups[1].Value),
            inode);
    }

    // Reads the volume info text up to the marker line. Everything after the marker is the raw stream.
    private static string ReadUntilMarker(Stream stream)
    {
        var marker = Encoding.ASCII.GetBytes(StreamMarker + "\n");
        var buffer = new List<byte>();
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            buffer.Add((byte)b);
            if (buffer.Count >= marker.Length && buffer.Skip(buffer.Count - marker.Length).SequenceEqual(marker))
            {
                return Encoding.UTF8.GetString(buffer.Take(buffer.Count - marker.Length).ToArray());
            }
        }

        throw new InvalidOperationException("The privileged read ended before the volume info was printed. Was the password prompt cancelled?");
    }

    private static void CheckFreeSpace(string compressedPath, long uncompressedSize)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(compressedPath))!;
        var free = new DriveInfo(folder).AvailableFreeSpace;
        if (free < uncompressedSize + uncompressedSize / 50)
        {
            throw new IOException($"Not enough free space: about {uncompressedSize / 1048576} MB is needed, {free / 1048576} MB is free.");
        }
    }

    // Where the compressed original goes in keep mode: "<folder>/compressed/<name>".
    public static string KeptOriginalPath(string compressedPath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(compressedPath))!;
        return Path.Combine(folder, "compressed", Path.GetFileName(compressedPath));
    }

    // Name for the staged copy in replace mode: "Gw.exe" becomes "Gw.decompressed.exe"; no extension gets ".decompressed".
    public static string DecompressedName(string compressedPath)
    {
        var folder = Path.GetDirectoryName(compressedPath)!;
        var name = Path.GetFileNameWithoutExtension(compressedPath);
        var extension = Path.GetExtension(compressedPath);
        return Path.Combine(folder, name + ".decompressed" + extension);
    }

    private static int ChunkSize(int algorithm) => algorithm switch
    {
        0 => 4096,
        1 => 32768,
        2 => 8192,
        3 => 16384,
        _ => throw new InvalidOperationException($"Unsupported WOF algorithm {algorithm}.")
    };

    // The volume and the path inside it, from the mount table. ntfsinfo wants the path relative to the volume root.
    private static (string Device, string RelativePath) VolumeFor(string path)
    {
        var full = Path.GetFullPath(path);
        string? bestMount = null, bestDevice = null;
        foreach (var line in File.ReadAllLines("/proc/mounts"))
        {
            var fields = line.Split(' ');
            if (fields.Length < 3)
            {
                continue;
            }

            var mount = fields[1].Replace("\\040", " ");
            if ((full == mount || full.StartsWith(mount.TrimEnd('/') + "/")) && (bestMount == null || mount.Length > bestMount.Length))
            {
                bestMount = mount;
                bestDevice = fields[0];
            }
        }

        if (bestMount == null || bestDevice == null)
        {
            throw new InvalidOperationException($"No mounted volume contains '{path}'.");
        }

        var relative = full[bestMount.TrimEnd('/').Length..].TrimStart('/');
        return (bestDevice, relative);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static void CloseOutput(Process? process)
    {
        try
        {
            process?.StandardOutput.BaseStream.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Already closed.
        }
    }

    private static void ReleaseProcess(Process? process)
    {
        if (process == null)
        {
            return;
        }

        CloseOutput(process);
        try
        {
            // The root child exits after its pipe closes. Waiting keeps the process from being left as a zombie.
            process.WaitForExit(10000);
        }
        catch (InvalidOperationException)
        {
            // Never started, or already reaped.
        }

        process.Dispose();
    }

    private static void Cleanup(string workPath)
    {
        try
        {
            if (File.Exists(workPath))
            {
                File.Delete(workPath);
            }
        }
        catch (IOException)
        {
            // Leave the partial file if it can't be removed; the caller reports the failure.
        }
    }

    // The WOF file-provider algorithm, from the reparse data (readable without root).
    public static int? WofAlgorithm(string path)
    {
        var buffer = new byte[64];
        var length = lgetxattr(path, "system.ntfs_reparse_data", buffer, (nint)buffer.Length);
        if (length < 24 || BinaryPrimitives.ReadUInt32LittleEndian(buffer) != WofTag)
        {
            return null;
        }

        return (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(20, 4));
    }

    [DllImport("libc", SetLastError = true)]
    private static extern nint lgetxattr(string path, string name, byte[] value, nint size);
}
