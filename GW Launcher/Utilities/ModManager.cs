using File = System.IO.File;

namespace GW_Launcher.Utilities;

public class SideloadedMod
{
    public string filePath = "";
    public ModType type;
    public string sourceFolder = "";
}

public class ModManager
{
    public static IOrderedEnumerable<string> GetDlls(Account account)
    {
        return GetMods(account).Item1;
    }

    public static IOrderedEnumerable<string> GetTexmods(Account account)
    {
        return GetMods(account).Item2;
    }

    // Plugins side-loaded from the "plugins" folders, in the same order GetMods scans them.
    private static IEnumerable<string> GetPluginFolders(Account account)
    {
        yield return Path.Combine(Directory.GetCurrentDirectory(), "plugins");

        if (account.gwpath != "")
        {
            var dir = Path.GetDirectoryName(account.gwpath);
            if (dir != null)
                yield return Path.Combine(dir, "plugins");
        }
    }

    public static List<SideloadedMod> GetPluginFolderMods(Account account)
    {
        var result = new List<SideloadedMod>();
        if (!account.usePluginFolderMods)
            return result;

        foreach (var folder in GetPluginFolders(account))
        {
            foreach (var plugin in PluginScanner.ScanFolder(folder, MapTarget).Where(plugin => plugin.TargetPath != null))
                result.Add(new SideloadedMod { filePath = plugin.TargetPath!, type = plugin.Type, sourceFolder = folder });
        }

        return result;
    }

    // Only a real GWToolboxdll.dll counts; a .lnk pointing at one is the supported way to side-load it.
    public static string? FindUnsupportedToolboxPlugin(Account account)
    {
        if (!account.usePluginFolderMods)
            return null;

        return GetPluginFolders(account)
            .Where(Directory.Exists)
            .SelectMany(Directory.GetFiles)
            .FirstOrDefault(path => Path.GetFileName(path).Equals("GWToolboxdll.dll", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ModFileExists(string path)
    {
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            path = GetShortcutPath(path) ?? "";
        return File.Exists(path);
    }

    // A shortcut's target is used only when it exists on disk.
    private static string? MapTarget(string windowsPath)
    {
        return File.Exists(windowsPath) ? Path.GetFullPath(windowsPath) : null;
    }

    private static List<string> PathsOfType(IEnumerable<PluginFile> plugins, ModType type)
    {
        return plugins.Where(plugin => plugin.Type == type && plugin.TargetPath != null)
            .Select(plugin => plugin.TargetPath!)
            .ToList();
    }

    private static Tuple<IOrderedEnumerable<string>, IOrderedEnumerable<string>>
        GetMods(Account account)
    {
        var plugins = new List<PluginFile>();
        if (account.usePluginFolderMods)
        {
            foreach (var directory in GetPluginFolders(account))
                plugins.AddRange(PluginScanner.ScanFolder(directory, MapTarget));
        }

        foreach (var mod in account.mods.Where(mod => mod.active))
        {
            if (PluginScanner.ScanFile(mod.fileName, MapTarget) is { } plugin)
                plugins.Add(plugin);
        }

        var dllsToLoad = PathsOfType(plugins, ModType.kModTypeDLL);
        var texsToLoad = PathsOfType(plugins, ModType.kModTypeTexmod);

        if (texsToLoad.Count > 0)
        {
            var found = dllsToLoad.Find(str => str.EndsWith("gMod.dll", StringComparison.OrdinalIgnoreCase));
            if (found == null)
            {
                var baseDirectory = AppContext.BaseDirectory;
                var gmod = Path.Combine(baseDirectory, "gMod.dll");
                if (!File.Exists(gmod))
                    gmod = Path.Combine(Directory.GetCurrentDirectory(), "gMod.dll");
                if (!File.Exists(gmod))
                {
                    MessageBox.Show(
                        "You have at least one texmod selected, but we could not find gMod.dll. Does your Firewall block GW Launcher from downloading it or does GW Launcher not have write permissions in the directory it's in?",
                        "Error");
                    return Tuple.Create(
                        dllsToLoad.Distinct().OrderBy(Path.GetFileName),
                        texsToLoad.Distinct().OrderBy(Path.GetFileName)
                    );
                }
                dllsToLoad.Add(Path.GetFullPath(gmod));
            }
        }

        return Tuple.Create(
            dllsToLoad.Distinct().OrderBy(Path.GetFileName),
            texsToLoad.Distinct().OrderBy(Path.GetFileName)
        );
    }

    private static string? GetShortcutPath(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return ShellLink.Read(path).WindowsPath;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
        {
            return null;
        }
    }
}
