using GW_Launcher.Classes;
using GW_Launcher.Memory;
using GW_Launcher.uMod;
using Microsoft.Win32;

namespace GW_Launcher;

internal class MulticlientPatch
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PEB
    {
        byte InheritedAddressSpace;
        byte ReadImageFileExecOptions;
        byte BeingDebugged;
        byte BitField;
        IntPtr Mutant;
        internal IntPtr ImageBaseAddress;
    }

    private enum PROCESSINFOCLASS : uint
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
    private struct PROCESS_BASIC_INFORMATION
    {
        IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        IntPtr Reserved2;
        IntPtr Reserved3;
        UIntPtr UniqueProcessId;
        IntPtr Reserved4;
    }
    private static IntPtr GetProcessModuleBase(IntPtr process)
    {
        if (WinApi.NtQueryInformationProcess(process, PROCESSINFOCLASS.ProcessBasicInformation, out var pbi, Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out _) != 0)
        {
            return IntPtr.Zero;
        }

        var buffer = new byte[Marshal.SizeOf(typeof(PEB))];

        if (!WinApi.ReadProcessMemory(process, pbi.PebBaseAddress, buffer, Marshal.SizeOf(typeof(PEB)), out _))
        {
            return IntPtr.Zero;
        }

        PEB peb = new()
        {
            ImageBaseAddress = (IntPtr)BitConverter.ToInt32(buffer, 8)
        };

        return peb.ImageBaseAddress + 0x1000;
    }

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

        var args =
            $"-email \"{account.email}\" -password \"{account.password}\" -character \"{character}\" {account.extraargs}";

        PatchRegistry(path);

        var pId = WinLauncher.RunAsUser(path, args, account.elevated, out var hThread);
        Debug.Assert(pId != 0, "pId != 0");
        var process = Process.GetProcessById(pId);
        //process.Suspend();
        
        if (!McPatch(process.Handle))
        {
            Debug.WriteLine("McPatch");
        }

        var memory = new GWCAMemory(process);

        // make sure umod d3d9.dll is loaded BEFORE the game loads the original d3d9.dll
        foreach (var dll in ModManager.GetPreloadDlls(path, account.mods))
        {
            memory.LoadModule(dll, false);
        }

        WinApi.ResumeThread(hThread);
        WinApi.CloseHandle(hThread);
        //process.Resume();

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

        McPatch(process.Handle);
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
            if (regSrc == null || (string) regSrc == Path.GetFullPath(path))
            {
                return;
            }

            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src",
                Path.GetFullPath(path));
            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path",
                Path.GetFullPath(path));
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine("PatchRegistry");
        }
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
                if (needle[k] != haystack[i + k])
                {
                    break;
                }
            }

            if (k == len)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool McPatch(IntPtr handle)
    {
        //Debug.Assert(process.MainModule != null, "process.MainModule != null");
        byte[] sigPatch =
        {
            0x56, 0x57, 0x68, 0x00, 0x01, 0x00, 0x00, 0x89, 0x85, 0xF4, 0xFE, 0xFF, 0xFF, 0xC7, 0x00, 0x00, 0x00, 0x00,
            0x00
        };
        var moduleBase = GetProcessModuleBase(handle);
        var gwdata = new byte[0x48D000];

        if (!WinApi.ReadProcessMemory(handle, moduleBase, gwdata, gwdata.Length, out _))
        {
            return false;
        }

        var idx = SearchBytes(gwdata, sigPatch);

        if (idx == -1)
        {
            return false;
        }

        var mcpatch = moduleBase + idx - 0x1A;

        byte[] payload = {0x31, 0xC0, 0x90, 0xC3};

        return WinApi.WriteProcessMemory(handle, mcpatch, payload, payload.Length, out _);
    }

    private static class WinApi
    {
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern uint CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern bool ReadProcessMemory(IntPtr hProcess,
            IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        internal static extern bool WriteProcessMemory(IntPtr hProcess,
            IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        internal static extern int NtQueryInformationProcess(IntPtr hProcess, PROCESSINFOCLASS pic, out PROCESS_BASIC_INFORMATION pbi, int cb, out int pSize);
    }

}
