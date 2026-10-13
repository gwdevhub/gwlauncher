using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// The bar and detail line that every progress view shows. Report() is the only way it changes, so each view
// (the progress dialog, the setup window) drives it the same way.
public sealed class ProgressPanel : StackPanel
{
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, IsIndeterminate = true };
    private readonly TextBlock _detail = new() { FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap };

    public ProgressPanel()
    {
        Spacing = 8;
        Children.Add(_bar);
        Children.Add(_detail);
    }

    public void Report(ProgressUpdate update)
    {
        if (update.Fraction is { } fraction)
        {
            _bar.IsIndeterminate = false;
            _bar.Value = Math.Clamp(fraction, 0, 1) * 100;
        }
        else
        {
            _bar.IsIndeterminate = true;
        }

        _detail.Text = update.Detail;
    }
}

// Small control factories shared by the progress and decompress windows.
public static class Ui
{
    public static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Content = text, MinWidth = 100, Padding = new Avalonia.Thickness(12, 6) };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static StackPanel ButtonRow(params Button[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        row.Children.AddRange(buttons);
        return row;
    }
}
