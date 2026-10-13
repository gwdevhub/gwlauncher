using System.Runtime.InteropServices;
using System.Text;

namespace GW_Launcher.Platform;

// Starts a Guild Wars client on Windows: the steam_appid file, the multiclient patch, then waits until the window is ready.
internal static class WindowsGame
{
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

    // Returns the error, or null when the game is up.
    public static string? Launch(Account account)
    {
        var result = DeleteSteamAppIdFile(account) ?? CreateSteamAppIdFile(account);
        if (result != null)
        {
            return result;
        }

        result = LaunchClient(account);
        DeleteSteamAppIdFile(account);
        return result;
    }

    private static string? CreateSteamAppIdFile(Account account)
    {
        var gwDirectory = Path.GetDirectoryName(account.gwpath);
        if (string.IsNullOrEmpty(gwDirectory))
        {
            return "Failed to determine Guild Wars directory path.";
        }

        if (account.is_steam_account)
        {
            try
            {
                File.WriteAllText(Path.Combine(gwDirectory, "steam_appid.txt"), "29720");
            }
            catch (Exception ex)
            {
                return $"Failed to create steam_appid.txt: {ex.Message}";
            }
        }

        return null;
    }

    private static string? DeleteSteamAppIdFile(Account account)
    {
        var gwDirectory = Path.GetDirectoryName(account.gwpath);
        if (string.IsNullOrEmpty(gwDirectory))
        {
            return "Failed to determine Guild Wars directory path.";
        }

        var steamAppIdPath = Path.Combine(gwDirectory, "steam_appid.txt");
        if (File.Exists(steamAppIdPath))
        {
            File.Delete(steamAppIdPath);
        }

        return null;
    }

    private static string? LaunchClient(Account account)
    {
        if (!File.Exists(account.gwpath))
        {
            return "Path to the Guild Wars executable incorrect, aborting launch.";
        }

        if (account.is_steam_account && Process.GetProcessesByName("steam").Length == 0)
        {
            return "Steam is not running. Please start Steam before launching a Steam account.";
        }

        GWCAMemory? memory = account.process;
        if (memory == null)
        {
            var toolboxPlugin = ModManager.FindUnsupportedToolboxPlugin(account);
            if (toolboxPlugin != null)
            {
                return "GWToolboxdll.dll was found in a plugins folder:\n" + toolboxPlugin +
                       "\n\nGWToolboxdll.dll is only supported through the account's mod list, or as a shortcut in the plugins folder pointing to GWToolboxdll.dll. Remove the file and add it one of those ways instead.";
            }

            try
            {
                if (IsProcessOpen(account.gwpath))
                {
                    return "The Guild Wars executable at " + account.gwpath + " is already running";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            var res = MulticlientPatch.LaunchClient(account, out memory);
            if (res != null)
            {
                return res;
            }
        }

        if (memory == null)
        {
            return "Failed to launch account.";
        }

        var game = memory;
        uint timeout = 10000;
        var ok = WaitFor(() =>
        {
            game.process.Refresh();
            return game.process.MainWindowHandle != IntPtr.Zero;
        }, timeout);
        if (!ok)
        {
            game.process.Kill();
            return "Failed to wait for MainWindowHandle after " + (timeout / 1000) + " seconds.";
        }

        ok = WaitFor(() =>
        {
            game.process.Refresh();
            return game.process.WaitForInputIdle(1000);
        }, timeout);
        if (!ok)
        {
            game.process.Kill();
            return "Failed to wait for WaitForInputIdle after " + (timeout / 1000) + " seconds.";
        }

        SetForegroundWindow(game.process.MainWindowHandle);
        account.process = game;

        GWMemory.FindAddressesIfNeeded(game);
        ok = WaitFor(() => game.Read<ushort>(GWMemory.CharnamePtr) != 0 && game.process.Responding, timeout);
        if (!ok)
        {
            Console.WriteLine("Failed to wait for CharnamePtr after " + (timeout / 1000) + " seconds.");
        }

        timeout = 5000;
        ok = WaitFor(() =>
        {
            game.process.Refresh();
            return game.process.MainWindowTitle != "";
        }, timeout);
        if (ok && (game.process.MainWindowTitle == "Guild Wars" || game.process.MainWindowTitle == "Guild Wars Reforged"))
        {
            // The window may not be ready for the title change, or the game may be resetting its title while it renders.
            ok = WaitFor(() =>
            {
                game.process.Refresh();
                var chars = Marshal.StringToHGlobalAnsi(account.Name);
                SendMessage(game.process.MainWindowHandle, 0xc, 0, chars);
                game.process.Refresh();
                return game.process.MainWindowTitle != "Guild Wars" && game.process.MainWindowTitle != "Guild Wars Reforged";
            }, timeout);
        }

        if (!game.process.Responding)
        {
            game.process.Kill();
            return "Failed to wait for process to respond after " + (timeout / 1000) + " seconds.";
        }

        return null;
    }

    private static bool WaitFor(Func<bool> testFunc, uint timeoutMs = 10000)
    {
        uint elapsed = 0;
        var ok = testFunc();
        while (!ok && elapsed <= timeoutMs)
        {
            Thread.Sleep(200);
            elapsed += 200;
            ok = testFunc();
        }

        return ok;
    }

    private static bool IsProcessOpen(string name)
    {
        var basename = Path.GetFileNameWithoutExtension(name);
        foreach (var process in Process.GetProcesses())
        {
            if (!process.ProcessName.Equals(basename))
            {
                continue;
            }

            var fileNameBuilder = new StringBuilder(1024);
            var bufferLength = (uint)fileNameBuilder.Capacity + 1;
            var path = QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength) ? fileNameBuilder.ToString() : null;
            if (path != null && path.Contains(name))
            {
                return true;
            }
        }

        return false;
    }
}
