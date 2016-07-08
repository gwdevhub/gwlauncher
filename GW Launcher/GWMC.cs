using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;
using System.Windows.Forms;
using GWCA.Memory;
using Microsoft.Win32;

namespace GWMC_CS
{
    public struct STARTUPINFO
    {
        public uint cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [Flags]
    public enum ProcessCreationFlags : uint
    {
        ZERO_FLAG = 0x00000000,
        CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
        CREATE_DEFAULT_ERROR_MODE = 0x04000000,
        CREATE_NEW_CONSOLE = 0x00000010,
        CREATE_NEW_PROCESS_GROUP = 0x00000200,
        CREATE_NO_WINDOW = 0x08000000,
        CREATE_PROTECTED_PROCESS = 0x00040000,
        CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
        CREATE_SEPARATE_WOW_VDM = 0x00001000,
        CREATE_SHARED_WOW_VDM = 0x00001000,
        CREATE_SUSPENDED = 0x00000004,
        CREATE_UNICODE_ENVIRONMENT = 0x00000400,
        DEBUG_ONLY_THIS_PROCESS = 0x00000002,
        DEBUG_PROCESS = 0x00000001,
        DETACHED_PROCESS = 0x00000008,
        EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
        INHERIT_PARENT_AFFINITY = 0x00010000
    }

    class GWMC
    {

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreateProcess(string lpApplicationName,
               string lpCommandLine, IntPtr lpProcessAttributes,
               IntPtr lpThreadAttributes,
               bool bInheritHandles, ProcessCreationFlags dwCreationFlags,
               IntPtr lpEnvironment, string lpCurrentDirectory,
               ref STARTUPINFO lpStartupInfo,
               out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        public static extern uint ResumeThread(IntPtr hThread);

        static void GenerateINI(INI_Reader ini)
        {
            ini.IniWriteValue("MCPatch", "gwpath", "Gw.exe");
            ini.IniWriteValue("MCPatch", "enabled", "1");
            ini.IniWriteValue("MCPatch", "datfix", "1");
            ini.IniWriteValue("LoadDLL", "enabled", "0");
            ini.IniWriteValue("LoadDLL", "dllpath", "");
        }

        static void Main()
        {
            INI_Reader settings = new INI_Reader(Environment.CurrentDirectory + "\\GWMC.ini");

            if (!File.Exists(Environment.CurrentDirectory + "\\GWMC.ini"))
                GenerateINI(settings);

            string gwpath = settings.IniReadValue("MCPatch", "gwpath");


            if (gwpath == "Gw.exe")
                gwpath = Environment.CurrentDirectory + "\\Gw.exe";

            try
            {
                if (Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", null) != null)
                {
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(gwpath));
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(gwpath));
                }

                if (Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", null) != null)
                {
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(gwpath));
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(gwpath));
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                MessageBox.Show("Insufficient access rights.\nPlease restart the launcher as admin.", "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
           

            if (!File.Exists(Environment.CurrentDirectory + "\\GWMC.ini"))
                GenerateINI(settings);

            STARTUPINFO startup = new STARTUPINFO();
            PROCESS_INFORMATION procinfo;
            
            bool createprocessresult = CreateProcess(
                gwpath,
                Environment.CommandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                ProcessCreationFlags.CREATE_SUSPENDED,
                IntPtr.Zero,
                null,
                ref startup,
                out procinfo);

            if (createprocessresult == false)
            {
                MessageBox.Show("Unable to launch Gw.exe.\nIs the path correct in the ini file?", "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Process gwproc = Process.GetProcessById((int)procinfo.dwProcessId);

            GWCAMemory memmgr = new GWCAMemory(gwproc);

            MulticlientPatch patcher = new MulticlientPatch(memmgr);

            if (settings.IniReadValue("MCPatch","enabled") == "1")
              patcher.ApplyMulticlientPatch();

            if (settings.IniReadValue("MCPatch", "datfix") == "1")
              patcher.ApplyDatFix();

            if (settings.IniReadValue("LoadDLL", "enabled") == "1")
                memmgr.LoadModule(settings.IniReadValue("LoadDLL", "dllpath"));

            ResumeThread(procinfo.hThread);
        }
    }
}