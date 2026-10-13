namespace GwLauncher.Ui;

// One tray menu row. A null action makes a disabled label, and Separator draws a line.
public sealed record TrayItem(string Text, Action? OnClick = null, bool Separator = false);

// The tray menu's contents, built here so both platforms show the same items. Each shell draws it natively, and
// rebuilds it each time it opens, so it always lists the current accounts.
public static class TrayMenu
{
    public static IReadOnlyList<TrayItem> Build(ILauncherHost host, LauncherBridge bridge, IShell shell)
    {
        var items = new List<TrayItem>();
        var setupNeeded = host.SetupItems().Any(item => !item.Done);
        if (setupNeeded)
        {
            items.Add(new TrayItem("Setup needed"));
        }
        else
        {
            var accounts = host.Accounts(out var error);
            if (error != null)
            {
                items.Add(new TrayItem(error));
            }
            else if (accounts.Count == 0)
            {
                items.Add(new TrayItem("No accounts"));
            }

            for (var index = 0; index < accounts.Count; index++)
            {
                var account = index;
                items.Add(new TrayItem(accounts[index].Name, () => LaunchFromMenu(host, bridge, account)));
            }
        }

        items.Add(new TrayItem("", Separator: true));
        if (host.SetupItems().Count > 0)
        {
            items.Add(new TrayItem("Setup...", () => bridge.Navigate(new { view = "setup" })));
        }

        items.Add(new TrayItem("Settings...", () => bridge.Navigate(new { view = "settings" })));
        items.Add(new TrayItem("Quit", shell.Quit));
        return items;
    }

    private static async void LaunchFromMenu(ILauncherHost host, LauncherBridge bridge, int index)
    {
        var result = await LaunchFlow.StartAsync(host, index);
        if (result.Issues != null)
        {
            bridge.Navigate(ShortcutLaunch.IssuesView(index, host.Accounts(out _)[index].Name, result));
        }
        else if (result.Error != null)
        {
            bridge.Message($"Can't start {host.Accounts(out _)[index].Name}", result.Error);
        }
    }
}
