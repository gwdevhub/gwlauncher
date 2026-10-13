using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// Centred setup window listing every prerequisite. Runs one step at a time, and closing it doesn't stop a running step.
public sealed class SetupWindow : AppWindow
{
    private readonly StackPanel _steps = new() { Spacing = 10 };
    private readonly TextBlock _heading = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressPanel _progress = new() { IsVisible = false };
    private readonly Button _actionButton = new() { MinWidth = 120, Padding = new Thickness(12, 6) };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private SetupStep? _runningStep;

    public SetupWindow()
    {
        Title = "GW Launcher setup";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        CanMinimize = false;
        CanMaximize = false;

        _actionButton.Click += (_, _) => RunNextStep();
        _timer.Tick += (_, _) => Poll();

        var closeButton = new Button { Content = "Close", MinWidth = 90, Padding = new Thickness(12, 6), HorizontalAlignment = HorizontalAlignment.Right };
        closeButton.Click += (_, _) => Hide();

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children = { _heading, _steps, _message, _progress, _actionButton, closeButton }
        };

        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    public void ShowCentered()
    {
        Refresh();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Show();
        Activate();
    }

    public void Refresh()
    {
        var satisfied = Setup.Steps.Select(step => step.IsSatisfied()).ToList();
        var nextIndex = satisfied.IndexOf(false);

        _steps.Children.Clear();
        for (var i = 0; i < Setup.Steps.Count; i++)
        {
            var step = Setup.Steps[i];
            var done = satisfied[i];
            var status = new TextBlock
            {
                Text = done ? "✓" : "○",
                FontSize = 16,
                Foreground = done ? Brushes.SeaGreen : Brushes.Gray,
                Margin = new Thickness(0, 2, 10, 0)
            };
            var text = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = step.Title, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = step.Description, TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.7 }
                }
            };
            _steps.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { status, text },
                Opacity = done || i == nextIndex ? 1 : 0.5
            });
        }

        if (nextIndex < 0)
        {
            _heading.Text = "Setup complete";
            _message.Text = "Click the tray icon to open the launcher.";
            _actionButton.IsVisible = false;
            return;
        }

        var next = Setup.Steps[nextIndex];
        var command = next.Command();

        _heading.Text = "GW Launcher needs a few things before it can run accounts.";
        _actionButton.IsVisible = true;
        _actionButton.IsEnabled = command != null;
        _actionButton.Content = $"Install {next.Title}";
        _message.Text = command == null
            ? $"{next.Title} can't be installed automatically on this distro. Install it with your package manager, then reopen this window."
            : "Click install. If asked, enter your password in the system prompt.";

        if (SetupRunner.InProgress())
        {
            BeginProgress();
        }
    }

    private void RunNextStep()
    {
        var step = Setup.Steps.FirstOrDefault(s => !s.IsSatisfied());
        var command = step?.Command();
        if (step == null || command == null)
        {
            return;
        }

        try
        {
            SetupRunner.Start(command, step.NeedsRoot);
        }
        catch (Exception ex)
        {
            _message.Text = $"Could not start the {step.Title} install: {ex.Message}";
            return;
        }

        _runningStep = step;
        BeginProgress();
    }

    private void BeginProgress()
    {
        _actionButton.IsEnabled = false;
        _progress.IsVisible = true;
        _message.Text = "Installing. This can take several minutes. You can close this window; the install keeps going.";
        ShowLastLogLine();

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    // The install runs as a detached process, so its progress is the last line of its log, read on each poll.
    private void ShowLastLogLine()
    {
        _progress.Report(new ProgressUpdate(null, SetupRunner.LastLogLine()));
    }

    private void Poll()
    {
        ShowLastLogLine();

        if (SetupRunner.InProgress())
        {
            return;
        }

        _timer.Stop();
        SetupRunner.ClearMarker();
        _progress.IsVisible = false;

        // A reattached run has no step object from this session, so it's judged by the refresh below.
        var failed = _runningStep is { } step && !step.IsSatisfied();
        _runningStep = null;

        Refresh();
        if (failed)
        {
            _message.Text = $"That step did not finish. Details are in {SetupRunner.LogPath}.";
        }
    }
}
