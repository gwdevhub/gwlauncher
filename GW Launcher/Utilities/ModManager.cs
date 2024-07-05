using IWshRuntimeLibrary;
using File = System.IO.File;

namespace GW_Launcher.Utilities;

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
    private static string? GetDllPath(string? path)
    {
        if (path != null && path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            path = GetShortcutPath(path);
        if (path == null || !File.Exists(path))
            return null;
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return path;
        return null;
    }
    private static string? GetTpfPath(string? path)
    {
        if (path != null && path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            path = GetShortcutPath(path);
        if (path == null || !File.Exists(path))
            return null;
        if (path.EndsWith(".tpf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return path;
        return null;
    }
    private static bool AddMod(string file_path, ref List<string> dlls_out, ref List<string> texmods_out)
    {
        var actual = GetDllPath(file_path);
        if (actual != null)
        {
            dlls_out.Add(actual);
            return true;
        }
        actual = GetTpfPath(file_path);
        if (actual != null)
        {
            texmods_out.Add(actual);
            return true;
        }
        return false;
    }
    private static int AddMods(string directory, ref List<string> dlls_out, ref List<string> texmods_out)
    {
        if (!Directory.Exists(directory))
            return 0;
        var links = Directory.GetFiles(directory);
        var added = 0;
        foreach (var path in links)
        {
            if (AddMod(path, ref dlls_out, ref texmods_out))
                added++;
        }
        return added;
    }

    private static Tuple<IOrderedEnumerable<string>, IOrderedEnumerable<string>>
        GetMods(Account account)
    {
        var dllsToLoad = new List<string>();
        var texsToLoad = new List<string>();
        var path = account.gwpath;
        if (account.usePluginFolderMods)
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
            AddMods(directory, ref dllsToLoad, ref texsToLoad);

            directory = Path.Combine(Path.GetDirectoryName(path)!, "plugins");
            AddMods(directory, ref dllsToLoad, ref texsToLoad);
        }
        foreach(var mod in account.mods)
        {
            if (mod.active != true)
                continue;
            AddMod(mod.fileName, ref dllsToLoad, ref texsToLoad);
        }

        if (texsToLoad.Count > 0)
        {
            var found = dllsToLoad.Find(str => str.EndsWith("gmod.dll", StringComparison.OrdinalIgnoreCase));
            if (found == null)
                AddMod(Path.Combine(Directory.GetCurrentDirectory(), "gMod.dll"), ref dllsToLoad, ref texsToLoad);
        }

        return Tuple.Create(
            dllsToLoad.Distinct().OrderBy(Path.GetFileName),
            texsToLoad.Distinct().OrderBy(Path.GetFileName)
        );
    }

    private static string? GetShortcutPath(string path)
    {
        if (path == null || !File.Exists(path))
            return null;
        var shell = new WshShell();
        var lnk = (IWshShortcut)shell.CreateShortcut(path);

        return lnk.TargetPath;
    }
}
