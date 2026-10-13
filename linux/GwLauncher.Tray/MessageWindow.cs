using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input.Platform;
using Avalonia.Media;

namespace GwLauncher.Tray;

// A message on screen for errors, with the launcher and OS versions and a button that copies it all for a bug report.
public sealed class MessageWindow : AppWindow
{
    private readonly string _report;

    private MessageWindow(string title, string text)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _report = BuildReport(title, text);

        var copy = new Button { Content = "Copy", MinWidth = 90, Padding = new Thickness(12, 6) };
        copy.Click += async (_, _) =>
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(_report);
                copy.Content = "Copied";
            }
        };

        var ok = new Button { Content = "OK", MinWidth = 90, Padding = new Thickness(12, 6) };
        ok.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick },
                new TextBlock
                {
                    Text = VersionLine(),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Opacity = 0.7
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { copy, ok }
                }
            }
        };
    }

    // Shows the message. It's unowned, because the launcher hides when it loses focus and would take an owned message with it.
    public static void Show(Window? owner, string title, string text)
    {
        new MessageWindow(title, text).Show();
    }

    private static string VersionLine()
    {
        return $"GW Launcher {LauncherVersion()} · {OsName()} · kernel {Environment.OSVersion.Version}";
    }

    private static string BuildReport(string title, string text)
    {
        return $"{title}\n{text}\n\n{VersionLine()}";
    }

    private static string LauncherVersion()
    {
        return typeof(MessageWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    }

    private static string OsName()
    {
        try
        {
            foreach (var line in File.ReadAllLines("/etc/os-release"))
            {
                if (line.StartsWith("PRETTY_NAME="))
                {
                    return line["PRETTY_NAME=".Length..].Trim('"');
                }
            }
        }
        catch (IOException)
        {
            // Fall through to the generic name.
        }

        return "Linux";
    }
}
