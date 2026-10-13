using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GwLauncher.Tray;

public sealed class ConfirmWindow : AppWindow
{
    public ConfirmWindow(string title, string message, Action onYes)
    {
        Title = title;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 300;
        SizeToContent = SizeToContent.Height;
        CanMinimize = false;
        CanMaximize = false;
        CanResize = false;

        var yes = new Button { Content = "Yes", MinWidth = 80, Padding = new Thickness(12, 6) };
        yes.Click += (_, _) =>
        {
            Close();
            onYes();
        };
        var no = new Button { Content = "No", MinWidth = 80, Padding = new Thickness(12, 6) };
        no.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes }
                }
            }
        };
    }
}
