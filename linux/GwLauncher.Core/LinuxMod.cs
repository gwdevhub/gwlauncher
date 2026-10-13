using System.Text.RegularExpressions;
using GW_Launcher.Utilities;

namespace GwLauncher.Linux;

// Matches the Windows ModType enum values, so Accounts.json mod entries read the same on both platforms.
public enum ModType
{
    Texmod,
    Dll
}

// A file from a plugins folder. SourcePath is the file itself, or a .lnk shortcut to the real file.
// TargetPath is the Linux path that gets loaded, or null when a shortcut's target can't be found here.
public sealed record PluginFile(string SourcePath, string? TargetPath, ModType Type);

public sealed partial class LinuxMod
{
    [JsonProperty("active")] public bool Active { get; set; } = true;

    [JsonProperty("fileName")] public string FileName { get; set; } = "";

    [JsonProperty("type")] public ModType Type { get; set; }

    // Kept in the sidecar file (see PathSidecar), never in Accounts.json.
    [JsonIgnore] public string? LinuxPath { get; set; }

    [JsonIgnore] public string EffectivePath => string.IsNullOrEmpty(LinuxPath) ? FileName : LinuxPath;

    // A Windows path with no Linux path set can't be opened here, so it's reported as missing.
    [JsonIgnore] public bool IsWindowsPath => WindowsPathPattern().IsMatch(EffectivePath);

    [JsonIgnore] public bool IsAvailable => !IsWindowsPath && FileCheck.IsUsableFile(EffectivePath);

    public LinuxMod Clone() => new() { Active = Active, FileName = FileName, Type = Type, LinuxPath = LinuxPath };

    public static ModType TypeForPath(string path) => IsDllPath(path) ? ModType.Dll : ModType.Texmod;

    // Files in the plugins folders, in the same order the Windows launcher scans them: the launcher's folder, then the game's.
    // Shortcuts are followed to their target, and kept only when the target is a DLL or texmod, as on Windows.
    public static List<PluginFile> PluginFolderFiles(string gwPath)
    {
        var folders = new List<string> { Path.Combine(AppPaths.DataDir, "plugins") };
        if (!string.IsNullOrEmpty(gwPath))
        {
            folders.Add(Path.Combine(Path.GetDirectoryName(gwPath) ?? "", "plugins"));
        }

        var result = new List<PluginFile>();
        foreach (var folder in folders.Where(Directory.Exists))
        {
            foreach (var path in Directory.GetFiles(folder).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                if (IsShortcut(path))
                {
                    var plugin = FromShortcut(path);
                    if (plugin != null)
                    {
                        result.Add(plugin);
                    }
                }
                else if (IsModPath(path))
                {
                    result.Add(new PluginFile(path, path, TypeForPath(path)));
                }
            }
        }

        return result;
    }

    private static PluginFile? FromShortcut(string shortcutPath)
    {
        ShellLink.Target target;
        try
        {
            target = ShellLink.Read(shortcutPath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
        {
            return new PluginFile(shortcutPath, null, ModType.Dll);
        }

        if (target.WindowsPath == null || !IsModPath(target.WindowsPath))
        {
            return null;
        }

        return new PluginFile(shortcutPath, WindowsPaths.ToLinux(target.WindowsPath), TypeForPath(target.WindowsPath));
    }

    private static bool IsShortcut(string path) => Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    private static bool IsDllPath(string path) => Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase);

    private static bool IsModPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".dll" or ".tpf" or ".zip";

    [GeneratedRegex(@"^[A-Za-z]:\\")]
    private static partial Regex WindowsPathPattern();
}
