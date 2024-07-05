using System.Extensions;
using System.Threading;
using Microsoft.Win32;

namespace GW_Launcher;

internal class MulticlientPatch
{
    private static string GetErrorMessage(string methodName, int errorCode,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
    {
        return $"Error in {methodName} at {file}:{lineNumber} - Code: {errorCode}";
    }

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

    public static string? LaunchClient(Account account, out GWCAMemory? memory)
    {
        var path = account.gwpath;
        Process? process = null;
        memory = null;
        string? err;
        if (!File.Exists(path))
        {
            err = GetErrorMessage("Account Gw.exe path invalid", 0);
            goto cleanup;
        }

        var texmods = string.Join('\n', ModManager.GetTexmods(account));
        if (!texmods.IsNullOrEmpty())
        {
            var modfile = Path.Combine(Path.GetDirectoryName(path)!, "modlist.txt");
            try
            {
                File.WriteAllText(modfile, texmods);
            }
            catch (UnauthorizedAccessException)
            {
                modfile = Path.Combine(Directory.GetCurrentDirectory(), "modlist.txt");
                try
                {
                    File.WriteAllText(modfile, texmods);
                }
                catch (UnauthorizedAccessException)
                {
                    err = GetErrorMessage("UnauthorizedAccessException, Failed to write texmods to modlist.txt", 0);
                    goto cleanup;
                }

                ;
            }

            ;
        }

        var args = $"-email \"{account.email}\" -password \"{account.password}\"";

        if (!string.IsNullOrEmpty(account.character))
        {
            args += $" -character \"{account.character}\"";
        }

        args += $" {account.extraargs}";

        PatchRegistry(path);

        err = LaunchClient(path, args, account.elevated, out PROCESS_INFORMATION procinfo);
        if (err != null)
        {
            goto cleanup;
        }

        process = Process.GetProcessById(procinfo.dwProcessId);

        if (!McPatch(process.Handle))
        {
            err = GetErrorMessage("McPatch(process.Handle)", Marshal.GetLastWin32Error());
            goto cleanup;
        }

        memory = new GWCAMemory(process);

        foreach (var dll in ModManager.GetDlls(account))
        {
            var load_module_result = memory.LoadModule(dll);
            if (load_module_result != GWCAMemory.LoadModuleResult.SUCCESSFUL)
            {
                err = GetErrorMessage($"memory.LoadModule({dll})", Marshal.GetLastWin32Error());
                goto cleanup;
            }
        }

        if (Control.ModifierKeys.HasFlag(Keys.Shift))
        {
            DialogResult result =
                MessageBox.Show("Guild Wars is in a suspended state, plugins are not yet loaded.\n\nContinue?",
                    "Launching paused", MessageBoxButtons.OKCancel);
            if (result == DialogResult.Cancel)
            {
                GetErrorMessage("Launch was cancelled", 0);
                goto cleanup;
            }
        }

        if (procinfo.hThread != IntPtr.Zero)
        {
            try
            {
                if (WinApi.ResumeThread(procinfo.hThread) == 0xffffffff)
                {
                    err = GetErrorMessage($"WinApi.ResumeThread({procinfo.hThread})", Marshal.GetLastWin32Error());
                    goto cleanup;
                }

                if (WinApi.CloseHandle(procinfo.hThread) == 0)
                {
                    err = GetErrorMessage($"WinApi.CloseHandle({procinfo.hThread})", Marshal.GetLastWin32Error());
                    goto cleanup;
                }
            }
            catch (Exception e)
            {
                err = e.Message;
            }
        }

        cleanup:
        if (err != null)
        {
            process?.Kill();
            memory = null;
        }

        return err;
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

    private static string? PatchRegistry(string path)
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
                return null;
            }

            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Src",
                Path.GetFullPath(path));
            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path",
                Path.GetFullPath(path));
        }
        catch (UnauthorizedAccessException)
        {
            return GetErrorMessage("PatchRegistry UnauthorizedAccessException", Marshal.GetLastWin32Error());
        }

        return null;
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

    private static string? LaunchClient(string path, string args, bool elevated, out PROCESS_INFORMATION procinfo)
    {
        var commandLine = $"\"{path}\" {args}";

        procinfo = new PROCESS_INFORMATION();
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
                return GetErrorMessage("WinSafer.SaferCreateLevel", Marshal.GetLastWin32Error());

            if (!WinSafer.SaferComputeTokenFromLevel(hLevel, IntPtr.Zero, out var hRestrictedToken, 0, IntPtr.Zero))
                return GetErrorMessage("WinSafer.SaferComputeTokenFromLevel", Marshal.GetLastWin32Error());

            if (!WinSafer.SaferCloseLevel(hLevel))
            {
                WinApi.CloseHandle(hRestrictedToken);
                return GetErrorMessage("WinSafer.SaferCloseLevel", Marshal.GetLastWin32Error());
            }

            // Set the token to medium integrity.

            TOKEN_MANDATORY_LABEL tml;
            tml.Label.Attributes = 0x20; // SE_GROUP_INTEGRITY
            if (!WinSafer.ConvertStringSidToSid("S-1-16-8192", out tml.Label.Sid))
            {
                WinApi.CloseHandle(hRestrictedToken);
                return GetErrorMessage("WinSafer.ConvertStringSidToSid", Marshal.GetLastWin32Error());
            }

            if (!WinSafer.SetTokenInformation(hRestrictedToken, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, ref tml,
                    (uint)Marshal.SizeOf(tml) + WinSafer.GetLengthSid(tml.Label.Sid)))
            {
                WinApi.LocalFree(tml.Label.Sid);
                WinApi.CloseHandle(hRestrictedToken);
                return GetErrorMessage("WinSafer.SetTokenInformation", Marshal.GetLastWin32Error());
            }

            WinApi.LocalFree(tml.Label.Sid);

            if (!WinSafer.CreateProcessAsUser(hRestrictedToken, null!, commandLine, ref saProcess,
                    ref saProcess, false, (uint)CreationFlags.CreateSuspended, IntPtr.Zero,
                    null!, ref startinfo, out procinfo))
            {
                WinApi.CloseHandle(hRestrictedToken);
                return GetErrorMessage("WinSafer.CreateProcessAsUser", Marshal.GetLastWin32Error());
            }

            //WinApi.CloseHandle(procinfo.hThread);
            WinApi.CloseHandle(hRestrictedToken);
        }
        else
        {
            if (!WinApi.CreateProcess(null!, commandLine, ref saProcess,
                    ref saThread, false, (uint)CreationFlags.CreateSuspended, IntPtr.Zero,
                    null!, ref startinfo, out procinfo))
            {
                WinApi.ResumeThread(procinfo.hThread);
                WinApi.CloseHandle(procinfo.hThread);
                return GetErrorMessage("WinSafer.CreateProcess", Marshal.GetLastWin32Error());
            }
        }

        Directory.SetCurrentDirectory(lastDirectory);

        WinApi.CloseHandle(procinfo.hProcess);
        return null;
    }
}
