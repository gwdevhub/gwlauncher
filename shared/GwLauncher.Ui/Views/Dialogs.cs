using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;

namespace GwLauncher.Ui;

public sealed record ProgressOptions(
    string Title,
    string Heading,
    string? Note = null,
    bool Cancellable = true,
    string FailureText = "The task failed");

public static class Dialogs
{
    // Unowned, because the launcher hides when it loses focus and would take an owned message with it.
    public static void Message(string title, string text) => MessageWindow(title, text).Show();

    public static Window MessageWindow(string title, string text) => new MessageDialog(title, text);

    public static async Task<bool> Confirm(Window owner, string title, string message)
    {
        var yes = false;
        var window = new AppWindow { Title = title, Width = 300, SizeToContent = SizeToContent.Height, CanResize = false, CanMinimize = false, CanMaximize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                Widgets.Text(message),
                Widgets.Row(
                    Widgets.Button("No", () => window.Close(), 80),
                    Widgets.Button("Yes", () => { yes = true; window.Close(); }, 80))
            }
        };
        await window.ShowDialog(owner);
        return yes;
    }

    // Asks for the master password. Null means the user cancelled. Remember is only offered when the host can keep it.
    public static async Task<(string Password, bool Remember)?> Password(Window? owner, bool offerRemember)
    {
        var password = new TextBox { PasswordChar = '*', Watermark = "Master password" };
        var remember = new CheckBox { Content = "Don't ask again on this computer", IsVisible = offerRemember };
        var error = Widgets.Error();
        (string, bool)? answer = null;
        var window = new AppWindow { Title = "GW Launcher - Master password", Width = 340, SizeToContent = SizeToContent.Height, CanResize = false, CanMinimize = false, CanMaximize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        void Submit()
        {
            if (password.Text is not { Length: > 0 } text)
            {
                error.Text = "Enter the master password.";
                return;
            }

            answer = (text, remember.IsChecked == true);
            window.Close();
        }

        password.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Submit();
            }
        };
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                Widgets.Text("Accounts are encrypted. Enter the master password to open them."),
                password,
                remember,
                error,
                Widgets.Row(Widgets.Button("Cancel", () => window.Close(), 80), Widgets.Button("OK", Submit, 80))
            }
        };
        if (owner != null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            window.Show();
            await closed.Task;
        }

        return answer is { } result ? (result.Item1, result.Item2) : null;
    }

    // Runs work behind a progress window. The result is the work's value, or null when it was cancelled or failed.
    public static Task<T?> Progress<T>(Window? owner, ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T?>> work)
        where T : class
    {
        var window = new ProgressWindow<T>(options, work);
        if (owner != null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        return window.Outcome;
    }

    private sealed class MessageDialog : AppWindow
    {
        public MessageDialog(string title, string text)
        {
            Title = title;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            CanMinimize = false;
            CanMaximize = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var report = $"{title}\n{text}\n\n{VersionLine()}";
            var copy = Widgets.Button("Copy", () => { }, 90);
            copy.Click += async (_, _) =>
            {
                var clipboard = GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(report);
                    copy.Content = "Copied";
                }
            };

            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick },
                    Widgets.Text(VersionLine(), 11),
                    Widgets.Row(copy, Widgets.Button("OK", () => Close()))
                }
            };
        }

        // The versions go in the copied report, so a bug report carries them.
        private static string VersionLine() =>
            $"GW Launcher {typeof(Dialogs).Assembly.GetName().Version?.ToString(3) ?? "unknown"} · {System.Runtime.InteropServices.RuntimeInformation.OSDescription}";
    }

    private sealed class ProgressWindow<T> : AppWindow where T : class
    {
        private readonly ProgressOptions _options;
        private readonly Func<CancellationToken, IProgress<ProgressUpdate>, Task<T?>> _work;
        private readonly ProgressPanel _panel = new();
        private readonly TextBlock _message = Widgets.Error();
        private readonly StackPanel _body = new() { Margin = new Thickness(16), Spacing = 12 };
        private readonly TaskCompletionSource<T?> _outcome = new();
        private readonly CancellationTokenSource _cancellation = new();
        private Button? _cancelButton;
        private bool _running;

        public ProgressWindow(ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T?>> work)
        {
            _options = options;
            _work = work;

            Title = options.Title;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 460;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            CanMinimize = false;
            CanMaximize = false;
            Content = _body;

            Opened += (_, _) => _ = RunWorkAsync();
            Closing += OnClosing;
            ShowProgress();
        }

        public Task<T?> Outcome => _outcome.Task;

        private void ShowProgress()
        {
            _body.Children.Clear();
            _body.Children.Add(new TextBlock { Text = _options.Heading, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
            if (_options.Note != null)
            {
                _body.Children.Add(Widgets.Text(_options.Note, 11));
            }

            _body.Children.Add(_panel);
            _body.Children.Add(_message);
            if (_options.Cancellable)
            {
                _cancelButton = Widgets.Button("Cancel", RequestCancel, 100);
                _body.Children.Add(Widgets.Row(_cancelButton));
            }
        }

        private async Task RunWorkAsync()
        {
            _running = true;
            var progress = new Progress<ProgressUpdate>(_panel.Report);
            T? result;
            try
            {
                result = await _work(_cancellation.Token, progress);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                Finish(null);
                return;
            }
            catch (Exception ex)
            {
                _running = false;
                ShowFailure(ex.Message);
                return;
            }

            Finish(result);
        }

        private void Finish(T? result)
        {
            _running = false;
            _outcome.TrySetResult(result);
            Close();
        }

        private void ShowFailure(string reason)
        {
            // The outcome is set before the failure view is shown, so closing that view doesn't change it.
            _outcome.TrySetResult(null);
            Title = $"{_options.Title} failed";
            _body.Children.Clear();
            _body.Children.Add(Widgets.Error($"{_options.FailureText}: {reason}"));
            _body.Children.Add(Widgets.Row(Widgets.Button("Close", () => Close(), 100)));
        }

        private void RequestCancel()
        {
            if (!_running)
            {
                Close();
                return;
            }

            // The work ends by itself once its partial output is removed, and the window closes then.
            if (_cancelButton != null)
            {
                _cancelButton.IsEnabled = false;
                _cancelButton.Content = "Cancelling…";
            }

            _cancellation.Cancel();
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_running)
            {
                e.Cancel = true;
                RequestCancel();
                return;
            }

            _outcome.TrySetResult(null);
        }
    }
}

// The bar and detail line every progress view shows.
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
