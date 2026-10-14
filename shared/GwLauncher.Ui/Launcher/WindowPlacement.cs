namespace GwLauncher.Ui;

public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class WindowPlacement
{
    // Beside the tray icon: centred on it, above a bottom panel or below a top one, and kept inside the work area.
    public static (int X, int Y) NextToIcon(ScreenRect icon, int width, int height, ScreenRect workArea, int gap = 4)
    {
        var x = Math.Clamp(icon.X + icon.Width / 2 - width / 2, workArea.X, Math.Max(workArea.X, workArea.Right - width));
        var iconInTopHalf = icon.Y + icon.Height / 2 < workArea.Y + workArea.Height / 2;
        var y = iconInTopHalf ? icon.Bottom + gap : icon.Y - height - gap;
        return (x, Math.Clamp(y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - height)));
    }

    // The tray flyout's width in logical pixels. It's placed with NextToIcon, so it stays this far from a screen edge it would pass.
    public const int TrayWidth = 200;

    // The size reduced to fit the work area, so a window never extends past the screen edge.
    public static (int Width, int Height) Fit(int width, int height, ScreenRect workArea) =>
        (Math.Min(width, workArea.Width), Math.Min(height, workArea.Height));

    // A menu opened at the pointer: its corner there, pulled back inside the work area if it would pass an edge.
    public static (int X, int Y) AtPoint(int x, int y, int width, int height, ScreenRect workArea) =>
        (Math.Clamp(x, workArea.X, Math.Max(workArea.X, workArea.Right - width)),
         Math.Clamp(y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - height)));

    public static (int X, int Y) Centred(int width, int height, ScreenRect workArea) =>
        (workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2);
}
