using System.Diagnostics;
using System.Runtime.InteropServices;
using GW_Launcher;
using GWCA.Memory;
using Microsoft.Win32;

namespace GWMC_CS;

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
                MessageBox.Show(@"Insufficient access rights.\nPlease restart the launcher as admin.",
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
        if (Directory.Exists(dllpath))
        {
            var files = Directory.GetFiles(dllpath, "*.dll");
            foreach (var file in files) mem.LoadModule(file);
        }

        dllpath = Path.GetDirectoryName(path) + "\\plugins";
        if (Directory.Exists(dllpath))
        {
            var files = Directory.GetFiles(dllpath, "*.dll");
            foreach (var file in files) mem.LoadModule(file);
        }

        if (mods != null)
            foreach (var mod in mods.Where(mod => mod.type == ModType.kModTypeDLL && File.Exists(mod.fileName)))
                mem.LoadModule(mod.fileName);

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