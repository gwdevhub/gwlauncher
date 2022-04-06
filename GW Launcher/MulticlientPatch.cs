using System.Diagnostics;
using System.Runtime.InteropServices;
using GW_Launcher.Utilities;
using Microsoft.Win32;
using IWshRuntimeLibrary;

namespace GW_Launcher;

internal class MulticlientPatch
{
    public static GWCAMemory LaunchClient(string path, string args, bool datfix, bool nologin = false,
        bool elevated = false, List<Mod>? mods = null)
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
                MessageBox.Show("Insufficient access rights.\nPlease restart the launcher as admin.",
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

        var dllpath = Directory.GetCurrentDirectory() + "\\plugins";
        var to_load = new List<string>();
        if (Directory.Exists(dllpath))
        {
            var links = Directory.GetFiles(dllpath, "*.lnk");
            var files = Directory.GetFiles(dllpath, "*.dll");
            links = links.Select(l => GetShortcutPath(l)).Where(l => l.EndsWith(".dll")).ToArray();
            to_load.AddRange(links);
            to_load.AddRange(files);
        }

        dllpath = Path.GetDirectoryName(path) + "\\plugins";
        if (Directory.Exists(dllpath))
        {
            var links = Directory.GetFiles(dllpath, "*.lnk");
            var files = Directory.GetFiles(dllpath, "*.dll");
            links = links.Select(l => GetShortcutPath(l)).Where(l => l.EndsWith(".dll")).ToArray();
            to_load.AddRange(links);
            to_load.AddRange(files);
        }

        if (mods != null)
            foreach (var mod in mods.Where(mod => mod.type == ModType.kModTypeDLL && System.IO.File.Exists(mod.fileName)))
                to_load.Add(mod.fileName);

        foreach (var dll in to_load.Distinct())
        {
            mem.LoadModule(dll);
        }

        NativeMethods.ResumeThread(hThread);
        NativeMethods.CloseHandle(hThread);

        return mem;
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