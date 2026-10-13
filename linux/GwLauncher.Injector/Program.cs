using System.Runtime.InteropServices;
using System.Text;

namespace GwLauncher.Injector;

// Starts a game suspended, loads each DLL into it with LoadLibraryW, then resumes it.
// Usage: GwLauncher.Injector --dll <path>... [--timeout <ms>] -- <exe> [args...]
public static class Program
{
    private const uint CreateSuspended = 0x00000004;
    private const uint MemCommitReserve = 0x3000;
    private const uint MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04;
    private const uint WaitTimeout = 0x102;

    public static int Main(string[] args)
    {
        var separator = Array.IndexOf(args, "--");
        if (separator < 0 || separator == args.Length - 1)
        {
            Console.Error.WriteLine("usage: GwLauncher.Injector --dll <path>... [--timeout <ms>] -- <exe> [args...]");
            return 2;
        }

        var dlls = new List<string>();
        var timeoutMs = 5000u;
        for (var i = 0; i < separator; i++)
        {
            if (args[i] == "--dll" && i + 1 < separator)
            {
                dlls.Add(args[++i]);
            }
            else if (args[i] == "--timeout" && i + 1 < separator && uint.TryParse(args[i + 1], out var parsed))
            {
                timeoutMs = parsed;
                i++;
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: {args[i]}");
                return 2;
            }
        }

        var gameCommand = args[(separator + 1)..];
        var commandLine = string.Join(' ', gameCommand.Select(Quote));
        var gameDirectory = Path.GetDirectoryName(gameCommand[0]);

        var startup = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        if (!CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false,
                CreateSuspended, IntPtr.Zero, gameDirectory, ref startup, out var process))
        {
            Console.Error.WriteLine($"Could not start the game (error {Marshal.GetLastWin32Error()}).");
            return 1;
        }

        var allLoaded = true;
        foreach (var dll in dlls)
        {
            var loaded = InjectDll(process.hProcess, dll, timeoutMs);
            Console.WriteLine($"{(loaded ? "Loaded" : "FAILED")} {dll}");
            allLoaded &= loaded;
        }

        ResumeThread(process.hThread);
        Console.WriteLine(allLoaded ? "Game resumed." : "Game resumed, but some DLLs did not load.");

        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return allLoaded ? 0 : 1;
    }

    private static bool InjectDll(IntPtr process, string dllPath, uint timeoutMs)
    {
        var bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
        var remote = VirtualAllocEx(process, IntPtr.Zero, (IntPtr)bytes.Length, MemCommitReserve, PageReadWrite);
        if (remote == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!WriteProcessMemory(process, remote, bytes, (IntPtr)bytes.Length, out _))
            {
                return false;
            }

            var kernel32 = GetModuleHandleW("kernel32.dll");
            var loadLibrary = GetProcAddress(kernel32, "LoadLibraryW");
            var thread = CreateRemoteThread(process, IntPtr.Zero, 0, loadLibrary, remote, 0, out _);
            if (thread == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (WaitForSingleObject(thread, timeoutMs) == WaitTimeout)
                {
                    return false;
                }

                // LoadLibraryW's return value is the module handle, which is zero when the load failed.
                return GetExitCodeThread(thread, out var moduleHandle) && moduleHandle != 0;
            }
            finally
            {
                CloseHandle(thread);
            }
        }
        finally
        {
            VirtualFreeEx(process, remote, IntPtr.Zero, MemRelease);
        }
    }

    // Windows command-line quoting, so arguments with spaces survive the trip to CreateProcess.
    private static string Quote(string argument)
    {
        if (argument.Length > 0 && !argument.Any(c => c == ' ' || c == '\t' || c == '"'))
        {
            return argument;
        }

        return "\"" + argument.Replace("\"", "\\\"") + "\"";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string? application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment, string? currentDirectory,
        ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, IntPtr size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, IntPtr size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr written);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr threadAttributes, uint stackSize,
        IntPtr startAddress, IntPtr parameter, uint creationFlags, out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeThread(IntPtr thread, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
