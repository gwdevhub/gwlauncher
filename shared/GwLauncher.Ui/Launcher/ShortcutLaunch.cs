namespace GwLauncher.Ui;

// A desktop shortcut starts its account without the launcher window. When it can't, the window opens on what stopped it.
public static class ShortcutLaunch
{
    // Null when the account started. Otherwise the view the page should open on.
    public static object? Start(ILauncherHost host, string name)
    {
        var entries = host.Accounts(out var error);
        if (error != null)
        {
            return MessageView("GW Launcher", error);
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
            return MessageView("GW Launcher", $"No account named '{name}'.");
        }

        var result = LaunchFlow.StartAsync(host, index).GetAwaiter().GetResult();
        if (result.Started)
        {
            return null;
        }

        return result.Issues != null
            ? IssuesView(index, entries[index].Name, result)
            : MessageView($"Can't start {name}", result.Error ?? "The account couldn't be started.");
    }

    public static object IssuesView(int index, string name, LaunchResult result) =>
        new { view = "issues", index, name, issues = result.Issues, canLaunch = result.CanLaunch, launchBlocked = true };

    public static object MessageView(string title, string text) => new { view = "message", title, text };
}
