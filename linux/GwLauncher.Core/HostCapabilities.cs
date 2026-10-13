namespace GwLauncher.Linux;

// What the desktop session provides, so features are turned off up front instead of failing when used.
public static class HostCapabilities
{
    private static readonly string[] TrayWatcherNames =
    [
        "org.kde.StatusNotifierWatcher",
        "org.freedesktop.StatusNotifierWatcher",
        "org.x.StatusNotifierWatcher"
    ];

    private static readonly Lazy<bool> TrayHostLazy = new(ProbeTrayHost);

    // Tray icons need a StatusNotifier host on the session bus. Without one the icon never appears.
    public static bool TrayHost => TrayHostLazy.Value;

    // Desktop shortcuts only show up where the Desktop folder is shown as icons.
    public static bool DesktopIcons => Directory.Exists(DesktopShortcut.DesktopDirectory());

    private static bool ProbeTrayHost()
    {
        // Without gdbus the bus can't be checked, so assume a host rather than turning the tray off.
        if (!CommandExists("gdbus"))
        {
            return true;
        }

        return TrayWatcherNames.Any(WatcherOnBus);
    }

    private static bool WatcherOnBus(string name)
    {
        try
        {
            var psi = new ProcessStartInfo("gdbus")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("introspect");
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add("--dest");
            psi.ArgumentList.Add(name);
            psi.ArgumentList.Add("--object-path");
            psi.ArgumentList.Add("/StatusNotifierWatcher");

            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(2000))
            {
                process.Kill();
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool CommandExists(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, command)));
    }
}
