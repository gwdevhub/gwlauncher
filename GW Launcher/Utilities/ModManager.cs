using IWshRuntimeLibrary;
using File = System.IO.File;

namespace GW_Launcher.Utilities;

public class ModManager
{
    public static IOrderedEnumerable<string> GetDlls(string path, Account account)
    {
        return GetMods(path, account).Item1;
    }

    public static IOrderedEnumerable<string> GetTexmods(string path, Account account)
    {
        return GetMods(path, account).Item2;
    }

    private static Tuple<IOrderedEnumerable<string>, IOrderedEnumerable<string>>
        GetMods(string path, Account account)
    {
        var dllsToLoad = new List<string>();
        var texsToLoad = new List<string>();
        if (account.usePluginFolderMods)
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
            if (Directory.Exists(directory))
            {
                var links = Directory.GetFiles(directory, "*.lnk");
                var files = Directory.GetFiles(directory, "*.dll");
                var dlllinks = links.Select(GetShortcutPath).Where(l => l.EndsWith(".dll")).ToArray();
                var textures = Directory.GetFiles(directory, "*").Where(t => t.EndsWith(".tpf") || t.EndsWith(".zip"));

                dllsToLoad.AddRange(files);
                dllsToLoad.AddRange(dlllinks);
                texsToLoad.AddRange(textures);
            }

            directory = Path.Combine(Path.GetDirectoryName(path)!, "plugins");
            if (Directory.Exists(directory))
            {
                var links = Directory.GetFiles(directory, "*.lnk");
                var files = Directory.GetFiles(directory, "*.dll");
                var dlllinks = links.Select(GetShortcutPath).Where(l => l.EndsWith(".dll")).ToArray();
                var textures = Directory.GetFiles(directory, "*").Where(t => t.EndsWith(".tpf") || t.EndsWith(".zip"));

                dllsToLoad.AddRange(dlllinks);
                dllsToLoad.AddRange(files);
                texsToLoad.AddRange(textures);
            }
        }

        dllsToLoad.AddRange(account.mods
            .Where(mod => mod is { type: ModType.kModTypeDLL, active: true } && File.Exists(mod.fileName))
            .Select(mod => mod.fileName));
        texsToLoad.AddRange(account.mods
            .Where(mod => mod is { type: ModType.kModTypeTexmod, active: true } && File.Exists(mod.fileName))
            .Select(mod => mod.fileName));

        if (texsToLoad.Count > 0)
        {
            dllsToLoad.Add(Path.Combine(Directory.GetCurrentDirectory(), "gMod.dll"));
        }

        return Tuple.Create(
            dllsToLoad.Distinct()
                .OrderByDescending(dllpath => dllpath == Path.Combine(Directory.GetCurrentDirectory(), "gMod.dll"))
                .ThenBy(Path.GetFileName),
            texsToLoad.Distinct().OrderBy(Path.GetFileName)
        );
    }

    private static string GetShortcutPath(string path)
    {
        var shell = new WshShell();
        var lnk = (IWshShortcut)shell.CreateShortcut(path);

        return lnk.TargetPath;
    }
}
