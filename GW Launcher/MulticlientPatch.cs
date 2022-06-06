using GW_Launcher.uMod;
using Microsoft.Win32;

namespace GW_Launcher;

internal static class ProcessExtension
{
    private enum ThreadAccess : int
    {
        TERMINATE = (0x0001),
        SUSPEND_RESUME = (0x0002),
        GET_CONTEXT = (0x0008),
        SET_CONTEXT = (0x0010),
        SET_INFORMATION = (0x0020),
        QUERY_INFORMATION = (0x0040),
        SET_THREAD_TOKEN = (0x0080),
        IMPERSONATE = (0x0100),
        DIRECT_IMPERSONATION = (0x0200)
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
    [DllImport("kernel32.dll")]
    static extern uint SuspendThread(IntPtr hThread);
    [DllImport("kernel32.dll")]
    static extern int ResumeThread(IntPtr hThread);

    public static void Suspend(this Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            var pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
            if (pOpenThread == IntPtr.Zero)
            {
                break;
            }
            SuspendThread(pOpenThread);
        }
    }
    public static void Resume(this Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            var pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
            if (pOpenThread == IntPtr.Zero)
            {
                break;
            }
            ResumeThread(pOpenThread);
        }
    }
}

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

        if (ModManager.GetTexmods(account.gwpath, account.mods).Any())
        {
            Task.Run(() =>
            {
                using var texClient = new uModTexClient();
                while (!texClient.IsReady() && !Program.shouldClose)
                {
                    Thread.Sleep(200);
                }
                foreach (var tex in ModManager.GetTexmods(path, account.mods))
                {
                    if (Program.shouldClose)
                    {
                        texClient.Dispose();
                        return;
                    }

                    texClient.AddFile(tex);
                }
                texClient.Send();

                GC.Collect(2, GCCollectionMode.Optimized); // force garbage collection
            });
        }

        var args = $"-email \"{account.email}\" -password \"{account.password}\" -character \"{character}\" {account.extraargs}";

        PatchRegistry(path);

        var lastDirectory = Directory.GetCurrentDirectory();

        Directory.SetCurrentDirectory(Path.GetDirectoryName(path)!);

        var process = account.elevated ? Process.Start(path, args) : RunAsDesktopUser(path, args);

        process.Suspend();

        Directory.SetCurrentDirectory(lastDirectory);

        if (!McPatch(process))
        {
        }

        var memory = new GWCAMemory(process);

        // make sure umod d3d9.dll is loaded BEFORE the game loads the original d3d9.dll
        foreach (var dll in ModManager.GetPreloadDlls(path, account.mods))
        {
            memory.LoadModule(dll, false);
        }

        process.Resume();

        foreach (var dll in ModManager.GetDlls(path, account.mods).Where(d => Path.GetFileName(d) != "d3d9.dll"))
        {
            memory.LoadModule(dll);
        }


        return memory;
    }

    internal static GWCAMemory LaunchClient(string path)
    {
        PatchRegistry(path);

        var lastDirectory = Directory.GetCurrentDirectory();

        Directory.SetCurrentDirectory(Path.GetDirectoryName(path)!);

        var process = Process.Start("explorer.exe", path);

        process.Suspend();

        Directory.SetCurrentDirectory(lastDirectory);

        McPatch(process);
        process.Resume();

        return new GWCAMemory(process);
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
            if (regSrc == null || (string)regSrc == Path.GetFullPath(path)) return;
            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src",
                Path.GetFullPath(path));
            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path",
                Path.GetFullPath(path));
        }
        catch (UnauthorizedAccessException)
        {

        }
    }

    internal static class WinApi
    {
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern uint ResumeThread(IntPtr hThread);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern uint CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern bool ReadProcessMemory(IntPtr hProcess,
            IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern bool WriteProcessMemory(IntPtr hProcess,
            IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
    }
    private static int SearchBytes(IReadOnlyList<byte> haystack, IReadOnlyList<byte> needle)
    {
        var len = needle.Count;
        var limit = haystack.Count - len;
        for (var i = 0; i <= limit; i++)
        {
            var k = 0;
            for (; k < len; k++)
            {
                if (needle[k] != haystack[i + k]) break;
            }
            if (k == len) return i;
        }
        return -1;
    }

    private static bool McPatch(Process process)
    {
        Debug.Assert(process.MainModule != null, "process.MainModule != null");
        byte[] sigPatch =
        {
            0x56, 0x57, 0x68, 0x00, 0x01, 0x00, 0x00, 0x89, 0x85, 0xF4, 0xFE, 0xFF, 0xFF, 0xC7, 0x00, 0x00, 0x00, 0x00,
            0x00
        };
        var moduleBase = process.MainModule.BaseAddress;
        var gwdata = new byte[0x48D000];

        if (!WinApi.ReadProcessMemory(process.Handle, moduleBase, gwdata, gwdata.Length, out var bytesRead))
        {
            return false;
        }

        var idx = SearchBytes(gwdata, sigPatch);

        if (idx == -1)
            return false;

        var mcpatch = moduleBase + idx - 0x1A;

        byte[] payload = { 0x31, 0xC0, 0x90, 0xC3 };

        return WinApi.WriteProcessMemory(process.Handle, mcpatch, payload, payload.Length, out var bytesWritten);
    }

    #region Interop

    private struct TOKEN_PRIVILEGES
    {
        public UInt32 PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public UInt32 Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
    }

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public Int32 cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public Int32 dwX;
        public Int32 dwY;
        public Int32 dwXSize;
        public Int32 dwYSize;
        public Int32 dwXCountChars;
        public Int32 dwYCountChars;
        public Int32 dwFillAttribute;
        public Int32 dwFlags;
        public Int16 wShowWindow;
        public Int16 cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }
    #endregion

    private Process RunAsUser(string path, string args, uint flag)
    {
        
        STARTUPINFOW startinfo = { 0 };
        PROCESS_INFORMATION procinfo = { 0 };

        WCHAR last_directory[MAX_PATH];
        GetCurrentDirectoryW(MAX_PATH, last_directory);
        WCHAR* trial = wcsstr(path, L"Gw.exe");
        trial[0] = L'\0';
        SetCurrentDirectoryW(path);

        if (!(flags & GWML_ELEVATED))
        {
            SAFER_LEVEL_HANDLE hLevel = NULL;
            if (!SaferCreateLevel(SAFER_SCOPEID_USER, SAFER_LEVELID_NORMALUSER, SAFER_LEVEL_OPEN, &hLevel, NULL))
            {
                MCERROR("SaferCreateLevel");
            }

            HANDLE hRestrictedToken = NULL;
            if (!SaferComputeTokenFromLevel(hLevel, NULL, &hRestrictedToken, 0, NULL))
            {
                SaferCloseLevel(hLevel);
                MCERROR("SaferComputeTokenFromLevel");
            }

            SaferCloseLevel(hLevel);

            // Set the token to medium integrity.

            TOKEN_MANDATORY_LABEL tml = { 0 };
            tml.Label.Attributes = SE_GROUP_INTEGRITY;
            if (!ConvertStringSidToSid(TEXT("S-1-16-8192"), &(tml.Label.Sid)))
            {
                CloseHandle(hRestrictedToken);
                MCERROR("ConvertStringSidToSid");
            }

            if (!SetTokenInformation(hRestrictedToken, TokenIntegrityLevel, &tml, sizeof(tml) + GetLengthSid(tml.Label.Sid)))
            {
                LocalFree(tml.Label.Sid);
                CloseHandle(hRestrictedToken);
                return FALSE;
            }

            if (!CreateProcessAsUserW(hRestrictedToken, NULL, commandLine, NULL, NULL, FALSE, CREATE_SUSPENDED, NULL, NULL, &startinfo, &procinfo))
            {
                MCERROR("CreateProcessAsUserW");
            }

            CloseHandle(hRestrictedToken);
        }
        else
        {
            if (!CreateProcessW(NULL, commandLine, NULL, NULL, FALSE, CREATE_SUSPENDED, NULL, NULL, &startinfo, &procinfo))
                MCERROR("CreateProcessW");
        }

        SetCurrentDirectoryW(last_directory);
        trial[0] = L'G';

        g_moduleBase = GetProcessModuleBase(procinfo.hProcess);

        if (!MCPatch(procinfo.hProcess))
        {
            ResumeThread(procinfo.hThread);
            CloseHandle(procinfo.hThread);
            CloseHandle(procinfo.hProcess);
            MCERROR("MCPatch");
        }

        if (out_hThread != NULL)
            *out_hThread = (uint)procinfo.hThread;

        CloseHandle(procinfo.hProcess);
        return procinfo.dwProcessId;
    }
}
