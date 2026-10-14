using GwLauncher.Linux;
using GwLauncher.Tray.Native;
using GwLauncher.Ui;

namespace GwLauncher.Tray;

public static class Program
{
    public static int Main(string[] args)
    {
        InjectorInstaller.Ensure();

        try
        {
            if (!Gtk.gtk_init_check(IntPtr.Zero, IntPtr.Zero))
            {
                Console.Error.WriteLine("GW Launcher needs a graphical session: GTK couldn't open the display.");
                return 1;
            }
        }
        catch (DllNotFoundException)
        {
            Console.Error.WriteLine("GW Launcher needs GTK 3. Install the libgtk-3 package with your package manager.");
            return 1;
        }

        if (!RendererCheck.Ensure())
        {
            return 1;
        }

        var host = new LinuxLauncherHost();
        object? startup = null;
        if (args is ["-launch", var accountName])
        {
            startup = ShortcutLaunch.Start(host, accountName);
            if (startup == null)
            {
                return 0;
            }
        }

        var shell = new GtkShell(host);
        var bridge = new LauncherBridge(host, shell, onClosed => new GtkShell(host, onClosed)) { Startup = startup };
        shell.Run(showAtStart: startup != null);
        return 0;
    }
}
