using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

public sealed record ProgressOptions(
    string Title,
    string Heading,
    string? Note = null,
    bool Cancellable = true,
    string FailureText = "The task failed");

// The one owner of a progress window's lifecycle. It runs the work, drives a ProgressPanel from its updates, and
// handles Cancel, closing mid-run (which also cancels), and failures (shown with a Close button). The result is the
// work's value, or null when the work was cancelled or failed. Callers that need a value on cancel map null themselves.
public static class ProgressDialog
{
    public static Task<T?> RunAsync<T>(Window? owner, ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T?>> work)
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

    private sealed class ProgressWindow<T> : AppWindow where T : class
    {
        private readonly ProgressOptions _options;
        private readonly Func<CancellationToken, IProgress<ProgressUpdate>, Task<T?>> _work;
        private readonly ProgressPanel _panel = new();
        private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
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
                _body.Children.Add(new TextBlock { Text = _options.Note, TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = 0.7 });
            }

            _body.Children.Add(_panel);
            _body.Children.Add(_message);

            if (_options.Cancellable)
            {
                _cancelButton = Ui.MakeButton("Cancel", RequestCancel);
                _body.Children.Add(Ui.ButtonRow(_cancelButton));
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
            _body.Children.Add(new TextBlock
            {
                Text = $"{_options.FailureText}: {reason}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Firebrick
            });
            _body.Children.Add(Ui.ButtonRow(Ui.MakeButton("Close", () => Close())));
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
