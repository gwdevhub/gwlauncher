using Avalonia;
using GwLauncher.Linux;
using Newtonsoft.Json;

namespace GwLauncher.Tray;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        InjectorInstaller.Ensure();

        if (args is ["-launch", var accountName])
        {
            if (LaunchAccount(accountName))
            {
                return 0;
            }

            // A desktop click has no terminal, so the reason goes to a window too.
            if (App.StartupError != null)
            {
                Console.Error.WriteLine(App.StartupError);
            }
        }

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    // Returns true when the game started. Otherwise sets what the app shows before it exits: an error, or the account's issues.
    private static bool LaunchAccount(string accountName)
    {
        LinuxAccount? account;
        try
        {
            account = App.OpenStore().Accounts
                .FirstOrDefault(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException)
        {
            App.StartupError = ex.Message;
            return false;
        }

        if (account == null)
        {
            App.StartupError = $"No account named '{accountName}'.";
            return false;
        }

        var issues = AccountDiagnostics.Check(account);
        if (issues.Any(issue => issue.Severity == IssueSeverity.Blocking))
        {
            App.StartupWindow = () => App.IssuesWindow(account, issues, launchBlocked: true);
            return false;
        }

        // Blocks before the app starts, which the shortcut has to wait for anyway.
        var gwIssues = AccountDiagnostics.CheckGwExeVersionAsync(account).GetAwaiter().GetResult();
        if (gwIssues.Count > 0)
        {
            var allIssues = issues.Concat(gwIssues).ToList();
            App.StartupWindow = () => App.IssuesWindow(account, allIssues, launchBlocked: true, () => App.Launch(account));
            return false;
        }

        var error = App.Launch(account);
        if (error == null)
        {
            return true;
        }

        App.StartupError = $"Can't start {accountName}: {error}";
        return false;
    }
}
