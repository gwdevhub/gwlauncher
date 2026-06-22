using IWshRuntimeLibrary;
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
            if (!Directory.Exists(folder))
                continue;

            foreach (var path in Directory.GetFiles(folder))
            {
                var dll = GetDllPath(path);
                if (dll != null)
                {
                    result.Add(new SideloadedMod { filePath = dll, type = ModType.kModTypeDLL, sourceFolder = folder });
                    continue;
                }

                var tpf = GetTpfPath(path);
                if (tpf != null)
                    result.Add(new SideloadedMod { filePath = tpf, type = ModType.kModTypeTexmod, sourceFolder = folder });
            }
        }

        return result;
    }

    public static bool ModFileExists(string path)
    {
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            path = GetShortcutPath(path) ?? "";
        return File.Exists(path);
    }

    private static string? GetDllPath(string? path)
    {
        if (path != null && path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            path = GetShortcutPath(path);
        if (path == null || !File.Exists(path))
            return null;
        return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(path) : null;
    }

    private static string? GetTpfPath(string? path)
    {
        if (path != null && path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            path = GetShortcutPath(path);
        if (path == null || !File.Exists(path))
            return null;
        if (path.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(path);
        return null;
    }

    private static bool AddMod(string filePath, ref List<string> dllsOut, ref List<string> texmodsOut)
    {
        var actual = GetDllPath(filePath);
        if (actual != null)
        {
            dllsOut.Add(actual);
            return true;
        }

        actual = GetTpfPath(filePath);
        if (actual != null)
        {
            texmodsOut.Add(actual);
            return true;
        }

        return false;
    }

    private static int AddMods(string directory, ref List<string> dllsOut, ref List<string> texmodsOut)
    {
        if (!Directory.Exists(directory))
            return 0;
        var links = Directory.GetFiles(directory);
        var added = 0;
        foreach (var path in links)
        {
            if (AddMod(path, ref dllsOut, ref texmodsOut))
                added++;
        }

        return added;
    }

    private static Tuple<IOrderedEnumerable<string>, IOrderedEnumerable<string>>
        GetMods(Account account)
    {
        var dllsToLoad = new List<string>();
        var texsToLoad = new List<string>();
        if (account.usePluginFolderMods)
        {
            foreach (var directory in GetPluginFolders(account))
                AddMods(directory, ref dllsToLoad, ref texsToLoad);
        }

        foreach (var mod in account.mods.Where(mod => mod.active))
        {
            AddMod(mod.fileName, ref dllsToLoad, ref texsToLoad);
        }

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
                AddMod(gmod, ref dllsToLoad, ref texsToLoad);
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
        var shell = new WshShell();
        var lnk = (IWshShortcut)shell.CreateShortcut(path);

        return lnk.TargetPath;
    }
}
