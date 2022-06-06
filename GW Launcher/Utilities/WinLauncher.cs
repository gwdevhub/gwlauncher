namespace GW_Launcher.Utilities;
internal class WinLauncher
{
    private enum SaferLevel : uint
    {
        Disallowed = 0,
        Untrusted = 0x1000,
        Constrained = 0x10000,
        NormalUser = 0x20000,
        FullyTrusted = 0x40000
    }

    private enum SaferLevelScope : uint
    {
        Machine = 1,
        User = 2
    }

    private enum SaferOpen : uint
    {
        Open = 1
    }

    private enum SaferTokenBehaviour : uint
    {
        Default = 0x0,
        NullIfEqual = 0x1,
        CompareOnly = 0x2,
        MakeInert = 0x4,
        WantFlags = 0x8
    }

    internal static Process? RunAsUser(string path, string args, bool elevated, out IntPtr hThread)
    {
        var commandLine = $"\"{path}\" {args}";
        hThread = IntPtr.Zero;

        PROCESS_INFORMATION procinfo;
        STARTUPINFO startinfo = new()
        {
            cb = Marshal.SizeOf(typeof(STARTUPINFO))
        };
        var saProcess = new SECURITY_ATTRIBUTES();
        saProcess.nLength = (uint)Marshal.SizeOf(saProcess);
        var saThread = new SECURITY_ATTRIBUTES();
        saThread.nLength = (uint)Marshal.SizeOf(saThread);

        var lastDirectory = Directory.GetCurrentDirectory();
        var newDirectory = Path.GetDirectoryName(path);
        Directory.SetCurrentDirectory(newDirectory);

        if (!elevated)
        {
            if (!WinSafer.SaferCreateLevel(SaferLevelScope.User, SaferLevel.NormalUser, SaferOpen.Open, out var hLevel,
                    IntPtr.Zero))
            {
                Debug.WriteLine("SaferCreateLevel");
                return null;
            }

            if (!WinSafer.SaferComputeTokenFromLevel(hLevel, IntPtr.Zero, out var hRestrictedToken, 0, IntPtr.Zero))
            {
                Debug.WriteLine("SaferComputeTokenFromLevel");
                return null;
            }

            WinSafer.SaferCloseLevel(hLevel);

            // Set the token to medium integrity.

            //TOKEN_MANDATORY_LABEL tml = { 0 };
            //tml.Label.Attributes = SE_GROUP_INTEGRITY;
            //if (!ConvertStringSidToSid(TEXT("S-1-16-8192"), &(tml.Label.Sid)))
            //{
            //    CloseHandle(hRestrictedToken);
            //    Debug.WriteLine("ConvertStringSidToSid");
            //}

            //if (!SetTokenInformation(hRestrictedToken, TokenIntegrityLevel, &tml, sizeof(tml) + GetLengthSid(tml.Label.Sid)))
            //{
            //    LocalFree(tml.Label.Sid);
            //    CloseHandle(hRestrictedToken);
            //    return FALSE;
            //}


            if (!WinSafer.CreateProcessAsUser(hRestrictedToken, string.Empty, commandLine, ref saProcess,
                    ref saProcess, false, 0x00000004 /*CREATE_SUSPENDED*/, IntPtr.Zero,
                    string.Empty, ref startinfo, out procinfo))
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"CreateProcessAsUser {error}");
                return null;
            }

            WinApi.CloseHandle(hRestrictedToken);
        }
        else
        {
            if (!WinApi.CreateProcess(string.Empty, commandLine, ref saProcess,
                    ref saThread, false, 0x00000004 /*CREATE_SUSPENDED*/, IntPtr.Zero,
                    string.Empty, ref startinfo, out procinfo))
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"CreateProcess {error}");
                return null;
            }
        }

        Directory.SetCurrentDirectory(lastDirectory);

        hThread = procinfo.hThread;
        var process = Process.GetProcessById(procinfo.dwProcessId);

        WinApi.CloseHandle(procinfo.hThread);
        WinApi.CloseHandle(procinfo.hProcess);
        return process;
    }

    private static class WinApi
    {
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern uint CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        internal static extern bool CreateProcess(
            string lpApplicationName, string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);
    }

    private static class WinSafer
    {
        [DllImport("advapi32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        public static extern bool SaferCreateLevel(SaferLevelScope scopeId, SaferLevel levelId, SaferOpen openFlags,
            out IntPtr levelHandle, IntPtr reserved);

        [DllImport("advapi32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        public static extern bool SaferComputeTokenFromLevel(IntPtr levelHandle, IntPtr inAccessToken,
            out IntPtr outAccessToken, SaferTokenBehaviour flags, IntPtr lpReserved);

        [DllImport("advapi32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        public static extern bool SaferCloseLevel(IntPtr levelHandle);


        [DllImport("advapi32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);
    }

    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct LUID_AND_ATTRIBUTES
    {
        public readonly LUID Luid;
        public readonly uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public readonly uint LowPart;
        public readonly int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_ATTRIBUTES
    {
        public uint nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }
}
