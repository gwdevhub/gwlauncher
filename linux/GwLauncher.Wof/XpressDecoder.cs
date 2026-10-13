using System.Buffers.Binary;

namespace GwLauncher.Wof;

// Decodes one XPRESS Huffman chunk (WOF algorithms 0, 2 and 3: XPRESS 4K, 8K and 16K), ported from
// wimlib's xpress_decompress.c. The chunk starts with 512 4-bit code lengths packed into 256 bytes,
// followed by the Huffman-coded literal and match stream.
//
// The bit buffer is refilled a 16-bit word at a time, exactly as wimlib does. The extra length bytes are
// read as raw bytes at the next unfetched position, so the refill points must match the encoder's.
public static class XpressDecoder
{
    private const int NumSymbols = 512;
    private const int NumChars = 256;
    private const int MaxCodeLen = 15;
    private const int MinMatch = 3;

    public static byte[] Decode(ReadOnlySpan<byte> compressed, int outSize, out long bitsConsumed)
    {
        if (compressed.Length < NumSymbols / 2)
        {
            throw new InvalidDataException("XPRESS chunk is shorter than its code table.");
        }

        var lens = new int[NumSymbols];
        for (var i = 0; i < NumSymbols / 2; i++)
        {
            lens[2 * i] = compressed[i] & 0xF;
            lens[2 * i + 1] = compressed[i] >> 4;
        }

        var reader = new XpressReader(compressed.Slice(NumSymbols / 2).ToArray());
        var huffman = new CanonicalHuffman(lens, MaxCodeLen);
        var output = new byte[outSize];
        var written = 0;

        while (written < outSize)
        {
            var sym = reader.DecodeSymbol(huffman);
            if (sym < NumChars)
            {
                output[written++] = (byte)sym;
                continue;
            }

            var length = sym & 0xF;
            var log2Offset = (sym >> 4) & 0xF;

            reader.Ensure(16);
            var offset = (1 << log2Offset) | reader.Pop(log2Offset);

            if (length == 0xF)
            {
                length += reader.ReadRawByte();
                if (length == 0xF + 0xFF)
                {
                    length = reader.ReadRawU16();
                }
            }

            length += MinMatch;

            if (offset <= 0 || offset > written)
            {
                throw new InvalidDataException("XPRESS match points before the start of the chunk.");
            }

            if (written + length > outSize)
            {
                throw new InvalidDataException("XPRESS match runs past the end of the chunk.");
            }

            var source = written - offset;
            for (var k = 0; k < length; k++)
            {
                output[written] = output[source + k];
                written++;
            }
        }

        // Microsoft's encoder writes symbol 256 after the data as an end marker (MS-XCA). Requiring it is a
        // cheap check that the stream was read to its real end.
        if (reader.DecodeSymbol(huffman) != NumChars)
        {
            throw new InvalidDataException("XPRESS data is not followed by the end marker.");
        }

        bitsConsumed = reader.BitsConsumed;
        return output;
    }

    // wimlib's input_bitstream model: a 32-bit left-aligned buffer, refilled 16 bits at a time from `next`.
    private sealed class XpressReader
    {
        private readonly byte[] _data;
        private int _next;
        private uint _bitBuf;
        private int _bitsLeft;

        public XpressReader(byte[] data)
        {
            _data = data;
        }

        // Bits consumed from the whole chunk, including the 256-byte code table, and not counting prefetched bits.
        public long BitsConsumed => (NumSymbols / 2 + (long)_next) * 8 - _bitsLeft;

        public void Ensure(int bits)
        {
            if (_bitsLeft >= bits)
            {
                return;
            }

            if (_data.Length - _next < 2)
            {
                _bitsLeft = 32;
                return;
            }

            var word = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_next, 2));
            _bitBuf |= (uint)word << (16 - _bitsLeft);
            _next += 2;
            _bitsLeft += 16;
        }

        public int Pop(int count)
        {
            if (count == 0)
            {
                return 0;
            }

            Ensure(count);
            var value = (int)(_bitBuf >> (32 - count));
            _bitBuf <<= count;
            _bitsLeft -= count;
            return value;
        }

        public int DecodeSymbol(CanonicalHuffman huffman)
        {
            Ensure(MaxCodeLen);
            var (symbol, length) = huffman.DecodeFromBits(_bitBuf >> (32 - MaxCodeLen));
            _bitBuf <<= length;
            _bitsLeft -= length;
            return symbol;
        }

        public int ReadRawByte()
        {
            if (_next >= _data.Length)
            {
                return 0;
            }

            return _data[_next++];
        }

        public int ReadRawU16()
        {
            if (_data.Length - _next < 2)
            {
                return 0;
            }

            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_next, 2));
            _next += 2;
            return value;
        }
    }
}
