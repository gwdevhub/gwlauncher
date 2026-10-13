using Avalonia;
using Avalonia.Controls;
using GwLauncher.Linux;
using GwLauncher.Ui;

namespace GwLauncher.Tray;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        InjectorInstaller.Ensure();
        var host = new LinuxLauncherHost();
        Func<Window>? startup = null;

        if (args is ["-launch", var accountName])
        {
            var outcome = ShortcutLaunch.Start(host, accountName);
            if (outcome.Started)
            {
                return 0;
            }

            // A desktop click has no terminal, so the reason goes to a window too.
            if (outcome.Error != null)
            {
                Console.Error.WriteLine(outcome.Error);
            }

            startup = outcome.Window ?? (() => Dialogs.MessageWindow("GW Launcher", outcome.Error ?? "The account couldn't be started."));
        }

        return AppBuilder.Configure(() => new LauncherApp(host, startup))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }
}
