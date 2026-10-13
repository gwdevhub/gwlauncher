using GwLauncher.Wof;

// Usage: wofdecode <compressed-stream> <uncompressed-size> <output> [algorithm]
// <compressed-stream> is the raw :WofCompressedData stream of a file compacted by Windows.
// algorithm is the WOF file-provider value from the reparse data: 0 XPRESS 4K, 1 LZX (default), 2 XPRESS 8K, 3 XPRESS 16K.
return Run(args);

static int Run(string[] args)
{
    if (args.Length is < 3 or > 4 || !long.TryParse(args[1], out var uncompressedSize) || uncompressedSize <= 0)
    {
        Console.Error.WriteLine("usage: wofdecode <compressed-stream> <uncompressed-size> <output> [algorithm]");
        return 2;
    }

    var algorithm = args.Length == 4 ? int.Parse(args[3]) : 1;
    var chunkSize = algorithm switch
    {
        0 => 4096,
        1 => 32768,
        2 => 8192,
        3 => 16384,
        _ => throw new ArgumentException($"Unsupported WOF algorithm {algorithm}.")
    };

    var chunkCount = (uncompressedSize + chunkSize - 1) / chunkSize;
    var tableBytes = (chunkCount - 1) * 4;

    using var source = File.OpenRead(args[0]);
    if (source.Length < tableBytes)
    {
        Console.Error.WriteLine("The stream is shorter than its chunk table.");
        return 1;
    }

    var table = new byte[tableBytes];
    source.ReadExactly(table);
    var ends = new long[chunkCount];
    for (var i = 0; i < chunkCount - 1; i++)
    {
        ends[i] = BitConverter.ToUInt32(table, i * 4);
    }

    ends[chunkCount - 1] = source.Length - tableBytes;

    var failures = new List<string>();
    long rawChunks = 0, decodedChunks = 0, worstUnusedBits = 0;

    using var output = File.Create(args[2]);
    for (var i = 0; i < chunkCount; i++)
    {
        var expect = i < chunkCount - 1 ? chunkSize : (int)(uncompressedSize - (chunkCount - 1) * chunkSize);
        var start = i == 0 ? 0 : ends[i - 1];
        var size = (int)(ends[i] - start);

        source.Seek(tableBytes + start, SeekOrigin.Begin);
        var raw = new byte[size];
        source.ReadExactly(raw);

        if (size == 0)
        {
            // An all-zero chunk is stored as nothing.
            output.Write(new byte[expect]);
            continue;
        }

        if (size == expect)
        {
            // Incompressible chunks are stored as they are.
            output.Write(raw);
            rawChunks++;
            continue;
        }

        try
        {
            byte[] decoded;
            long bitsConsumed;
            if (algorithm == 1)
            {
                decoded = LzxWimDecoder.Decode(raw, expect, out bitsConsumed);
            }
            else
            {
                decoded = XpressDecoder.Decode(raw, expect, out bitsConsumed);
            }

            // An odd-length chunk has its last byte in the high half of a word, so its bit count is rounded up to whole words.
            // LZX must consume its chunk to within padding. XPRESS is checked by its end marker in the decoder:
            // its streams carry extra trailing bytes after the marker that are not yet understood.
            if (algorithm == 1)
            {
                var availableBits = (size + (size & 1)) * 8L;
                var unusedBits = availableBits - bitsConsumed;
                if (unusedBits < 0 || unusedBits > 16)
                {
                    failures.Add($"chunk {i}: {unusedBits} unused bits");
                }

                worstUnusedBits = Math.Max(worstUnusedBits, unusedBits);
            }

            output.Write(decoded);
            decodedChunks++;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            failures.Add($"chunk {i}: {ex.Message}");
            output.Write(new byte[expect]);
        }
    }

    Console.WriteLine($"algorithm {algorithm}: chunks {chunkCount} (raw {rawChunks}, decoded {decodedChunks}); worst unused bits {worstUnusedBits}; failures {failures.Count}");
    foreach (var failure in failures.Take(10))
    {
        Console.Error.WriteLine(failure);
    }

    return failures.Count == 0 ? 0 : 1;
}
