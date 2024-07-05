using System;
using System.Collections.Generic;
using System.Linq;

namespace GW_Launcher.Guildwars.Utils;

internal sealed class HuffmanTable
{
    private readonly (uint, uint)[] nodes = new (uint, uint)[256];
    private readonly List<(uint, int, int)> largeSymbolTranslation = [];
    private readonly List<uint> largeSymbolValues = [];

    private HuffmanTable((uint, uint)[] nodes, int largeSymbolCount)
    {
        this.nodes = nodes;
        this.largeSymbolTranslation = new List<(uint, int, int)>(24);
        for (int i = 0; i < 24; i++)
        {
            this.largeSymbolTranslation.Add((0, 0, 0));
        }

        this.largeSymbolValues = new List<uint>(largeSymbolCount);
    }

    public uint GetNextCode(BitStream stream)
    {
        var bits = stream.Peek(8);
        (var encLen, var encVal) = this.nodes[bits];

        if (encLen == 0xFFFFFFFF)
        {
            var buf1 = stream.Peek(32);
            (var firstEncoding, var lastIndex, var encLength) = this.largeSymbolTranslation.First(tuple => tuple.Item1 <= buf1);
            encLen = (uint)encLength;
            var groupIndex = (buf1 - firstEncoding) >> (32 - encLength);
            var largeEncIndex = lastIndex - (int)groupIndex;
            if (largeEncIndex < 0 || largeEncIndex >= this.largeSymbolValues.Count)
            {
                throw new InvalidOperationException("Failed to get next Huffman Table code. largeEncIndex >= this.largeSymbolValues.Count");
            }

            encVal = this.largeSymbolValues[largeEncIndex];
        }

        stream.Consume((int)encLen);
        return encVal;
    }

    public static HuffmanTable BuildHuffmanTable(BitStream stream)
    {
        var symbolFollowTableRoot = new uint[32];
        for (int i = 0; i < symbolFollowTableRoot.Length; i++)
        {
            symbolFollowTableRoot[i] = 0xFFFFFFFF;
        }

        var symbolCount = (int)stream.Read(16);
        var symbolFollowTable = new int[symbolCount];
        var totalSymbolCount = 0;

        var symbolIdx = symbolCount - 1;
        while (symbolIdx != -1)
        {
            var buf1 = stream.Peek(32);
            int idx;
            for (idx = 0; idx < Huffman.Table1.Length; idx++)
            {
                if (Huffman.Table1[idx].Item1 <= buf1)
                {
                    break;
                }
            }

            if (idx == Huffman.Table1.Length)
            {
                throw new InvalidOperationException("Failed to build Huffman Table. Index out of table1 bounds");
            }

            var bitCount = idx + 3;
            var offset = (int)((buf1 - Huffman.Table1[idx].Item1) >> (32 - bitCount));
            stream.Consume(bitCount);

            var temp = Huffman.Table2[Huffman.Table1[idx].Item2 - offset];
            var numberOfSymbol = temp >> 5;
            var symbolLen = temp & 0x1F;

            if (symbolLen != 0 || symbolCount < 2)
            {
                numberOfSymbol += 1;
                totalSymbolCount += (int)numberOfSymbol;
                for (int i = 0; i < numberOfSymbol; i++)
                {
                    symbolFollowTable[symbolIdx] = (int)symbolFollowTableRoot[symbolLen];
                    symbolFollowTableRoot[symbolLen] = (uint)symbolIdx;
                    symbolIdx--;
                }
            }
            else
            {
                symbolIdx -= (int)(numberOfSymbol + 1);
            }
        }

        if (totalSymbolCount == 0)
        {
            symbolFollowTable[symbolCount - 1] = (int)symbolFollowTableRoot[0];
            symbolFollowTableRoot[0] = (uint)(symbolCount - 1);
            totalSymbolCount = 1;
        }

        var nextBitsEncoding = 1;
        var symbolInHuffmanTable = 0;
        var nodes = new (uint, uint)[256];
        for (var encLen = 1; encLen <= 8; encLen++)
        {
            var currentSymbol = symbolFollowTableRoot[encLen];
            while (currentSymbol != 0xFFFFFFFF)
            {
                if (currentSymbol >= symbolCount)
                {
                    throw new InvalidOperationException("Failed to build Huffman Table. currentSymbol >= symbolCount");
                }

                if (nextBitsEncoding >= (1 << encLen))
                {
                    throw new InvalidOperationException("Failed to build Huffman Table. nextBitsEncoding >= (1 << encLen)");
                }

                var firstSymbol = nextBitsEncoding << (8 - encLen);
                var iterCount = 1 << (8 - encLen);

                for (var idx = firstSymbol; idx < firstSymbol + iterCount; idx++)
                {
                    nodes[idx] = ((uint)encLen, (uint)currentSymbol);
                }

                currentSymbol = (uint)symbolFollowTable[currentSymbol];
                symbolInHuffmanTable++;
                nextBitsEncoding--;
            }

            nextBitsEncoding = (nextBitsEncoding << 1) + 1;
        }

        var largeSymbolCount = totalSymbolCount - symbolInHuffmanTable;
        var huffman = new HuffmanTable(nodes, largeSymbolCount);
        if (symbolInHuffmanTable == totalSymbolCount)
        {
            return huffman;
        }

        for (var encLen = 9; encLen < 32; encLen++)
        {
            var currentSymbol = symbolFollowTableRoot[encLen];
            while (currentSymbol != 0xFFFFFFFF)
            {
                if (currentSymbol >= symbolCount)
                {
                    throw new InvalidOperationException("Failed to build Huffman Table. currentSymbol >= symbolCount");
                }
                if (nextBitsEncoding >= (1 << encLen))
                {
                    throw new InvalidOperationException("Failed to build Huffman Table. nextBitsEncoding >= (1 << encLen)");
                }

                int partialEncoding = nextBitsEncoding >> (encLen - 8);
                huffman.nodes[partialEncoding] = (0xFFFFFFFF, 0);
                huffman.largeSymbolValues.Add((uint)currentSymbol);
                currentSymbol = (uint)symbolFollowTable[currentSymbol];
                nextBitsEncoding--;
            }

            uint firstEncoding = (uint)((nextBitsEncoding + 1) << (32 - encLen));
            int lastIndex = huffman.largeSymbolValues.Count - 1;
            huffman.largeSymbolTranslation[encLen - 9] = (firstEncoding, lastIndex, encLen);

            nextBitsEncoding = (nextBitsEncoding << 1) + 1;
        }

        return huffman;
    }
}
