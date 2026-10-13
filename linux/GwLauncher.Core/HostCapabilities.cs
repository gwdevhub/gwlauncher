namespace GwLauncher.Linux;

// What the desktop session provides, so features are turned off up front instead of failing when used.
public static class HostCapabilities
{
    // Desktop shortcuts only show up where the Desktop folder is shown as icons.
    public static bool DesktopIcons => Directory.Exists(DesktopShortcut.DesktopDirectory());
}
