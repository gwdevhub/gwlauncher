using Avalonia;
using Avalonia.Controls;
using GwLauncher.Linux;
using GwLauncher.Ui;

namespace GwLauncher.Tray;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        InjectorInstaller.Ensure();
        var host = new LinuxLauncherHost();
        Func<Window>? startup = null;

        if (args is ["-launch", var accountName])
        {
            var outcome = LaunchFromShortcut(host, accountName);
            if (outcome.Started)
            {
                return 0;
            }

            // A desktop click has no terminal, so the reason goes to a window too.
            if (outcome.Error != null)
            {
                Console.Error.WriteLine(outcome.Error);
            }

            startup = outcome.Window ?? (() => Dialogs.MessageWindow("GW Launcher", outcome.Error ?? "The account couldn't be started."));
        }

        return AppBuilder.Configure(() => new LauncherApp(host, startup))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    // Starts the account, or returns what the window should show: its error, or the account's issues.
    private static (bool Started, string? Error, Func<Window>? Window) LaunchFromShortcut(LinuxLauncherHost host, string accountName)
    {
        var index = host.IndexOf(accountName);
        if (index < 0)
        {
            return (false, $"No account named '{accountName}'.", null);
        }

        var issues = AccountDiagnostics.Check(LinuxLauncherHost.OpenStore().Accounts[index]);
        if (issues.Any(issue => issue.Severity == IssueSeverity.Blocking))
        {
            return (false, null, () => LaunchFlow.IssuesWindow(host, null, index, accountName, issues, launchBlocked: true, withLaunch: false));
        }

        // Blocks before the app starts, which the shortcut has to wait for anyway.
        var versionIssues = host.VersionIssues(index).GetAwaiter().GetResult();
        if (versionIssues.Count > 0)
        {
            var all = issues.Concat(versionIssues).ToList();
            return (false, null, () => LaunchFlow.IssuesWindow(host, null, index, accountName, all, launchBlocked: true, withLaunch: true));
        }

        var error = host.Launch(index);
        return error == null
            ? (true, null, null)
            : (false, $"Can't start {accountName}: {error}", null);
    }
}
