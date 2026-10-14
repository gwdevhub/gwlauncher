using GwLauncher.Ui;
using GW_Launcher.Platform;

namespace GW_Launcher;

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";

    // Read by the memory code for the module-load timeout, and saved by the settings page.
    public static GlobalSettings Settings = GlobalSettings.Load();

    // Held by the first instance. Closed before a self-update swaps the exe.
    internal static Mutex? GwlMutex;

    [STAThread]
    internal static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var accountName = LaunchArgument(args);
        var isFirstInstance = AcquireInstanceMutex();
        var host = new WindowsLauncherHost();

        // A desktop shortcut starts its account without the window, so it works even before WebView2 is installed.
        object? startup = null;
        if (accountName != null)
        {
            startup = ShortcutLaunch.Start(host, accountName);
            if (startup == null)
            {
                return 0;
            }
        }
        else if (!isFirstInstance)
        {
            return 0;
        }

        if (!RendererCheck.Ensure())
        {
            return 1;
        }

        using var shell = new WebViewShell(host);
        var bridge = new LauncherBridge(host, shell, onClosed => new WebViewShell(host, onClosed)) { Startup = startup };
        StartShell(shell, bridge, startup != null);
        Application.Run();
        return 0;
    }

    private static async void StartShell(WebViewShell shell, LauncherBridge bridge, bool showAtStart)
    {
        try
        {
            await shell.Start(showAtStart);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"GW Launcher couldn't open its window: {ex.Message}", "GW Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
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
