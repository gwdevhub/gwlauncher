namespace GwLauncher.Wof;

// Bit-level primitives shared by the LZX and XPRESS decoders.
// Both formats read 16-bit little-endian words, most significant bit first.
internal sealed class WofBitReader
{
    private readonly byte[] _data;
    private long _bitPos;

    public WofBitReader(ReadOnlySpan<byte> data, int bytePos = 0)
    {
        _data = data.ToArray();
        _bitPos = (long)bytePos * 8;
    }

    public long BitPosition => _bitPos;

    public int Read(int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | NextBit();
        }

        return value;
    }

    // Skips to the next 16-bit boundary and returns the byte offset that follows.
    public int AlignToBytes()
    {
        var r = _bitPos % 16;
        if (r != 0)
        {
            _bitPos += 16 - r;
        }

        return (int)(_bitPos / 8);
    }

    // Windows writes an odd-length stream with its final byte as the high half of the last word; the
    // low half is absent. Reads past the end give zeros, so over-reads show up as negative unused bits
    // in the caller rather than as a silent success.
    private int NextBit()
    {
        var byteIndex = (int)(_bitPos / 16) * 2;
        int wordValue;
        if (byteIndex + 1 < _data.Length)
        {
            wordValue = _data[byteIndex] | (_data[byteIndex + 1] << 8);
        }
        else if (byteIndex < _data.Length)
        {
            wordValue = _data[byteIndex] << 8;
        }
        else
        {
            wordValue = 0;
        }

        var bitInWord = 15 - (int)(_bitPos % 16);
        _bitPos++;
        return (wordValue >> bitInWord) & 1;
    }
}

// Canonical Huffman decoding, one bit at a time. Codes are assigned in symbol order within each length.
internal sealed class CanonicalHuffman
{
    private readonly int[] _count;
    private readonly int[] _symbols;
    private readonly int _maxLen;

    public CanonicalHuffman(int[] lengths, int maxLen)
    {
        _maxLen = maxLen;
        _count = new int[maxLen + 1];
        foreach (var l in lengths)
        {
            if (l > maxLen)
            {
                throw new InvalidDataException($"Code length {l} exceeds the maximum of {maxLen}.");
            }

            _count[l]++;
        }

        _count[0] = 0;
        _symbols = new int[lengths.Length];
        var offsets = new int[maxLen + 2];
        for (var l = 1; l <= maxLen; l++)
        {
            offsets[l + 1] = offsets[l] + _count[l];
        }

        for (var sym = 0; sym < lengths.Length; sym++)
        {
            if (lengths[sym] != 0)
            {
                _symbols[offsets[lengths[sym]]++] = sym;
            }
        }
    }

    // Decodes from the top _maxLen bits of a buffer (most significant bit first). Returns the symbol and its code length.
    public (int Symbol, int Length) DecodeFromBits(uint topBits)
    {
        var code = 0;
        var first = 0;
        var index = 0;
        for (var len = 1; len <= _maxLen; len++)
        {
            code |= (int)((topBits >> (_maxLen - len)) & 1);
            var count = _count[len];
            if (code - first < count)
            {
                return (_symbols[index + (code - first)], len);
            }

            index += count;
            first += count;
            first <<= 1;
            code <<= 1;
        }

        throw new InvalidDataException("Invalid Huffman codeword.");
    }

    public int Decode(WofBitReader bits)
    {
        var code = 0;
        var first = 0;
        var index = 0;
        for (var len = 1; len <= _maxLen; len++)
        {
            code |= bits.Read(1);
            var count = _count[len];
            if (code - first < count)
            {
                return _symbols[index + (code - first)];
            }

            index += count;
            first += count;
            first <<= 1;
            code <<= 1;
        }

        throw new InvalidDataException("Invalid Huffman codeword.");
    }
}
