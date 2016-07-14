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
using GWMultiLaunch;
using Binarysharp.Assemblers.Fasm;
using GW_Launcher;

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

    class MulticlientPatch : GWCAMemory
    {

        static public IntPtr mcmutexaddr = IntPtr.Zero;
        static public IntPtr datfix1 = IntPtr.Zero;
        static public IntPtr datfix2 = IntPtr.Zero;
        static public IntPtr datfix2retn = IntPtr.Zero;

        public MulticlientPatch(Process proc)
            :base(proc)
        {
            InitScanner(new IntPtr(0x401000), 0x49A000);
        }

        ~MulticlientPatch()
        {
           TerminateScanner();
        }

        public bool ApplyMulticlientPatch()
        {
            return HandleManager.ClearMutex();
        }

        public bool ApplyMulticlientPatch_old()
        {
            if (mcmutexaddr == IntPtr.Zero)
            {
                mcmutexaddr = ScanForPtr(new byte[] { 0x8B, 0xF0, 0x85, 0xF6, 0x74, 0x10, 0xFF }, 0x14);
                if (mcmutexaddr == IntPtr.Zero) return false;
            }
            WriteBytes(mcmutexaddr, new byte[] { 0xEB });

            return true;
        }

        public bool ApplyDatFix()
        {
            if (datfix1 == IntPtr.Zero)
            {
                // signature of .dat "OpenFile" call.
                datfix1 = ScanForPtr(new byte[] { 0x6A, 0x00, 0xBA, 0x00, 0x00, 0x00, 0xC0 }, 0x1);
                if (datfix1 == IntPtr.Zero) return false;
            }

            // Change handle to allow others to open I/O with the same .dat
            WriteBytes(datfix1, new byte[] { 0x03 });


            if (datfix2 == IntPtr.Zero)
            {
                // WriteFile call.
                datfix2 = ScanForPtr(new byte[] { 0x6A, 0x00, 0x52, 0x57, 0x50, 0x51 }, -0x23);
                if (datfix2 == IntPtr.Zero) return false;
                datfix2retn = (IntPtr)(datfix2.ToInt32() + 0x6);
            }

            IntPtr writehookbuffer = AllocMem(0x100);
            FasmNet asm = new FasmNet();

            asm.AddLine("use32");
            asm.AddLine("org {0}", writehookbuffer);

            asm.AddLine("mov eax,dword[{0}]", GWMem.DATInfo);
            asm.AddLine("cmp eax,0");
            asm.AddLine("je Trampoline");
            asm.AddLine("mov eax,dword[eax+0x10]");
            asm.AddLine("cmp eax,ecx");
            asm.AddLine("jne Trampoline");
            asm.AddLine("ret 0x10");

            asm.AddLine("Trampoline:");
            asm.AddLine("push ebp");
            asm.AddLine("mov ebp,esp");
            asm.AddLine("mov eax,dword[ebp+0x0C]");
            asm.AddLine("push {0}", datfix2retn);
            asm.AddLine("ret");

            byte[] bytecode = asm.Assemble();

            WriteBytes(writehookbuffer, bytecode);

            FasmNet jmp = new FasmNet();

            jmp.AddLine("use32");
            jmp.AddLine("push {0}", writehookbuffer);
            jmp.AddLine("ret");

            byte[] jmpcode = jmp.Assemble();

            WriteBytes(datfix2, jmpcode);

            // NOP it to avoid multiple writes, causing dat corruption
            //  WriteBytes(datfix2, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }); 





            return true;
        }

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

            STARTUPINFO startup = new STARTUPINFO();
            PROCESS_INFORMATION procinfo;

            bool createprocessresult = CreateProcess(
                path,
                "\"" + path + "\"" + args,
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
                MessageBox.Show("Unable to launch Gw.exe.", "GWMC - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            };
            


            Process proc = Process.GetProcessById((int)procinfo.dwProcessId);
            MulticlientPatch patch = new MulticlientPatch(proc);


            patch.ApplyMulticlientPatch_old();
            if (datfix) patch.ApplyDatFix();

            string dllpath = Path.GetDirectoryName(path) + "\\plugins";
            if (Directory.Exists(dllpath)) {
                string[] files = Directory.GetFiles(dllpath, "*.dll");
                foreach(string file in files)
                {
                    patch.LoadModule(file);
                }
            }

            ResumeThread(procinfo.hThread);

            return proc;
        }
    }
}
