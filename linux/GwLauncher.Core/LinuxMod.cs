using System.Text.RegularExpressions;
using GW_Launcher.Classes;
using GW_Launcher.Utilities;

namespace GwLauncher.Linux;

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

    // Files in the plugins folders, in the same order the Windows launcher scans them: the launcher's folder, then the game's.
    // Shortcuts are followed to their target, and kept only when the target is a DLL or texmod, as on Windows.
    public static List<PluginFile> PluginFolderFiles(string gwPath)
    {
        var folders = new List<string> { Path.Combine(AppPaths.DataDir, "plugins") };
        if (!string.IsNullOrEmpty(gwPath))
        {
            folders.Add(Path.Combine(Path.GetDirectoryName(gwPath) ?? "", "plugins"));
        }

        return folders.SelectMany(folder => PluginScanner.ScanFolder(folder, WindowsPaths.ToLinux)).ToList();
    }

    public static ModType TypeForPath(string path) => PluginScanner.TypeForPath(path);

    [GeneratedRegex(@"^[A-Za-z]:\\")]
    private static partial Regex WindowsPathPattern();
}
