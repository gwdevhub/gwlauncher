using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GwLauncher.Linux;
using GwLauncher.Resolve;

namespace GwLauncher.Tray;

// Decompresses a Windows-compacted file in two steps: a choice of what to do with the original, then the shared
// progress dialog. The result is the plain file's path, or "" if the user cancelled (the partial output has already
// been removed by then).
public static class ResolveWindow
{
    public static async Task<string> RunAsync(Window owner, string path)
    {
        var mode = await ChooseModeAsync(owner, path);
        if (mode == null)
        {
            return "";
        }

        var name = Path.GetFileName(path);
        var result = await ProgressDialog.RunAsync<string>(
            owner,
            new ProgressOptions(
                "Decompressing…",
                $"Decompressing {name}",
                mode == ResolveMode.ReplaceOriginal
                    ? "If asked, enter your password in the system prompt. The original is replaced once the copy is verified. This can take several minutes. Cancelling removes the partial copy."
                    : "If asked, enter your password in the system prompt. The original is kept. This can take several minutes. Cancelling removes the partial copy.",
                FailureText: "The file could not be decompressed"),
            (cancellation, progress) => WofResolver.ResolveAsync(
                path,
                mode.Value,
                new Progress<ResolveProgress>(p => progress.Report(new ProgressUpdate(p.Fraction, p.Detail))),
                cancellation));

        return result ?? "";
    }

    // The confirmation step. Null when the user cancels it.
    private static Task<ResolveMode?> ChooseModeAsync(Window owner, string path)
    {
        var window = new ChoiceWindow(path);
        window.Show(owner);
        return window.Choice;
    }

    private sealed class ChoiceWindow : AppWindow
    {
        private readonly TaskCompletionSource<ResolveMode?> _choice = new();
        private readonly RadioButton _keepOriginal = new() { Content = "Keep the original and decompress next to it", IsChecked = true };
        private readonly RadioButton _replace = new() { Content = "Replace the original" };

        public ChoiceWindow(string path)
        {
            Title = "Decompress file";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 460;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            CanMinimize = false;
            CanMaximize = false;
            Closing += (_, _) => _choice.TrySetResult(null);

            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{Path.GetFileName(path)} is compressed by Windows, so Linux can't read it.",
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock { Text = "Choose what to do with the compressed file:", TextWrapping = TextWrapping.Wrap },
                    Option(_keepOriginal, "The compressed file moves into a \"compressed\" folder beside it, and the plain copy takes its place."),
                    Option(_replace, "The plain copy is verified first, then takes the original's place. The compressed file is removed."),
                    Ui.ButtonRow(
                        Ui.MakeButton("Cancel", () => Close()),
                        Ui.MakeButton("Continue", Continue))
                }
            };
        }

        public Task<ResolveMode?> Choice => _choice.Task;

        private void Continue()
        {
            _choice.TrySetResult(_replace.IsChecked == true ? ResolveMode.ReplaceOriginal : ResolveMode.DecompressNextToOriginal);
            Close();
        }

        private static StackPanel Option(RadioButton button, string description)
        {
            return new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    button,
                    new TextBlock { Text = description, FontSize = 11, Opacity = 0.7, Margin = new Thickness(24, 0, 0, 0), TextWrapping = TextWrapping.Wrap }
                }
            };
        }
    }
}
