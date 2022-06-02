using GW_Launcher.uMod;
using Microsoft.Win32;
using IWshRuntimeLibrary;

namespace GW_Launcher;

internal class MulticlientPatch
{
    public static GWCAMemory LaunchClient(Account account)
    {
        var path = account.gwpath;
        var character = " ";
        if (!string.IsNullOrEmpty(account.character))
        {
            character = account.character;
        }

        if (GetTexmods(account.gwpath, account.mods).Any())
        {
            Task.Run(() =>
            {
                using var texClient = new uModTexClient();
                texClient.WaitForConnection();
                foreach (var tex in GetTexmods(path, account.mods))
                {
                    if (Program.shouldClose)
                    {
                        texClient.Dispose();
                        return;
                    }

                    texClient.AddFile(tex);
                }
                texClient.Send();

                GC.Collect(); // force garbage collection
            });
        }

        var args = $" -email \"{account.email}\" -password \"{account.password}\" -character \"{character}\" {account.extraargs}";
        var datfix = account.datfix;
        var nologin = false;
        var elevated = account.elevated;

        PatchRegistry(path);

        var dwPid = NativeMethods.LaunchClient(path, args,
            (int)GWML_FLAGS.KEEP_SUSPENDED | (datfix ? 0 : (int)GWML_FLAGS.NO_DATFIX) |
            (nologin ? (int)GWML_FLAGS.NO_LOGIN : 0) | (elevated ? (int)GWML_FLAGS.ELEVATED : 0), out var hThread);
        var proc = Process.GetProcessById((int)dwPid);
        var memory = new GWCAMemory(proc);

        foreach (var dll in GetDlls(path, account.mods))
        {
            memory.LoadModule(dll);
        }

        NativeMethods.ResumeThread(hThread);
        NativeMethods.CloseHandle(hThread);

        return memory;
    }

    public static GWCAMemory LaunchClient(string path)
    {
        PatchRegistry(path);

        var dwPid = NativeMethods.LaunchClient(path, "", 0, out var hThread);
        var proc = Process.GetProcessById((int)dwPid);
        var mem = new GWCAMemory(proc);

        NativeMethods.ResumeThread(hThread);
        NativeMethods.CloseHandle(hThread);

        return mem;
    }

    private static void PatchRegistry(string path)
    {
        try
        {
            var regSrc = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", null);
            if (regSrc != null && (string)regSrc != Path.GetFullPath(path))
            {
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(path));
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(path));
            }

            regSrc = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", null);
            if (regSrc != null && (string)regSrc != Path.GetFullPath(path))
            {
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src",
                    Path.GetFullPath(path));
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path",
                    Path.GetFullPath(path));
            }
        }
        catch (UnauthorizedAccessException)
        {

        }
    }

    private static IOrderedEnumerable<string> GetDlls(string path, IReadOnlyCollection<Mod> mods)
    {
        return GetMods(path, mods).Item1;
    }

    private static IOrderedEnumerable<string> GetTexmods(string path, IReadOnlyCollection<Mod> mods)
    {
        return GetMods(path, mods).Item2;
    }
    private static Tuple<IOrderedEnumerable<string>, IOrderedEnumerable<string>> GetMods(string path, IReadOnlyCollection<Mod> mods)
    {
        var directory = Directory.GetCurrentDirectory() + "\\plugins";
        var dllsToLoad = new List<string>();
        var texsToLoad = new List<string>();
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

        directory = Path.GetDirectoryName(path) + "\\plugins";
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

        dllsToLoad.AddRange(mods.Where(mod => mod.type == ModType.kModTypeDLL && System.IO.File.Exists(mod.fileName)).Select(mod => mod.fileName));
        texsToLoad.AddRange(mods.Where(mod => mod.type == ModType.kModTypeTexmod && mod.active && System.IO.File.Exists(mod.fileName)).Select(mod => mod.fileName));
        if (texsToLoad.Count > 0)
        {
            dllsToLoad.RemoveAll(p => Path.GetFileName(p) == "d3d9.dll"); // don't load any other d3d9.dll
            dllsToLoad.Add(Path.Combine(Directory.GetCurrentDirectory(), "d3d9.dll")); // load d3d9.dll for umod
        }

        return Tuple.Create(
            dllsToLoad.Distinct().OrderByDescending(p => Path.GetFileName(p) == "d3d9.dll").ThenBy(Path.GetFileName),
            texsToLoad.Distinct().OrderBy(Path.GetFileName)
        );
    }

    private enum GWML_FLAGS
    {
        NO_DATFIX = 1,
        KEEP_SUSPENDED = 2,
        NO_LOGIN = 4,
        ELEVATED = 8
    }

    private static string GetShortcutPath(string path)
    {
        var shell = new WshShell();
        var lnk = (IWshShortcut)shell.CreateShortcut(path);

        return lnk.TargetPath;
    }

    internal static class NativeMethods
    {
        [DllImport("GWML.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint LaunchClient(string client, string args, int flags, out IntPtr thread);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern uint ResumeThread(IntPtr hThread);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern uint CloseHandle(IntPtr handle);
    }
}