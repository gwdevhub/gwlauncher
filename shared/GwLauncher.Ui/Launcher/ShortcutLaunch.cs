using Avalonia.Controls;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// What a desktop-shortcut launch produced: the account started, or a window to show instead.
public sealed record ShortcutOutcome(bool Started, string? Error, Func<Window>? Window);

public static class ShortcutLaunch
{
    // Starts an account without its launcher window. Blocking issues, and an outdated Gw.exe, give a window to show first.
    // The window is built lazily, because the platform calls this before its UI is running.
    public static ShortcutOutcome Start(ILauncherHost host, string name)
    {
        var entries = host.Accounts(out var error);
        if (error != null)
        {
            return new ShortcutOutcome(false, error, null);
        }

        var index = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return new ShortcutOutcome(false, $"No account named '{name}'.", null);
        }

        var issues = entries[index].Issues;
        if (issues.Any(issue => issue.Severity == IssueSeverity.Blocking))
        {
            return new ShortcutOutcome(false, null, () => LaunchFlow.IssuesWindow(host, null, index, name, issues, launchBlocked: true, withLaunch: false));
        }

        var versionIssues = host.VersionIssues(index).GetAwaiter().GetResult();
        if (versionIssues.Count > 0)
        {
            var all = issues.Concat(versionIssues).ToList();
            return new ShortcutOutcome(false, null, () => LaunchFlow.IssuesWindow(host, null, index, name, all, launchBlocked: true, withLaunch: true));
        }

        var launchError = host.Launch(index).GetAwaiter().GetResult();
        return launchError == null
            ? new ShortcutOutcome(true, null, null)
            : new ShortcutOutcome(false, $"Can't start {name}: {launchError}", null);
    }
}
