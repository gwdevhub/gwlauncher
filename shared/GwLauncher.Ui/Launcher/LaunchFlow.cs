using Avalonia.Controls;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// The launch path shared by the window and the tray menu. Blocking issues stop the launch and show the issues window.
// Version issues, such as an outdated Gw.exe, are offered as fixes before the account starts.
public static class LaunchFlow
{
    public static async Task StartAsync(ILauncherHost host, Window? owner, int index, string name, IReadOnlyList<AccountIssue> localIssues, Action? closed = null)
    {
        if (localIssues.Any(issue => issue.Severity == IssueSeverity.Blocking))
        {
            ShowIssues(host, owner, index, name, localIssues, launchBlocked: true, withLaunch: false, closed);
            return;
        }

        var versionIssues = await host.VersionIssues(index);
        if (versionIssues.Count > 0)
        {
            ShowIssues(host, owner, index, name, localIssues.Concat(versionIssues).ToList(), launchBlocked: true, withLaunch: true, closed);
            return;
        }

        var error = host.Launch(index);
        if (error != null)
        {
            Dialogs.Message($"Can't start {name}", error);
        }
    }

    public static AccountIssuesWindow IssuesWindow(
        ILauncherHost host,
        Window? owner,
        int index,
        string name,
        IReadOnlyList<AccountIssue> issues,
        bool launchBlocked,
        bool withLaunch)
    {
        return new AccountIssuesWindow(
            name,
            issues,
            launchBlocked,
            issue => host.Fix(owner, index, issue),
            withLaunch ? () => host.Launch(index) : null);
    }

    public static void ShowIssues(
        ILauncherHost host,
        Window? owner,
        int index,
        string name,
        IReadOnlyList<AccountIssue> issues,
        bool launchBlocked,
        bool withLaunch,
        Action? closed = null)
    {
        var window = IssuesWindow(host, owner, index, name, issues, launchBlocked, withLaunch);
        if (closed != null)
        {
            window.Closed += (_, _) => closed();
        }

        if (owner != null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }
}
