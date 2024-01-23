using GW_Launcher.uMod;
using Microsoft.Win32;
using static GW_Launcher.Memory.GWCAMemory;

namespace GW_Launcher;

internal class MulticlientPatch
{
    private static IntPtr GetProcessModuleBase(IntPtr process)
    {
        if (WinApi.NtQueryInformationProcess(process, PROCESSINFOCLASS.ProcessBasicInformation, out var pbi,
                Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out _) != 0)
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

    public static string? LaunchClient(Account account, out GWCAMemory? memory_out)
    {
        var path = account.gwpath;
        var character = " ";
        var error = "";
        Process? process = null;
        memory_out = null;
        if (!File.Exists(path))
            return $"Failed to find executable @ {path}";
        if (!string.IsNullOrEmpty(account.character))
            character = account.character;

        uModTexClient? texClient = null;

        if (ModManager.GetTexmods(account.gwpath, account.mods).Any())
        {
            if (Directory.GetFiles(@"\\.\pipe\", @"Game2uMod").Any())
                MessageBox.Show(@"uMod may be running in the background. Textures may not load.");
            texClient = new uModTexClient();
        }

        var args = $"-email \"{account.email}\" -password \"{account.password}\" -character \"{character}\" {account.extraargs}";
        //var args = $"-email \"{account.email}\" -password \"{account.password}\" {account.extraargs}";
        PatchRegistry(path);

        var pId = LaunchClient(path, args, account.elevated, out var hThread);
        if (pId == 0)
            return "Failed to run LaunchClient, last error = " + Marshal.GetLastWin32Error();
        process = Process.GetProcessById(pId);
        if (!McPatch(process.Handle))
            Debug.WriteLine("McPatch");

        GWCAMemory memory = new GWCAMemory(process);
        if (Control.ModifierKeys.HasFlag(Keys.Shift))
        {
            DialogResult result = MessageBox.Show("Guild Wars is in a suspended state, plugins are not yet loaded.\n\nContinue?", "Launching paused", MessageBoxButtons.OKCancel);
            if (result == DialogResult.Cancel)
                return "Launch was cancelled";
        }
        foreach (var dll in ModManager.GetDlls(path, account.mods))
        {
            var res = memory.LoadModule(dll);
            if(res != LoadModuleResult.SUCCESSFUL)
                return "Failed to load dll " + dll + " - LoadModuleResult " + res + " last error = " + Marshal.GetLastWin32Error();
        }
        if (Control.ModifierKeys.HasFlag(Keys.Shift))
        {
            DialogResult result = MessageBox.Show("Guild Wars is in a suspended state, plugins have been loaded.\n\nContinue?", "Launching paused", MessageBoxButtons.OKCancel);
            if (result == DialogResult.Cancel)
                return "Launch was cancelled";
        }
        if (hThread != IntPtr.Zero)
        {
            if(WinApi.ResumeThread(hThread) == 0xfffffffe)
                return "Failed to run WinApi.ResumeThread, last error = " + Marshal.GetLastWin32Error();
            if ((int)WinApi.CloseHandle(hThread) == -1)
                return "Failed to run WinApi.CloseHandle, last error = " + Marshal.GetLastWin32Error();
        }

        if (texClient != null)
        {
            var timeout = 0;
            bool ok = false;
            for (timeout = 0; timeout < 10 && !ok; timeout++)
            {
                ok = texClient.IsReady();
                if (!ok) Thread.Sleep(1000);
                memory.process.Refresh();
            }
            if (!ok)
            {
                error = "Failed to wait for uMod Client after " + timeout + " seconds.";
                goto finish_launch;
            }
            //texClient.Dispose();

            foreach (var tex in ModManager.GetTexmods(path, account.mods))
            {
                texClient.AddFile(tex);
            }

            texClient.Send();
        }

        finish_launch:
        if (error.Length > 0) {
            // Error occurred
            MessageBox.Show(error);
            if (process != null)
                process.Kill();
        }
        if (texClient != null)
            texClient.Dispose();
        GC.Collect(2, GCCollectionMode.Optimized); // force garbage collection

        memory_out = memory;
        return null;

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
            if (regSrc != null && (string)regSrc != Path.GetFullPath(path))
            {
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Src", Path.GetFullPath(path));
                Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", Path.GetFullPath(path));
            }

            regSrc = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src", null);
            if (regSrc == null || (string)regSrc == Path.GetFullPath(path))
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

    private static bool McPatch(IntPtr processHandle)
    {
        Debug.Assert(processHandle != IntPtr.Zero, "processHandle != IntPtr.Zero");
        byte[] sigPatch =
        {
            0x56, 0x57, 0x68, 0x00, 0x01, 0x00, 0x00, 0x89, 0x85, 0xF4, 0xFE, 0xFF, 0xFF, 0xC7, 0x00, 0x00, 0x00, 0x00,
            0x00
        };
        var moduleBase = GetProcessModuleBase(processHandle);
        var gwdata = new byte[0x48D000];

        if (!WinApi.ReadProcessMemory(processHandle, moduleBase, gwdata, gwdata.Length, out _))
        {
            return false;
        }

        var idx = SearchBytes(gwdata, sigPatch);

        if (idx == -1)
        {
            return false;
        }

        var mcpatch = moduleBase + idx - 0x1A;

        byte[] payload = { 0x31, 0xC0, 0x90, 0xC3 };

        return WinApi.WriteProcessMemory(processHandle, mcpatch, payload, payload.Length, out _);
    }

    private static int LaunchClient(string path, string args, bool elevated, out IntPtr hThread)
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
        Directory.SetCurrentDirectory(Path.GetDirectoryName(path)!);

        if (!elevated)
        {
            if (!WinSafer.SaferCreateLevel(SaferLevelScope.User, SaferLevel.NormalUser, SaferOpen.Open, out var hLevel,
                    IntPtr.Zero))
            {
                Debug.WriteLine("SaferCreateLevel");
                return 0;
            }

            if (!WinSafer.SaferComputeTokenFromLevel(hLevel, IntPtr.Zero, out var hRestrictedToken, 0, IntPtr.Zero))
            {
                Debug.WriteLine("SaferComputeTokenFromLevel");
                return 0;
            }

            WinSafer.SaferCloseLevel(hLevel);

            // Set the token to medium integrity.

            TOKEN_MANDATORY_LABEL tml;
            tml.Label.Attributes = 0x20; // SE_GROUP_INTEGRITY
            if (!WinSafer.ConvertStringSidToSid("S-1-16-8192", out tml.Label.Sid))
            {
                WinApi.CloseHandle(hRestrictedToken);
                Debug.WriteLine("ConvertStringSidToSid");
            }

            if (!WinSafer.SetTokenInformation(hRestrictedToken, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, ref tml,
                    (uint)Marshal.SizeOf(tml) + WinSafer.GetLengthSid(tml.Label.Sid)))
            {
                WinApi.LocalFree(tml.Label.Sid);
                WinApi.CloseHandle(hRestrictedToken);
                return 0;
            }

            if (!WinSafer.CreateProcessAsUser(hRestrictedToken, null!, commandLine, ref saProcess,
                    ref saProcess, false, (uint)CreationFlags.CreateSuspended, IntPtr.Zero,
                    null!, ref startinfo, out procinfo))
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"CreateProcessAsUser {error}");
                WinApi.CloseHandle(procinfo.hThread);
                return 0;
            }

            WinApi.CloseHandle(hRestrictedToken);
        }
        else
        {
            if (!WinApi.CreateProcess(null!, commandLine, ref saProcess,
                    ref saThread, false, (uint)CreationFlags.CreateSuspended, IntPtr.Zero,
                    null!, ref startinfo, out procinfo))
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"CreateProcess {error}");
                WinApi.ResumeThread(procinfo.hThread);
                WinApi.CloseHandle(procinfo.hThread);
                return 0;
            }
        }

        Directory.SetCurrentDirectory(lastDirectory);

        WinApi.CloseHandle(procinfo.hProcess);
        hThread = procinfo.hThread;
        return procinfo.dwProcessId;
    }
}
