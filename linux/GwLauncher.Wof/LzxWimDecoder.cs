using System.Buffers.Binary;

namespace GwLauncher.Wof;

// Decodes one WIM/WOF LZX chunk (algorithm 1, 32 KiB chunks). Ported from wimlib's lzx_decompress.c and
// lzx_common.c, and checked against the prototype and against Gw.exe / Gw.dat. A WOF chunk is an independent
// stream: no E8 header, a 1-bit block-size flag, and E8 call translation undone after decoding.
public static class LzxWimDecoder
{
    private const int NumChars = 256;
    private const int NumPrimaryLens = 7;
    private const int MinMatch = 2;
    private const int AlignedBits = 3;
    private const int DefaultBlockSize = 32768;
    private const int NumSlots32K = 30;
    private const int LenCodeSymbols = 249;
    private const int PrecodeSymbols = 20;
    private const int MaxCodeLen = 16;
    private const int WimMagicFileSize = 12000000;

    private static readonly int[] OffsetSlotBase =
    {
        -2, -1, 0, 1, 2, 4, 6, 10, 14, 22, 30, 46, 62, 94, 126,
        190, 254, 382, 510, 766, 1022, 1534, 2046, 3070, 4094,
        6142, 8190, 12286, 16382, 24574,
    };

    private static readonly int[] ExtraOffsetBits =
    {
        0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8,
        9, 9, 10, 10, 11, 11, 12, 12, 13, 13,
    };

    // Decodes a chunk that must produce exactly outSize bytes. bitsConsumed is how much of the input the stream
    // used, so trailing padding can be checked.
    public static byte[] Decode(ReadOnlySpan<byte> compressed, int outSize, out long bitsConsumed)
    {
        var bits = new WofBitReader(compressed);
        var mainLens = new int[NumChars + 8 * NumSlots32K];
        var lenLens = new int[LenCodeSymbols];
        var recent = new long[] { 1, 1, 1 };
        var output = new byte[outSize];
        var written = 0;

        while (written < outSize)
        {
            var blockType = bits.Read(3);
            var size = bits.Read(1) == 1 ? DefaultBlockSize : bits.Read(16);
            if (size < 1 || written + size > outSize)
            {
                throw new InvalidDataException($"LZX block size {size} does not fit the chunk.");
            }

            if (blockType == 3)
            {
                // Uncompressed block: three repeated offsets, then raw bytes, padded to an even length.
                var pos = bits.AlignToBytes();
                for (var i = 0; i < 3; i++)
                {
                    recent[i] = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Slice(pos + 4 * i, 4));
                }

                pos += 12;
                compressed.Slice(pos, size).CopyTo(output.AsSpan(written));
                written += size;
                pos += size + (size & 1);
                bits = new WofBitReader(compressed, pos);
                continue;
            }

            CanonicalHuffman? aligned = null;
            if (blockType == 2)
            {
                var alignedLens = new int[8];
                for (var i = 0; i < 8; i++)
                {
                    alignedLens[i] = bits.Read(AlignedBits);
                }

                aligned = new CanonicalHuffman(alignedLens, 7);
            }

            ReadCodewordLengths(bits, mainLens, 0, NumChars);
            ReadCodewordLengths(bits, mainLens, NumChars, NumChars + 8 * NumSlots32K);
            var main = new CanonicalHuffman(mainLens, MaxCodeLen);
            ReadCodewordLengths(bits, lenLens, 0, LenCodeSymbols);
            var lens = new CanonicalHuffman(lenLens, MaxCodeLen);

            var end = written + size;
            while (written < end)
            {
                var sym = main.Decode(bits);
                if (sym < NumChars)
                {
                    output[written++] = (byte)sym;
                    continue;
                }

                var s = sym - NumChars;
                var length = s % 8;
                var slot = s / 8;
                if (length == NumPrimaryLens)
                {
                    length += lens.Decode(bits);
                }

                length += MinMatch;

                long offset;
                if (slot < 3)
                {
                    offset = recent[slot];
                    recent[slot] = recent[0];
                }
                else
                {
                    var extra = ExtraOffsetBits[slot];
                    long v;
                    if (blockType == 2 && extra >= AlignedBits)
                    {
                        v = ((long)bits.Read(extra - AlignedBits) << AlignedBits) | (long)aligned!.Decode(bits);
                    }
                    else
                    {
                        v = bits.Read(extra);
                    }

                    offset = OffsetSlotBase[slot] + v;
                    recent[2] = recent[1];
                    recent[1] = recent[0];
                }

                recent[0] = offset;
                if (offset <= 0 || offset > written)
                {
                    throw new InvalidDataException("LZX match points before the start of the chunk.");
                }

                if (written + length > end)
                {
                    throw new InvalidDataException("LZX match runs past the end of its block.");
                }

                var source = written - (int)offset;
                for (var k = 0; k < length; k++)
                {
                    output[written] = output[source + k];
                    written++;
                }
            }
        }

        bitsConsumed = bits.BitPosition;
        UndoE8Translation(output);
        return output;
    }

    // Reads delta-coded code lengths through a 20-symbol precode, as wimlib's lzx_read_codeword_lens does.
    private static void ReadCodewordLengths(WofBitReader bits, int[] lens, int first, int last)
    {
        var precode = new CanonicalHuffman(ReadPrecodeLengths(bits), 16);
        var i = first;
        while (i < last)
        {
            var sym = precode.Decode(bits);
            if (sym < 17)
            {
                var v = lens[i] - sym;
                lens[i++] = v < 0 ? v + 17 : v;
            }
            else if (sym == 17)
            {
                i = FillRun(lens, i, last, 4 + bits.Read(4), 0);
            }
            else if (sym == 18)
            {
                i = FillRun(lens, i, last, 20 + bits.Read(5), 0);
            }
            else
            {
                var count = 4 + bits.Read(1);
                var z = precode.Decode(bits);
                if (z > 17)
                {
                    throw new InvalidDataException("LZX length run uses an invalid precode symbol.");
                }

                var v = lens[i] - z;
                v = v < 0 ? v + 17 : v;
                i = FillRun(lens, i, last, count, v);
            }
        }
    }

    private static int[] ReadPrecodeLengths(WofBitReader bits)
    {
        var lens = new int[PrecodeSymbols];
        for (var i = 0; i < PrecodeSymbols; i++)
        {
            lens[i] = bits.Read(4);
        }

        return lens;
    }

    private static int FillRun(int[] lens, int i, int last, int count, int value)
    {
        for (var k = 0; k < count; k++)
        {
            if (i >= last)
            {
                throw new InvalidDataException("LZX code length run overflows its table.");
            }

            lens[i++] = value;
        }

        return i;
    }

    // Undoes LZX E8 call translation in place, as wimlib's lzx_e8_filter does.
    private static void UndoE8Translation(byte[] data)
    {
        var size = data.Length;
        if (size <= 10)
        {
            return;
        }

        var limit = size - 10;
        var i = 0;
        while (i < limit)
        {
            if (data[i] != 0xE8)
            {
                i++;
                continue;
            }

            var target = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i + 1, 4));
            if (target >= 0)
            {
                if (target < WimMagicFileSize)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i + 1, 4), target - i);
                }
            }
            else if (target >= -i)
            {
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i + 1, 4), target + WimMagicFileSize);
            }

            i += 5;
        }
    }
}
