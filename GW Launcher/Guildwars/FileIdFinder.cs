using PeNet;

namespace GW_Launcher.Guildwars;

public static class FileIdFinder
{
    public static int GetFileIdLegacy(string filePath)
    {
        byte[] pattern = [0x8B, 0xC8, 0x33, 0xDB, 0x39, 0x8D, 0xC0, 0xFD, 0xFF, 0xFF, 0x0F, 0x95, 0xC3];

        try
        {
            var fileScanner = new FileScanner(filePath);
            uint offset = fileScanner.Find(pattern);
            Console.WriteLine($"Pattern found at RVA: 0x{offset:X}");

            uint functionRva = fileScanner.FollowCall(offset - 5);
            int fileId = (int)fileScanner.Read(functionRva + 1);
            Console.WriteLine($"File ID: {fileId}");
            return fileId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return 0;
        }
    }

    public static int GetFileIdNew(string filePath)
    {
        byte?[] pattern = [0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x08, 0xE8, null, null, null, null, 0x83, 0x3D, null, null, null, null, 0x00];

        try
        {
            var fileScanner = new FileScanner(filePath);
            var patternRva = fileScanner.FindWithWildcards(pattern);
            Console.WriteLine($"Pattern found at RVA: 0x{patternRva:X}");
            
            var callRva = patternRva + 0x32;
            var fileIdFunctionRva = fileScanner.FollowCall(callRva);
            var fileId = (int)fileScanner.Read(fileIdFunctionRva + 1);
            Console.WriteLine($"File ID: {fileId}");
            return fileId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return 0;
        }
    }
}

internal class FileScanner
{
    private PeFile _peFile;
    private PeNet.Header.Pe.ImageSectionHeader _textSection;

    public FileScanner(string path)
    {
        _peFile = new PeFile(path);
        _textSection = _peFile.ImageSectionHeaders!.FirstOrDefault(s => s.Name == ".text")!;
        if (_textSection == null)
            throw new Exception("Could not find .text section");
    }

    public uint Find(byte[] pattern, int offset = 0)
    {
        var buffer = _peFile.RawFile.AsSpan(_textSection.PointerToRawData, _textSection.SizeOfRawData);
        int pos = IndexOf(buffer, pattern);
        if (pos == -1)
            throw new Exception("Couldn't find the pattern");
        return _textSection.VirtualAddress + (uint)pos + (uint)offset;
    }

    public uint FindWithWildcards(byte?[] pattern, int offset = 0)
    {
        var buffer = _peFile.RawFile.AsSpan((int)_textSection.PointerToRawData, (int)_textSection.SizeOfRawData);
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
                return _textSection.VirtualAddress + (uint)i + (uint)offset;
            }
        }

        throw new Exception("Couldn't find the pattern");
    }

    public uint FollowCall(uint callRva)
    {
        int posInFile = RvaToOffset(callRva);
        byte op = _peFile.RawFile.ReadByte(posInFile);
        int callParam = BitConverter.ToInt32(_peFile.RawFile.ToArray(), posInFile + 1);

        if (op != 0xE8 && op != 0xE9)
            throw new Exception($"Unsupported opcode '0x{op:X2} ({op})'");

        return callRva + (uint)callParam + 5;
    }

    public uint Read(uint rva)
    {
        int posInFile = RvaToOffset(rva);
        return BitConverter.ToUInt32(_peFile.RawFile.ToArray(), posInFile);
    }

    private int RvaToOffset(uint rva)
    {
        var section =
            _peFile.ImageSectionHeaders!.FirstOrDefault(s =>
                rva >= s.VirtualAddress && rva < s.VirtualAddress + s.VirtualSize);
        if (section == null)
            throw new Exception("Could not find section for RVA");
        return (int)(rva - section.VirtualAddress + section.PointerToRawData);
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
