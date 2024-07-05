namespace GW_Launcher.Utilities;

internal static class WinApi
{
    [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
    internal static extern bool CreateProcess(
        string lpApplicationName, string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
        ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
    internal static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
    internal static extern uint CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
    internal static extern bool ReadProcessMemory(IntPtr hProcess,
        IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
    internal static extern bool WriteProcessMemory(IntPtr hProcess,
        IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
    internal static extern int NtQueryInformationProcess(IntPtr hProcess, PROCESSINFOCLASS pic,
        out PROCESS_BASIC_INFORMATION pbi, int cb, out int pSize);

}

internal static class WinSafer
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
    public static extern bool SetTokenInformation(IntPtr tokenHandle,
        TOKEN_INFORMATION_CLASS tokenInformationokenInformationClass,
        ref TOKEN_MANDATORY_LABEL tokenInformation, uint tokenInformationLength);

    [DllImport("advapi32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
    public static extern bool ConvertStringSidToSid(string stringSid, out IntPtr ptrSid);

    [DllImport("advapi32.dll")]
    internal static extern uint GetLengthSid(IntPtr pSid);


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

internal enum SaferLevel : uint
{
    Disallowed = 0,
    Untrusted = 0x1000,
    Constrained = 0x10000,
    NormalUser = 0x20000,
    FullyTrusted = 0x40000
}

internal enum SaferLevelScope : uint
{
    Machine = 1,
    User = 2
}

internal enum SaferOpen : uint
{
    Open = 1
}

internal enum SaferTokenBehaviour : uint
{
    Default = 0x0,
    NullIfEqual = 0x1,
    CompareOnly = 0x2,
    MakeInert = 0x4,
    WantFlags = 0x8
}

internal enum TOKEN_INFORMATION_CLASS : uint
{
    TokenUser = 1,
    TokenGroups,
    TokenPrivileges,
    TokenOwner,
    TokenPrimaryGroup,
    TokenDefaultDacl,
    TokenSource,
    TokenType,
    TokenImpersonationLevel,
    TokenStatistics,
    TokenRestrictedSids,
    TokenSessionId,
    TokenGroupsAndPrivileges,
    TokenSessionReference,
    TokenSandBoxInert,
    TokenAuditPolicy,
    TokenOrigin,
    TokenElevationType,
    TokenLinkedToken,
    TokenElevation,
    TokenHasRestrictions,
    TokenAccessInformation,
    TokenVirtualizationAllowed,
    TokenVirtualizationEnabled,
    TokenIntegrityLevel,
    TokenUiAccess,
    TokenMandatoryPolicy,
    TokenLogonSid,
    MaxTokenInfoClass
}

[Flags]
internal enum CreationFlags : uint
{
    CreateSuspended = 0x00000004,
    DetachedProcess = 0x00000008,
    CreateNoWindow = 0x08000000,
    ExtendedStartupInfoPresent = 0x00080000
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

[StructLayout(LayoutKind.Sequential)]
internal struct PEB
{
    private readonly byte InheritedAddressSpace;
    private readonly byte ReadImageFileExecOptions;
    private readonly byte BeingDebugged;
    private readonly byte BitField;
    private readonly IntPtr Mutant;
    internal IntPtr ImageBaseAddress;
}

internal enum PROCESSINFOCLASS : uint
{
    ProcessBasicInformation = 0x00,
    ProcessDebugPort = 0x07,
    ProcessExceptionPort = 0x08,
    ProcessAccessToken = 0x09,
    ProcessWow64Information = 0x1A,
    ProcessImageFileName = 0x1B,
    ProcessDebugObjectHandle = 0x1E,
    ProcessDebugFlags = 0x1F,
    ProcessExecuteFlags = 0x22,
    ProcessInstrumentationCallback = 0x28,
    MaxProcessInfoClass = 0x64
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROCESS_BASIC_INFORMATION
{
    private readonly IntPtr Reserved1;
    internal IntPtr PebBaseAddress;
    private readonly IntPtr Reserved2;
    private readonly IntPtr Reserved3;
    private readonly UIntPtr UniqueProcessId;
    private readonly IntPtr Reserved4;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SID_AND_ATTRIBUTES
{
    public IntPtr Sid;
    public int Attributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SID_IDENTIFIER_AUTHORITY
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public byte[] Value;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_MANDATORY_LABEL
{
    public SID_AND_ATTRIBUTES Label;
}
