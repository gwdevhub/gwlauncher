namespace GwLauncher.Ui;

// The outcome of a launch request: started, failed with a reason, or stopped by issues the user should see first.
public sealed record LaunchResult(bool Started, string? Error = null, IReadOnlyList<AccountIssue>? Issues = null, bool CanLaunch = false);

// The launch path shared by the window, the tray menu and desktop shortcuts. Blocking issues stop the launch.
// Version issues, such as an outdated Gw.exe, are offered as fixes before the account starts.
public static class LaunchFlow
{
    public static async Task<LaunchResult> StartAsync(ILauncherHost host, int index)
    {
        var entries = host.Accounts(out var error);
        if (error != null)
        {
            return new LaunchResult(false, error);
        }

        var local = entries[index].Issues;
        if (entries[index].Blocked)
        {
            return new LaunchResult(false, Issues: local);
        }

        var version = await host.VersionIssues(index);
        if (version.Count > 0)
        {
            return new LaunchResult(false, Issues: local.Concat(version).ToList(), CanLaunch: true);
        }

        var launchError = await host.Launch(index);
        return launchError == null ? new LaunchResult(true) : new LaunchResult(false, launchError);
    }
}
