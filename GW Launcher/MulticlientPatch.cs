using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using GWCA.Memory;
using GW_Launcher;

namespace GWMC_CS
{

    class MulticlientPatch
    {
        enum GWML_FLAGS {
            NO_DATFIX        = 1,
            KEEP_SUSPENDED   = 2,
            NO_LOGIN         = 4,
            ELEVATED         = 8
        };

        [DllImport("GWML.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern uint LaunchClient(string client, string args, GWML_FLAGS flags, out IntPtr thread);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint ResumeThread(IntPtr hThread);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint CloseHandle(IntPtr handle);

        public static GWCAMemory LaunchClient(string path, string args, bool datfix, bool nologin = false, bool elevated = false, List<Mod> mods = null)
        {
            try
            {
                if (Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", null) != null)
                {
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(path));
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(path));
                }

                if (Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", null) != null)
                {
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(path));
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(path));
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                MessageBox.Show("Insufficient access rights.\nPlease restart the launcher as admin.", "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            IntPtr hThread = IntPtr.Zero;
            uint dwPid = LaunchClient(path, args, (GWML_FLAGS)((int)GWML_FLAGS.KEEP_SUSPENDED | (datfix ? 0 : (int)GWML_FLAGS.NO_DATFIX) | (nologin ? (int)GWML_FLAGS.NO_LOGIN : 0) | (elevated ? (int)GWML_FLAGS.ELEVATED : 0)), out hThread);
            var proc = Process.GetProcessById((int)dwPid);
            var mem = new GWCAMemory(proc);
            //if (mem.process.Threads[0].ThreadState == ThreadState.Wait && mem.process.Threads[0].WaitReason == ThreadWaitReason.Suspended)
            //{
            //    try
            //    {
            //        mem.process.Kill();
            //        dwPID = LaunchClient(path, args, (GWML_FLAGS)((datfix ? 2 : 3) | (nologin ? 4 : 0)), out hThread);
            //        proc = Process.GetProcessById((int)dwPID);
            //        mem = new GWCAMemory(proc);
            //    }
            //    catch (Exception)
            //    {
            //        MessageBox.Show("This Guild Wars executable is in a suspended state.\nPlease restart the launcher as admin.", "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        ResumeThread(hThread);
            //        CloseHandle(hThread);
            //        return null;
            //    }
            //}
            string dllpath = Directory.GetCurrentDirectory() + "\\plugins";
            if (Directory.Exists(dllpath))
            {
                string[] files = Directory.GetFiles(dllpath, "*.dll");
                foreach (string file in files)
                {
                    mem.LoadModule(file);
                }
            }

            dllpath = Path.GetDirectoryName(path) + "\\plugins";
            if (Directory.Exists(dllpath)) {
                string[] files = Directory.GetFiles(dllpath, "*.dll");
                foreach(string file in files)
                {
                    mem.LoadModule(file);
                }
            }

            if(mods != null)
            {
                foreach (var mod in mods)
                {
                    if (mod.type == ModType.kModTypeDLL && File.Exists(mod.fileName))
                        mem.LoadModule(mod.fileName);
                }
            }
            
            ResumeThread(hThread);
            CloseHandle(hThread);

            return mem;
        }
    }
}
