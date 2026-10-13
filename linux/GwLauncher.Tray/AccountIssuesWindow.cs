using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// Lists an account's issues. Blocking issues come first, in red. Issues with a fix get a Resolve button, which
// runs that fix and returns a message for the status line, or null when nothing changed. When launch is given, a
// Launch button starts the account from here once no blocking issues are left.
public sealed class AccountIssuesWindow : AppWindow
{
    public AccountIssuesWindow(
        string accountName,
        IReadOnlyList<AccountIssue> issues,
        bool launchBlocked,
        Func<AccountIssue, Task<string?>>? resolve,
        Func<string?>? launch = null)
    {
        Title = launchBlocked ? $"Can't launch {accountName}" : $"Issues: {accountName}";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanMinimize = false;
        CanMaximize = false;

        var list = new StackPanel { Spacing = 10 };
        if (issues.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = "No issues. This account should launch.", TextWrapping = TextWrapping.Wrap });
        }

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
        Button? launchButton = null;
        var unresolvedBlocking = issues.Where(i => i.Severity == IssueSeverity.Blocking).ToList();

        foreach (var issue in issues.OrderBy(i => i.Severity))
        {
            var blocking = issue.Severity == IssueSeverity.Blocking;
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = (blocking ? "Blocking: " : "Warning: ") + issue.Message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = blocking ? Brushes.Firebrick : Brushes.DarkGoldenrod
            });

            if ((issue.WofPath != null || issue.CanDownloadGMod || issue.CanUpdateGwExe) && resolve != null)
            {
                var resolveButton = new Button { Content = "Resolve…", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4) };
                resolveButton.Click += async (_, _) =>
                {
                    resolveButton.IsEnabled = false;
                    var message = await resolve(issue);
                    status.Text = message ?? "Nothing was changed.";
                    resolveButton.IsEnabled = message == null;

                    // A fix that changed something clears its issue, and Launch unlocks once none are left.
                    if (message != null)
                    {
                        unresolvedBlocking.Remove(issue);
                        if (launchButton != null)
                        {
                            launchButton.IsEnabled = unresolvedBlocking.Count == 0;
                        }
                    }

                    if (message != null && issue.CanUpdateGwExe)
                    {
                        row.IsVisible = false;
                    }
                };
                row.Children.Add(resolveButton);
            }

            list.Children.Add(row);
        }

        var close = new Button { Content = "Close", MinWidth = 90, Padding = new Thickness(12, 6) };
        close.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        if (launch != null)
        {
            launchButton = new Button { Content = "Launch", MinWidth = 90, Padding = new Thickness(12, 6), IsEnabled = unresolvedBlocking.Count == 0 };
            launchButton.Click += (_, _) =>
            {
                var error = launch();
                if (error == null)
                {
                    Close();
                    return;
                }

                status.Text = $"Can't start {accountName}: {error}";
            };
            buttons.Children.Add(launchButton);
        }

        buttons.Children.Add(close);

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = launchBlocked ? "Fix these before launching:" : "Issues found for this account:",
                    FontWeight = FontWeight.SemiBold
                },
                list,
                status,
                buttons
            }
        };
    }
}
