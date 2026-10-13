using GW_Launcher.Classes;

namespace GW_Launcher.Utilities;

// The DLLs and texmods one launch loads, chosen from the account's active mods and plugin folders.
public sealed class ModSelection
{
    private ModSelection(IReadOnlyList<string> dlls, IReadOnlyList<string> texmods)
    {
        Dlls = dlls;
        Texmods = texmods;
    }

    public IReadOnlyList<string> Dlls { get; }

    public IReadOnlyList<string> Texmods { get; }

    // Texmods run through gMod, so gMod is needed when there are texmods and no gMod.dll among the DLLs.
    public bool NeedsGMod => Texmods.Count > 0 && !Dlls.Any(IsGMod);

    // Files whose target isn't available are left out. Results are de-duplicated and sorted by file name.
    public static ModSelection From(IEnumerable<PluginFile> files)
    {
        var available = files.Where(file => file.TargetPath != null).ToList();
        return new ModSelection(
            Sorted(available.Where(file => file.Type == ModType.kModTypeDLL).Select(file => file.TargetPath!)),
            Sorted(available.Where(file => file.Type == ModType.kModTypeTexmod).Select(file => file.TargetPath!)));
    }

    // The selection with gMod added, for when NeedsGMod is true and gMod was found at gmodPath.
    public ModSelection WithGMod(string gmodPath) => new(Sorted(Dlls.Append(gmodPath)), Texmods);

    public static bool IsGMod(string path) => Path.GetFileName(path).Equals("gMod.dll", StringComparison.OrdinalIgnoreCase);

    private static List<string> Sorted(IEnumerable<string> paths) => paths.Distinct().OrderBy(Path.GetFileName).ToList();
}
