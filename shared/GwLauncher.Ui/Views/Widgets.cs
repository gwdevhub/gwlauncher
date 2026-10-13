using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GwLauncher.Ui;

// Gives every window the launcher's helmet icon, which the taskbar and window switcher show.
public class AppWindow : Window
{
    public AppWindow()
    {
        Icon = AppIcon.Window();
    }
}

public static class AppIcon
{
    private static readonly Uri Asset = new("avares://GwLauncher.Ui/Assets/gwlauncher-helmet.png");

    public static WindowIcon Window() => new(Avalonia.Platform.AssetLoader.Open(Asset));

    public static void SaveTo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var source = Avalonia.Platform.AssetLoader.Open(Asset);
        using var target = File.Create(path);
        source.CopyTo(target);
    }
}

// Control factories shared by every window.
public static class Widgets
{
    public static Button Button(string text, Action onClick, double minWidth = 90)
    {
        var button = new Button { Content = text, MinWidth = minWidth, Padding = new Thickness(12, 6) };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        row.Children.AddRange(children);
        return row;
    }

    public static TextBlock Text(string text, double fontSize = 0, bool wrap = true) => new()
    {
        Text = text,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        FontSize = fontSize == 0 ? 14 : fontSize,
        Opacity = fontSize == 0 ? 1 : 0.7
    };

    public static TextBlock Error(string text = "") => new() { Text = text, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
}
