using PeNet;
using PeNet.Header.Pe;

namespace GW_Launcher.Guildwars;

internal sealed class GuildWarsExecutableParser
{
    // Used by older Guild Wars versions
    private readonly static byte[] VersionPattern = [0x8B, 0xC8, 0x33, 0xDB, 0x39, 0x8D, 0xC0, 0xFD, 0xFF, 0xFF, 0x0F, 0x95, 0xC3];

    // Used by newer Guild Wars versions
    private readonly static byte?[] FileIdFunctionPattern = [0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x08, 0xE8, null, null, null, null, 0x83, 0x3D, null, null, null, null, 0x00];

    private readonly PeFile peFile;
    private readonly ImageSectionHeader textSection;

    public GuildWarsExecutableParser(string path)
    {
        this.peFile = new PeFile(path);
        this.textSection = this.peFile.ImageSectionHeaders?.FirstOrDefault(s => s.Name == ".text") ?? throw new InvalidOperationException("Unable to find .text section");
    }

    public int GetVersionLegacy()
    {
		var offset = this.Find(VersionPattern);
		var functionRva = this.FollowCall(offset - 5);
		var fileId = (int)this.Read(functionRva + 1);
		return fileId;
	}

    public int GetFileId()
    {
		var patternRva = this.FindWithWildcards(FileIdFunctionPattern);
		var callRva = patternRva + 0x32;
		var fileIdFunctionRva = this.FollowCall(callRva);
		var fileId = (int)this.Read(fileIdFunctionRva + 1);
		return fileId;
	}

    private uint FindWithWildcards(byte?[] pattern, int offset = 0)
    {
        var buffer = this.peFile.RawFile.AsSpan((int)this.textSection.PointerToRawData, (int)this.textSection.SizeOfRawData);
        for (int i = 0; i <= buffer.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (pattern[j].HasValue &&
                    buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return this.textSection.VirtualAddress + (uint)i + (uint)offset;
            }
        }

        throw new Exception("Couldn't find the pattern");
    }

    private uint Find(byte[] pattern, int offset = 0)
    {
        var buffer = this.peFile.RawFile.AsSpan(this.textSection.PointerToRawData, this.textSection.SizeOfRawData);
        var pos = IndexOf(buffer, pattern);
        if (pos == -1)
        {
            throw new Exception("Couldn't find the pattern");
        }

        return this.textSection.VirtualAddress + (uint)pos + (uint)offset;
    }

    private uint FollowCall(uint callRva)
    {
        var posInFile = this.RvaToOffset(callRva);
        var op = this.peFile.RawFile.ReadByte(posInFile);
        var callParam = BitConverter.ToInt32(this.peFile.RawFile.ToArray(), posInFile + 1);

        if (op != 0xE8 && op != 0xE9)
        {
            throw new Exception($"Unsupported opcode '0x{op:X2} ({op})'");
        }

        return callRva + (uint)callParam + 5;
    }

    private uint Read(uint rva)
    {
        var posInFile = this.RvaToOffset(rva);
        return BitConverter.ToUInt32(this.peFile.RawFile.ToArray(), posInFile);
    }

    private int RvaToOffset(uint rva)
    {
        var section = this.peFile.ImageSectionHeaders!.FirstOrDefault(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + s.VirtualSize);
        return section is null
            ? throw new Exception("Could not find section for RVA")
            : (int)(rva - section.VirtualAddress + section.PointerToRawData);
    }

    private static int IndexOf(Span<byte> haystack, Span<byte> needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (needle.SequenceEqual(haystack.Slice(i, needle.Length)))
            {
                return i;
            }
        }

        return -1;
    }
}
