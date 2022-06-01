using GW_Launcher.uMod;
using Microsoft.Win32;
using IWshRuntimeLibrary;

namespace GW_Launcher;

internal class MulticlientPatch
{
    public static GWCAMemory LaunchClient(Account a)
    {
        if (GetTexmods(a.gwpath, a.mods).Any())
        {
            a.texClient = new uModTexClient();
        }
        return LaunchClient(a.gwpath,
            " -email \"" + a.email + "\" -password \"" + a.password + "\" -character \"" +
            a.character + "\" " + a.extraargs, a.datfix, false, a.elevated, a.mods, a.texClient);
    }

    public static GWCAMemory LaunchClient(string path, string args, bool datfix, bool nologin = false,
        bool elevated = false, List<Mod>? mods = null, uModTexClient? texClient = null)
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
            if (elevated)
            {
                MessageBox.Show(@"Insufficient access rights.
Please restart the launcher as admin.",
                    @"GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        var hThread = IntPtr.Zero;
        var dwPid = NativeMethods.LaunchClient(path, args,
            (int)GWML_FLAGS.KEEP_SUSPENDED | (datfix ? 0 : (int)GWML_FLAGS.NO_DATFIX) |
            (nologin ? (int)GWML_FLAGS.NO_LOGIN : 0) | (elevated ? (int)GWML_FLAGS.ELEVATED : 0), out hThread);
        var proc = Process.GetProcessById((int)dwPid);
        var mem = new GWCAMemory(proc);


        foreach (var dll in GetDlls(path, mods))
        {
            mem.LoadModule(dll);
        }

        foreach (var tex in GetTexmods(path, mods))
        {
            texClient?.AddFile(tex);
        }

        NativeMethods.ResumeThread(hThread);
        NativeMethods.CloseHandle(hThread);

        return mem;
    }

    private static IEnumerable<string> GetDlls(string path, List<Mod>? mods = null)
    {
        return GetMods(path, mods).Item1;
    }

    private static IEnumerable<string> GetTexmods(string path, List<Mod>? mods = null)
    {
        return GetMods(path, mods).Item2;
    }
    private static Tuple<IEnumerable<string>, IEnumerable<string>> GetMods(string path, IReadOnlyCollection<Mod>? mods = null)
    {
        var directory = Directory.GetCurrentDirectory() + "\\plugins";
        var dllsToLoad = new List<string>();
        var texsToLoad = new List<string>();
        if (Directory.Exists(directory))
        {
            var links = Directory.GetFiles(directory, "*.lnk");
            var files = Directory.GetFiles(directory, "*.dll");
            var dlllinks = links.Select(GetShortcutPath).Where(l => l.EndsWith(".dll")).ToArray();
            var texlinks = links.Select(GetShortcutPath).Where(l => l.EndsWith(".zip") || l.EndsWith(".tpf")).ToArray();

            dllsToLoad.AddRange(dlllinks);
            dllsToLoad.AddRange(files);
            texsToLoad.AddRange(texlinks);
        }

        directory = Path.GetDirectoryName(path) + "\\plugins";
        if (Directory.Exists(directory))
        {
            var links = Directory.GetFiles(directory, "*.lnk");
            var files = Directory.GetFiles(directory, "*.dll");
            var dlllinks = links.Select(GetShortcutPath).Where(l => l.EndsWith(".dll")).ToArray();
            var texlinks = links.Select(GetShortcutPath).Where(l => l.EndsWith(".zip") || l.EndsWith(".tpf")).ToArray();

            dllsToLoad.AddRange(dlllinks);
            dllsToLoad.AddRange(files);
            texsToLoad.AddRange(texlinks);
        }

        if (mods != null) dllsToLoad.AddRange(mods.Where(mod => mod.type == ModType.kModTypeDLL && System.IO.File.Exists(mod.fileName)).Select(mod => mod.fileName));
        if (mods != null) texsToLoad.AddRange(mods.Where(mod => mod.type == ModType.kModTypeTexmod && System.IO.File.Exists(mod.fileName)).Select(mod => mod.fileName));
        if (texsToLoad.Count > 0)
        {
            dllsToLoad.Add(Path.Combine(Directory.GetCurrentDirectory(), "d3d9.dll")); // load d3d9.dll for umod
        }

        return Tuple.Create(dllsToLoad.Distinct(), texsToLoad.Distinct());
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