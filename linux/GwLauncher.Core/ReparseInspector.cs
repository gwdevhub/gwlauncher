using System.Runtime.InteropServices;

namespace GwLauncher.Linux;

// Reads the Windows reparse tag of a path without following it. On ntfs-3g this is exposed as the
// system.ntfs_reparse_data extended attribute, which is how a WOF-compacted file shows up on Linux.
public static class ReparseInspector
{
    public const uint WofTag = 0x80000017;

    private const int BufferSize = 64;

    public static uint? ReparseTag(string path)
    {
        var buffer = new byte[BufferSize];
        var length = lgetxattr(path, "system.ntfs_reparse_data", buffer, (nint)buffer.Length);
        if (length < 4)
        {
            return null;
        }

        return BitConverter.ToUInt32(buffer, 0);
    }

    public static bool IsWofStub(string path) => ReparseTag(path) == WofTag;

    // Why a path can't be used, phrased to follow "<label> is ". Covers WOF stubs, other Windows links, missing
    // files, and files that exist but can't be read. Always includes the path.
    public static string UnusableReason(string path)
    {
        var tag = ReparseTag(path);
        if (tag == WofTag)
        {
            return $"compressed by Windows (WOF), so Linux can't read it: {path}";
        }

        if (tag != null)
        {
            return $"a Windows link (reparse tag 0x{tag:X8}) that Linux can't read: {path}";
        }

        if (!File.Exists(path) && new FileInfo(path).LinkTarget == null)
        {
            return $"missing: {path}";
        }

        return $"unreadable on Linux: {path}";
    }

    [DllImport("libc", SetLastError = true)]
    private static extern nint lgetxattr(string path, string name, byte[] value, nint size);
}
