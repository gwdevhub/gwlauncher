using System.Runtime.InteropServices;

namespace GwLauncher.Tray;

// Reads the global pointer position from X11. A tray click happens with the pointer over the icon,
// and the StatusNotifierItem click event doesn't carry coordinates, so this is the icon's location.
internal static class PointerLocator
{
    public static bool TryGetPosition(out int x, out int y)
    {
        x = y = 0;

        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var root = XDefaultRootWindow(display);
            var found = XQueryPointer(display, root, out _, out _, out x, out y, out _, out _, out _) != 0;
            return found;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryPointer(
        IntPtr display, IntPtr window,
        out IntPtr root, out IntPtr child,
        out int rootX, out int rootY,
        out int winX, out int winY,
        out uint mask);
}
