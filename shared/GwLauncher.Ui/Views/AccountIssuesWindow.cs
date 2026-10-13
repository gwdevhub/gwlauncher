using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// Lists an account's issues, blocking ones first. Issues with a fix get a Resolve button, which runs that fix and
// returns a status message, or null when nothing changed. When launch is given, a Launch button starts the account
// once no blocking issues are left.
public sealed class AccountIssuesWindow : AppWindow
{
    public AccountIssuesWindow(
        string accountName,
        IReadOnlyList<AccountIssue> issues,
        bool launchBlocked,
        Func<AccountIssue, Task<string?>> resolve,
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
            list.Children.Add(Widgets.Text("No issues. This account should launch."));
        }

        var status = Widgets.Text("", 11);
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

            if (issue.WofPath != null || issue.CanDownloadGMod || issue.CanUpdateGwExe)
            {
                var resolveButton = Widgets.Button("Resolve…", () => { }, 0);
                resolveButton.HorizontalAlignment = HorizontalAlignment.Left;
                resolveButton.Padding = new Thickness(10, 4);
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

                        if (issue.CanUpdateGwExe)
                        {
                            row.IsVisible = false;
                        }
                    }
                };
                row.Children.Add(resolveButton);
            }

            list.Children.Add(row);
        }

        var buttons = new List<Control>();
        if (launch != null)
        {
            launchButton = Widgets.Button("Launch", () => { }, 90);
            launchButton.IsEnabled = unresolvedBlocking.Count == 0;
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
            buttons.Add(launchButton);
        }

        buttons.Add(Widgets.Button("Close", () => Close()));

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
                Widgets.Row(buttons.ToArray())
            }
        };
    }
}
