using Avalonia;
using Avalonia.Controls;
using GwLauncher.Ui;
using GW_Launcher.Platform;

namespace GW_Launcher;

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";

    // Read by the memory code for the module-load timeout, and saved by the settings window.
    public static GlobalSettings Settings = GlobalSettings.Load();

    // Held by the first instance. Closed before a self-update swaps the exe.
    internal static Mutex? GwlMutex;

    [STAThread]
    internal static int Main(string[] args)
    {
        var accountName = LaunchArgument(args);
        var isFirstInstance = AcquireInstanceMutex();
        var host = new WindowsLauncherHost();

        Func<Window>? startup = null;
        if (accountName != null)
        {
            // A desktop shortcut starts the account without the launcher window, and shows a window only when it can't.
            var outcome = ShortcutLaunch.Start(host, accountName);
            if (outcome.Started)
            {
                return 0;
            }

            startup = outcome.Window ?? (() => Dialogs.MessageWindow("GW Launcher", outcome.Error ?? "The account couldn't be started."));
        }
        else if (!isFirstInstance)
        {
            return 0;
        }

        return AppBuilder.Configure(() => new LauncherApp(host, startup))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    private static string? LaunchArgument(string[] args)
    {
        var index = Array.IndexOf(args, "-launch");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    // True for the first instance. A second instance finds the mutex and leaves the first one running.
    private static bool AcquireInstanceMutex()
    {
        if (Mutex.TryOpenExisting(GwlMutexName, out var existing))
        {
            existing.Close();
            return false;
        }

        GwlMutex = new Mutex(true, GwlMutexName);
        return true;
    }
}
