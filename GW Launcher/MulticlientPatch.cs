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
            NO_LOGIN		 = 4
        };

        [DllImport("GWML.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        static extern uint LaunchClient(string client, string email, string pass, string charname, string args, GWML_FLAGS flags, out IntPtr thread);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint ResumeThread(IntPtr hThread);

        static public Process LaunchClient(string path,string args,bool datfix)
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
            uint dwPID = LaunchClient(path, null, null, null, args, (GWML_FLAGS)(datfix ? 2 : 3), out hThread);





            Process proc = Process.GetProcessById((int)dwPID);
            GWCAMemory mem = new GWCAMemory(proc);

            string dllpath = Path.GetDirectoryName(path) + "\\plugins";
            if (Directory.Exists(dllpath)) {
                string[] files = Directory.GetFiles(dllpath, "*.dll");
                foreach(string file in files)
                {
                    mem.LoadModule(file);
                }
            }

            ResumeThread(hThread);

            return proc;
        }
    }
}
