using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using GWCA.Memory;
using GW_Launcher;
using IWshRuntimeLibrary;
using File = System.IO.File;


namespace GWMC_CS
{
    internal class MulticlientPatch
    {
        private enum GWML_FLAGS
        {
            NO_DATFIX = 1,
            KEEP_SUSPENDED = 2,
            NO_LOGIN = 4,
            ELEVATED = 8
        };

        internal static class NativeMethods
        {

            [DllImport("GWML.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint LaunchClient(string client, string args, int flags, out IntPtr thread);

            [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
            public static extern uint ResumeThread(IntPtr hThread);

            [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
            public static extern uint CloseHandle(IntPtr handle);
        }

        public static GWCAMemory LaunchClient(string path, string args, bool datfix, bool nologin = false, bool elevated = false, List<Mod> mods = null)
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
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(path));
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(path));
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                if (elevated)
                {
                    MessageBox.Show("Insufficient access rights.\nPlease restart the launcher as admin.",
                        "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }

            var hThread = IntPtr.Zero;
            uint dwPid = NativeMethods.LaunchClient(path, args, ((int)GWML_FLAGS.KEEP_SUSPENDED | (datfix ? 0 : (int)GWML_FLAGS.NO_DATFIX) | (nologin ? (int)GWML_FLAGS.NO_LOGIN : 0) | (elevated ? (int)GWML_FLAGS.ELEVATED : 0)), out hThread);
            var proc = Process.GetProcessById((int)dwPid);
            var mem = new GWCAMemory(proc);
            if (mem.process.Threads[0].ThreadState == ThreadState.Wait && mem.process.Threads[0].WaitReason == ThreadWaitReason.Suspended)
            {
                try
                {
                    mem.process.Kill();
                    dwPid = NativeMethods.LaunchClient(path, args, ((int)GWML_FLAGS.KEEP_SUSPENDED | (datfix ? 0 : (int)GWML_FLAGS.NO_DATFIX) | (nologin ? (int)GWML_FLAGS.NO_LOGIN : 0) | (elevated ? (int)GWML_FLAGS.ELEVATED : 0)), out hThread);
                    proc = Process.GetProcessById((int)dwPid);
                    mem = new GWCAMemory(proc);
                }
                catch (Exception)
                {
                    MessageBox.Show("This Guild Wars executable is in a suspended state.\nPlease restart the launcher as admin.", "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    NativeMethods.ResumeThread(hThread);
                    NativeMethods.CloseHandle(hThread);
                    return null;
                }
            }
            string dllpath = Directory.GetCurrentDirectory() + "\\plugins";
            if (Directory.Exists(dllpath))
            {
                string[] links = Directory.GetFiles(dllpath, "*.lnk");
                string[] files = Directory.GetFiles(dllpath, "*.dll");
                foreach (string link in links)
                {
                    var shell = new WshShell();
                    var lnk = (IWshShortcut)shell.CreateShortcut(link);

                    if (lnk.TargetPath.EndsWith(".dll"))
                        mem.LoadModule(lnk.TargetPath);
                }
                foreach (string file in files)
                {
                    mem.LoadModule(file);
                }
            }

            dllpath = Path.GetDirectoryName(path) + "\\plugins";
            if (Directory.Exists(dllpath))
            {
                string[] links = Directory.GetFiles(dllpath, "*.lnk");
                string[] files = Directory.GetFiles(dllpath, "*.dll");
                foreach (string link in links)
                {
                    var shell = new WshShell();
                    var lnk = (IWshShortcut)shell.CreateShortcut(link);

                    if (lnk.TargetPath.EndsWith(".dll"))
                        mem.LoadModule(lnk.TargetPath);
                }
                foreach (string file in files)
                {
                    mem.LoadModule(file);
                }
            }

            if (mods != null)
            {
                foreach (Mod mod in mods.Where(mod => mod.type == ModType.kModTypeDLL && File.Exists(mod.fileName)))
                {
                    mem.LoadModule(mod.fileName);
                }
            }

            NativeMethods.ResumeThread(hThread);
            NativeMethods.CloseHandle(hThread);

            return mem;
        }
    }
}
