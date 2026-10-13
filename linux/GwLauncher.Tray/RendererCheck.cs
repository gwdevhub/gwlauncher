using System.Runtime.InteropServices;
using GwLauncher.Linux;
using GwLauncher.Tray.Native;

namespace GwLauncher.Tray;

// The window is drawn by WebKitGTK. When it's missing, this asks in a plain GTK dialog and installs it through pkexec.
internal static class RendererCheck
{
    private const string Manual = "Install the WebKitGTK 4.1 package (libwebkit2gtk-4.1) with your package manager, then start GW Launcher again.";

    public static bool Ensure()
    {
        if (IsInstalled())
        {
            return true;
        }

        var command = Distro.InstallCommand(
            "apt-get update && apt-get install -y libwebkit2gtk-4.1-0",
            "dnf install -y webkit2gtk4.1",
            "pacman -S --noconfirm webkit2gtk-4.1",
            "zypper --non-interactive install libwebkit2gtk-4_1-0");
        if (command == null)
        {
            Tell($"GW Launcher needs WebKitGTK 4.1 to show its window, and it isn't installed.\n\n{Manual}");
            return false;
        }

        if (!Ask("GW Launcher needs WebKitGTK 4.1 to show its window, and it isn't installed.\n\nInstall it now? You'll be asked for your password."))
        {
            return false;
        }

        var exitCode = RunAsRoot(command);
        if (IsInstalled())
        {
            return true;
        }

        Tell($"WebKitGTK wasn't installed (the install exited with code {exitCode}).\n\n{Manual}");
        return false;
    }

    private static bool IsInstalled() => NativeLibrary.TryLoad(Gtk.WebKitLibrary, out _);

    private static int RunAsRoot(string command)
    {
        using var process = Process.Start(new ProcessStartInfo("pkexec") { ArgumentList = { "sh", "-c", command }, UseShellExecute = false });
        if (process == null)
        {
            return -1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static bool Ask(string text)
    {
        var dialog = Dialog(text, type: 2);
        Gtk.gtk_dialog_add_button(dialog, "Cancel", 0);
        Gtk.gtk_dialog_add_button(dialog, "Install", 1);
        return Run(dialog) == 1;
    }

    private static void Tell(string text)
    {
        var dialog = Dialog(text, type: 3);
        Gtk.gtk_dialog_add_button(dialog, "OK", 0);
        Run(dialog);
    }

    // The text goes in as the dialog's format string, so a literal % has to be doubled.
    private static IntPtr Dialog(string text, int type)
    {
        var dialog = Gtk.gtk_message_dialog_new(IntPtr.Zero, 0, type, 0, text.Replace("%", "%%"));
        Gtk.gtk_window_set_title(dialog, "GW Launcher");
        return dialog;
    }

    private static int Run(IntPtr dialog)
    {
        var response = Gtk.gtk_dialog_run(dialog);
        Gtk.gtk_widget_destroy(dialog);
        Gtk.Pump();
        return response;
    }
}
