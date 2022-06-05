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
        var elevated = account.elevated;

        PatchRegistry(path);

        var lastDirectory = Directory.GetCurrentDirectory();

        Directory.SetCurrentDirectory(Path.GetDirectoryName(path)!);

        var process = elevated ? Process.Start("explorer.exe", path) : Process.Start(path, args);

        process.Suspend();

        Directory.SetCurrentDirectory(lastDirectory);

        MCPatch(process);
        
        var memory = new GWCAMemory(process);

        foreach (var dll in ModManager.GetDlls(path, account.mods))
        {
            memory.LoadModule(dll);
        }

        process.Resume();

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

        MCPatch(process);
        process.Resume();

        return new GWCAMemory(process);
    }

    private static void PatchRegistry(string path)
    {
        try
        {
            var regSrc = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", null);
            if (regSrc != null && (string) regSrc != Path.GetFullPath(path))
            {
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(path));
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(path));
            }

            regSrc = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", null);
            if (regSrc == null || (string) regSrc == Path.GetFullPath(path)) return;
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

    private static bool MCPatch(Process process)
    {
        Debug.Assert(process.MainModule != null, "process.MainModule != null");
        byte[] sigPatch =
        {
            0x56, 0x57, 0x68, 0x00, 0x01, 0x00, 0x00, 0x89, 0x85, 0xF4, 0xFE, 0xFF, 0xFF, 0xC7, 0x00, 0x00, 0x00, 0x00,
            0x00
        };
        var moduleBase = process.MainModule.BaseAddress;
        var gwdata = new byte[0x48D000];

        if (!WinApi.ReadProcessMemory(process.Handle, moduleBase, gwdata, 0x48D000, out var bytesRead))
        {
            return false;
        }

        var idx = SearchBytes(gwdata, sigPatch);

        if (idx == -1)
            return false;

        var mcpatch = moduleBase + idx - 0x1A;

        byte[] payload = {0x31, 0xC0, 0x90, 0xC3};

        return WinApi.WriteProcessMemory(process.Handle, mcpatch, payload, payload.Length, out var bytesWritten);
    }
}