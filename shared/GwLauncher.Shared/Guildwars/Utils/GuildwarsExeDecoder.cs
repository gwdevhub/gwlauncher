namespace GW_Launcher.Guildwars.Utils;

// Decodes the LZ77 + Huffman stream ArenaNet serves for Gw.exe. Has no UI so both launchers can call it.
public static class GuildwarsExeDecoder
{
    // onProgress receives the fraction of the output written so far, from 0 to 1.
    public static void Decompress(string compressedPath, string outputPath, int expectedFinalSize, Action<double>? onProgress = null)
    {
        using var input = new FileStream(compressedPath, FileMode.Open, FileAccess.Read);
        var bitStream = new BitStream(input);
        bitStream.Consume(4);
        var first4Bits = bitStream.Read(4);

        // Decoded in memory: seeking per byte on disk is far slower, especially under antivirus file hooks.
        var output = new byte[expectedFinalSize];
        var outputLength = 0;
        while (outputLength < expectedFinalSize)
        {
            var litHuffman = HuffmanTable.BuildHuffmanTable(bitStream);
            var distHuffman = HuffmanTable.BuildHuffmanTable(bitStream);
            var blockSize = (bitStream.Read(4) + 1) * 4096;
            for (var i = 0; i < blockSize && outputLength < expectedFinalSize; i++)
            {
                var code = litHuffman.GetNextCode(bitStream);
                if (code < 0x100)
                {
                    output[outputLength++] = (byte)code;
                    continue;
                }

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

                if (backtrack >= outputLength)
                {
                    throw new InvalidDataException("Downloaded Gw.exe is corrupt: a back-reference points before the start of the data.");
                }

                // Byte by byte, because a back-reference can overlap the bytes it is writing (runs of one value).
                var src = outputLength - (backtrack + 1);
                for (var j = src; j < src + backtrackCount; j++)
                {
                    output[outputLength++] = output[j];
                }
            }

            onProgress?.Invoke((double)outputLength / expectedFinalSize);
        }

        File.WriteAllBytes(outputPath, output);
    }
}
