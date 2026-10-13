using Avalonia.Controls;

namespace GwLauncher.Tray;

// Gives every window the launcher's helmet icon, which the taskbar and window switcher show.
public abstract class AppWindow : Window
{
    protected AppWindow()
    {
        Icon = AppIcon.Window();
    }
}
