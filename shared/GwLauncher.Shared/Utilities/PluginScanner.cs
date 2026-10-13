using GW_Launcher.Classes;

namespace GW_Launcher.Utilities;

// A mod file found in a plugins folder or named by an account. TargetPath is the file that loads, or null when a
// shortcut's target isn't available on this computer.
public sealed record PluginFile(string SourcePath, string? TargetPath, ModType Type);

// Finds DLL and texmod files the same way on every platform. A shortcut is followed, and mapTarget turns its target
// into a path that exists here, or null when it doesn't.
public static class PluginScanner
{
    // Mod files in the folder, sorted by name. A missing folder gives an empty list.
    public static List<PluginFile> ScanFolder(string folder, Func<string, string?> mapTarget)
    {
        var result = new List<PluginFile>();
        if (!Directory.Exists(folder))
        {
            return result;
        }

        foreach (var path in Directory.GetFiles(folder).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (ScanFile(path, mapTarget) is { } plugin)
            {
                result.Add(plugin);
            }
        }

        return result;
    }

    // One mod file or shortcut. Null when the file doesn't exist or isn't a mod.
    public static PluginFile? ScanFile(string path, Func<string, string?> mapTarget)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        if (IsShortcut(path))
        {
            return FromShortcut(path, mapTarget);
        }

        return IsModPath(path) ? new PluginFile(path, Path.GetFullPath(path), TypeForPath(path)) : null;
    }

    public static ModType TypeForPath(string path) => IsDllPath(path) ? ModType.kModTypeDLL : ModType.kModTypeTexmod;

    public static bool IsModPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".dll" or ".tpf" or ".zip";

    private static PluginFile? FromShortcut(string shortcutPath, Func<string, string?> mapTarget)
    {
        ShellLink.Target target;
        try
        {
            target = ShellLink.Read(shortcutPath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
        {
            return new PluginFile(shortcutPath, null, ModType.kModTypeDLL);
        }

        if (target.WindowsPath == null || !IsModPath(target.WindowsPath))
        {
            return null;
        }

        return new PluginFile(shortcutPath, mapTarget(target.WindowsPath), TypeForPath(target.WindowsPath));
    }

    private static bool IsShortcut(string path) => Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    private static bool IsDllPath(string path) => Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase);
}
