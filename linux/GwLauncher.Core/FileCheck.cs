namespace GwLauncher.Linux;

public static class FileCheck
{
    // File.Exists is true for a dangling symlink on Linux, and the copied Windows links (reparse points) are exactly that.
    // Following the link is what decides whether the file can actually be used.
    public static bool IsUsableFile(string path)
    {
        // A Windows-compressed file is never usable as-is, even when its link resolves. Every compressed file's link
        // points at the same name, so the target can be a different file entirely (one that gMod then reads as the texmod).
        if (ReparseInspector.IsWofStub(path))
        {
            return false;
        }

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return false;
        }

        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        return target == null || target.Exists;
    }
}
