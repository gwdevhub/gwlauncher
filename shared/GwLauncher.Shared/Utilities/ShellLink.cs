using System.Text;

namespace GW_Launcher.Utilities;

// Reads the target of a Windows shortcut (.lnk), per MS-SHLLINK. Only the target path is needed for plugins.
public static class ShellLink
{
    private const uint HasLinkTargetIdList = 0x01;
    private const uint HasLinkInfo = 0x02;
    private const uint HasName = 0x04;
    private const uint HasRelativePath = 0x08;
    private const uint HasWorkingDir = 0x10;
    private const uint HasArguments = 0x20;
    private const uint HasIconLocation = 0x40;
    private const uint IsUnicode = 0x80;

    private const uint VolumeIdAndLocalBasePath = 0x01;
    private const int HeaderSize = 0x4C;

    public sealed record Target(string? WindowsPath, string? Arguments);

    public static Target Read(string shortcutPath)
    {
        return Parse(File.ReadAllBytes(shortcutPath), Path.GetDirectoryName(Path.GetFullPath(shortcutPath))!);
    }

    public static Target Parse(byte[] data, string shortcutFolder)
    {
        if (data.Length < HeaderSize || BitConverter.ToUInt32(data, 0) != HeaderSize)
        {
            throw new InvalidDataException("Not a Windows shortcut.");
        }

        var flags = BitConverter.ToUInt32(data, 0x14);
        var position = HeaderSize;

        if ((flags & HasLinkTargetIdList) != 0)
        {
            position += 2 + BitConverter.ToUInt16(data, position);
        }

        string? target = null;
        if ((flags & HasLinkInfo) != 0)
        {
            target = ReadLinkInfoPath(data, position);
            position += (int)BitConverter.ToUInt32(data, position);
        }

        var unicode = (flags & IsUnicode) != 0;
        if ((flags & HasName) != 0)
        {
            position = SkipString(data, position, unicode);
        }

        string? relative = null;
        if ((flags & HasRelativePath) != 0)
        {
            relative = ReadString(data, ref position, unicode);
        }

        if ((flags & HasWorkingDir) != 0)
        {
            position = SkipString(data, position, unicode);
        }

        string? arguments = null;
        if ((flags & HasArguments) != 0)
        {
            arguments = ReadString(data, ref position, unicode);
        }

        // The absolute path from LinkInfo is preferred; the relative path is only a fallback.
        target ??= relative == null ? null : Path.GetFullPath(Path.Combine(shortcutFolder, relative.Replace('\\', '/')));

        return new Target(target, arguments);
    }

    // LinkInfo holds the local base path plus a common suffix. The Unicode forms are used when the header has them.
    private static string? ReadLinkInfoPath(byte[] data, int start)
    {
        var flags = BitConverter.ToUInt32(data, start + 8);
        if ((flags & VolumeIdAndLocalBasePath) == 0)
        {
            return null;
        }

        var headerSize = BitConverter.ToUInt32(data, start + 4);
        var hasUnicodeOffsets = headerSize >= 0x24;

        var basePath = hasUnicodeOffsets
            ? ReadZeroTerminated(data, start + (int)BitConverter.ToUInt32(data, start + 0x1C), unicode: true)
            : null;
        basePath ??= ReadZeroTerminated(data, start + (int)BitConverter.ToUInt32(data, start + 0x10), unicode: false);

        var suffix = hasUnicodeOffsets
            ? ReadZeroTerminated(data, start + (int)BitConverter.ToUInt32(data, start + 0x20), unicode: true)
            : null;
        suffix ??= ReadZeroTerminated(data, start + (int)BitConverter.ToUInt32(data, start + 0x18), unicode: false);

        if (string.IsNullOrEmpty(basePath))
        {
            return null;
        }

        return string.IsNullOrEmpty(suffix) ? basePath : Path.Combine(basePath, suffix);
    }

    private static string? ReadZeroTerminated(byte[] data, int offset, bool unicode)
    {
        if (offset <= 0 || offset >= data.Length)
        {
            return null;
        }

        var end = offset;
        if (unicode)
        {
            while (end + 1 < data.Length && (data[end] != 0 || data[end + 1] != 0))
            {
                end += 2;
            }

            return Encoding.Unicode.GetString(data, offset, end - offset);
        }

        while (end < data.Length && data[end] != 0)
        {
            end++;
        }

        return Encoding.Default.GetString(data, offset, end - offset);
    }

    // StringData entries are a 2-byte character count followed by the characters.
    private static string ReadString(byte[] data, ref int position, bool unicode)
    {
        var count = BitConverter.ToUInt16(data, position);
        position += 2;
        var byteCount = unicode ? count * 2 : count;
        var text = unicode
            ? Encoding.Unicode.GetString(data, position, byteCount)
            : Encoding.Default.GetString(data, position, byteCount);
        position += byteCount;
        return text;
    }

    private static int SkipString(byte[] data, int position, bool unicode)
    {
        var count = BitConverter.ToUInt16(data, position);
        return position + 2 + (unicode ? count * 2 : count);
    }
}
