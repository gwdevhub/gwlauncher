using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GW_Launcher.Classes;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// Opened by a left-click on the tray icon. Closing hides the window so the tray keeps running.
// Sized to the Windows launcher's main form: 204x312 client area, resizable to a 204x312 minimum.
public sealed class LauncherWindow : AppWindow
{
    private readonly Func<AccountStore> _openStore;
    private readonly Func<LinuxAccount, string?> _launch;
    private readonly Action _openSetup;
    private readonly Action _quit;
    private readonly Func<bool> _keepInTray;
    private readonly ListBox _accountList = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4) };
    private readonly Grid _accountsView = new() { RowDefinitions = new RowDefinitions("*,Auto") };
    private readonly StackPanel _setupView = new()
    {
        Margin = new Thickness(16),
        Spacing = 14,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ContextMenu _contextMenu = new();
    private readonly MenuItem _editItem = new() { Header = "Edit Selected" };
    private readonly MenuItem _moveUpItem = new() { Header = "Move Up" };
    private readonly MenuItem _moveDownItem = new() { Header = "Move Down" };
    private readonly MenuItem _removeItem = new() { Header = "Remove Selected" };
    private readonly MenuItem _launchItem = new() { Header = "Launch Selected" };
    private readonly MenuItem _shortcutItem = new() { Header = "Create Desktop Shortcut" };
    private readonly MenuItem _issuesItem = new() { Header = "View issues..." };
    private List<List<AccountIssue>> _issues = new();
    private List<LinuxAccount> _accounts = new();
    private DateTime _lastHiddenUtc = DateTime.MinValue;

    // Focus moves to the context menu and to dialogs; the window must stay up while they're open.
    private bool _menuOpen;
    private bool _dialogOpen;

    public LauncherWindow(
        Func<AccountStore> openStore,
        Func<LinuxAccount, string?> launch,
        Action openSetup,
        Action quit,
        Func<bool> keepInTray)
    {
        _openStore = openStore;
        _launch = launch;
        _openSetup = openSetup;
        _quit = quit;
        _keepInTray = keepInTray;

        Title = "GW Launcher";
        Width = 204;
        Height = 312;
        MinWidth = 204;
        MinHeight = 312;
        CanMinimize = false;
        CanMaximize = false;

        _accountList.DoubleTapped += (_, _) => LaunchSelected();
        _accountList.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(_accountList).Properties.IsRightButtonPressed)
            {
                SelectItemUnder(e.Source);
            }
        };
        Grid.SetRow(_accountList, 0);
        Grid.SetRow(_status, 1);
        _accountsView.Children.Add(_accountList);
        _accountsView.Children.Add(_status);

        BuildContextMenu();
        _accountList.ContextMenu = _contextMenu;

        var setupButton = new Button
        {
            Content = "Open setup",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(12, 8),
            MinHeight = 36
        };
        setupButton.Click += (_, _) => _openSetup();
        _setupView.Children.Add(new TextBlock
        {
            Text = "Setup is needed before accounts can launch.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        _setupView.Children.Add(setupButton);

        Content = new Grid { Children = { _accountsView, _setupView } };

        Closing += (_, e) =>
        {
            // With keep-in-tray off the window is a normal app window, so closing it exits, as on Windows.
            if (!_keepInTray())
            {
                return;
            }

            e.Cancel = true;
            HideLauncher();
        };

        // Clicking anywhere else takes focus away, which dismisses the window like a tray flyout.
        Deactivated += (_, _) =>
        {
            if (_keepInTray() && !_menuOpen && !_dialogOpen)
            {
                HideLauncher();
            }
        };
    }

    public void ShowCentered()
    {
        ShowInTaskbar = !_keepInTray();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Show();
        Activate();
    }

    // A tray click that focus-dismissed the window arrives just after the hide, and must not reopen it.
    public bool WasHiddenJustNow => DateTime.UtcNow - _lastHiddenUtc < TimeSpan.FromMilliseconds(300);

    public void HideLauncher()
    {
        Hide();
        _lastHiddenUtc = DateTime.UtcNow;
    }

    // Anchors the window to the tray icon the way Windows does: above a bottom panel, below a top one, clamped on screen.
    public void ShowNear(int anchorX, int anchorY)
    {
        ShowInTaskbar = !_keepInTray();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Show();
        Activate();

        // Bounds are only valid after the first layout pass, so position once the window has loaded.
        Dispatcher.UIThread.Post(() => PlaceNear(anchorX, anchorY), DispatcherPriority.Loaded);
    }

    private void PlaceNear(int anchorX, int anchorY)
    {
        var screen = Screens.ScreenFromPoint(new PixelPoint(anchorX, anchorY));
        if (screen == null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Bounds.Width * screen.Scaling);
        var height = (int)Math.Ceiling(Bounds.Height * screen.Scaling);

        var left = Math.Clamp(anchorX - width / 2, area.X, Math.Max(area.X, area.Right - width));
        var top = anchorY > area.Center.Y ? area.Bottom - height : area.Y;

        Position = new PixelPoint(left, top);
    }

    public void Refresh()
    {
        if (!Setup.IsComplete)
        {
            _accountsView.IsVisible = false;
            _setupView.IsVisible = true;
            return;
        }

        _setupView.IsVisible = false;
        _accountsView.IsVisible = true;

        try
        {
            _accounts = _openStore().Accounts;
            _status.Text = _accounts.Count == 0 ? "No accounts yet. Right-click to add one." : "";
        }
        catch (Exception ex)
        {
            _accounts = new List<LinuxAccount>();
            _status.Text = $"Error: {ex.Message}";
        }

        _issues = _accounts.Select(AccountDiagnostics.Check).ToList();
        _accountList.ItemsSource = _accounts
            .Select((account, i) => _issues[i].Any(issue => issue.Severity == IssueSeverity.Blocking)
                ? "⚠ " + account.Name
                : account.Name)
            .ToList();
    }

    private void BuildContextMenu()
    {
        var refresh = new MenuItem { Header = "Refresh Accounts" };
        refresh.Click += (_, _) => Refresh();

        var add = new MenuItem { Header = "Add New" };
        add.Click += (_, _) => OpenEditor(null, account => Modify(store => store.Add(account), null));

        _editItem.Click += (_, _) =>
        {
            var index = _accountList.SelectedIndex;
            if (index >= 0)
            {
                OpenEditor(_accounts[index], account => Modify(store => store.Replace(index, account), index));
            }
        };

        _moveUpItem.Click += (_, _) =>
        {
            var index = _accountList.SelectedIndex;
            if (index > 0)
            {
                Modify(store => store.Move(index, index - 1), index - 1);
            }
        };

        _moveDownItem.Click += (_, _) =>
        {
            var index = _accountList.SelectedIndex;
            if (index >= 0 && index < _accounts.Count - 1)
            {
                Modify(store => store.Move(index, index + 1), index + 1);
            }
        };

        _removeItem.Click += (_, _) => ConfirmRemove();

        _launchItem.Click += (_, _) => LaunchSelected();
        _shortcutItem.Click += (_, _) => CreateDesktopShortcut();
        _issuesItem.Click += (_, _) => ShowIssues(_accountList.SelectedIndex, launchBlocked: false);

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => _quit();

        _contextMenu.Items.Add(refresh);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(add);
        _contextMenu.Items.Add(_editItem);
        _contextMenu.Items.Add(_moveUpItem);
        _contextMenu.Items.Add(_moveDownItem);
        _contextMenu.Items.Add(_removeItem);
        _contextMenu.Items.Add(_launchItem);
        _contextMenu.Items.Add(_shortcutItem);
        _contextMenu.Items.Add(_issuesItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(exit);

        _contextMenu.Opening += (_, _) =>
        {
            _menuOpen = true;
            UpdateMenuState();
        };
        _contextMenu.Closed += (_, _) =>
        {
            _menuOpen = false;

            // Clicking away from an open menu leaves the window unfocused, so it dismisses here.
            if (!_dialogOpen && !IsActive)
            {
                HideLauncher();
            }
        };
    }

    private void UpdateMenuState()
    {
        var index = _accountList.SelectedIndex;
        var hasSelection = index >= 0;

        _editItem.IsEnabled = hasSelection;
        _removeItem.IsEnabled = hasSelection;
        _launchItem.IsEnabled = hasSelection;
        _shortcutItem.IsEnabled = hasSelection && HostCapabilities.DesktopIcons;
        _issuesItem.IsEnabled = hasSelection;
        _moveUpItem.IsEnabled = hasSelection && index > 0;
        _moveDownItem.IsEnabled = hasSelection && index < _accounts.Count - 1;
    }

    // Right-clicking a row selects it first, as the Windows list does.
    private void SelectItemUnder(object? source)
    {
        if (source is not Visual visual)
        {
            return;
        }

        var item = visual.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (item == null)
        {
            return;
        }

        var index = _accountList.IndexFromContainer(item);
        if (index >= 0)
        {
            _accountList.SelectedIndex = index;
        }
    }

    // Loads the store, applies the change, saves, and reselects. The file stays the source of truth.
    private void Modify(Action<AccountStore> change, int? selectIndex)
    {
        try
        {
            var store = _openStore();
            change(store);
            store.Save();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not save: {ex.Message}";
            return;
        }

        Refresh();
        if (selectIndex is { } index && index >= 0 && index < _accounts.Count)
        {
            _accountList.SelectedIndex = index;
        }
    }

    private void OpenEditor(LinuxAccount? existing, Action<LinuxAccount> onSave)
    {
        var editor = new AccountEditorWindow(existing, onSave);
        OpenDialog(editor);
    }

    private void ConfirmRemove()
    {
        var index = _accountList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        var name = _accounts[index].Name;
        var confirm = new ConfirmWindow(
            "Remove Account",
            $"Are you sure you want to remove '{name}'?",
            () => Modify(store => store.Remove(index), null));
        OpenDialog(confirm);
    }

    // Shows a dialog owned by this window, keeping this window open until it closes.
    private void OpenDialog(Window dialog)
    {
        _dialogOpen = true;
        dialog.Closed += (_, _) =>
        {
            _dialogOpen = false;
            Activate();
        };
        dialog.Show(this);
    }

    // Runs the fix for one issue. Returns a status message, or null when nothing changed.
    private async Task<string?> ResolveIssue(int index, AccountIssue issue)
    {
        if (issue.WofPath != null)
        {
            var resolved = await ResolveAndRepoint(index, issue.WofPath);
            return resolved == "" ? null : $"Decompressed to {resolved}. Reopen the account to check its issues again.";
        }

        if (issue.CanDownloadGMod)
        {
            var path = await ProgressDialog.RunAsync<string>(
                this,
                new ProgressOptions(
                    "Downloading gMod.dll",
                    "Downloading gMod.dll from GitHub",
                    "Texmods need it. It's saved next to the launcher.",
                    FailureText: "gMod.dll couldn't be downloaded"),
                (cancellation, progress) => GModProvider.RefreshAsync(progress, cancellation));
            return path == null ? null : $"gMod.dll saved to {path}. Reopen the account to check its issues again.";
        }

        if (issue.CanUpdateGwExe)
        {
            return await GwExeFix.RunAsync(this, _accounts[index]);
        }

        return null;
    }

    // Runs the decompression flow for one of the account's files, then points the account at the plain copy.
    // Replacing the original needs no repointing, since the path is unchanged.
    private async Task<string> ResolveAndRepoint(int index, string path)
    {
        var resolved = await ResolveWindow.RunAsync(this, path);
        if (resolved == "" || resolved == path)
        {
            return resolved;
        }

        string message;
        try
        {
            var store = _openStore();
            var stored = store.Accounts.FirstOrDefault(a => a.Name == _accounts[index].Name && a.GwPath == _accounts[index].GwPath);
            if (stored == null)
            {
                message = "The account changed while it was being resolved, so it wasn't repointed.";
            }
            else
            {
                var (changed, text) = AccountRepoint.Apply(stored, path, resolved);
                if (changed)
                {
                    store.Save();
                }

                message = text;
            }
        }
        catch (Exception ex)
        {
            message = $"Decompressed to {resolved}, but the account could not be saved: {ex.Message}";
        }

        // Refresh clears the status line, so the message is set after it.
        Refresh();
        _status.Text = message;
        return resolved;
    }

    private async void LaunchSelected()
    {
        var index = _accountList.SelectedIndex;
        if (index < 0 || index >= _accounts.Count)
        {
            return;
        }

        // Blocking issues are shown as the reason the launch won't start, rather than as a generic failure.
        if (_issues[index].Any(issue => issue.Severity == IssueSeverity.Blocking))
        {
            ShowIssues(index, launchBlocked: true);
            return;
        }

        var account = _accounts[index];
        var gwIssues = await AccountDiagnostics.CheckGwExeVersionAsync(account);
        if (gwIssues.Count > 0)
        {
            var window = new AccountIssuesWindow(
                account.Name,
                _issues[index].Concat(gwIssues).ToList(),
                launchBlocked: true,
                issue => ResolveIssue(index, issue),
                () => _launch(account));
            window.Closed += (_, _) => Refresh();
            OpenDialog(window);
            return;
        }

        var error = _launch(account);
        if (error != null)
        {
            // No owner: the launcher hides when it loses focus, and an owned message would hide with it.
            MessageWindow.Show(null, $"Can't start {account.Name}", error);
        }
    }

    private void CreateDesktopShortcut()
    {
        var index = _accountList.SelectedIndex;
        if (index < 0 || index >= _accounts.Count)
        {
            return;
        }

        try
        {
            var launcher = Environment.ProcessPath
                ?? throw new InvalidOperationException("The launcher's location isn't known, so no shortcut can be made.");
            AppIcon.SaveTo(AppPaths.IconFile);
            var path = DesktopShortcut.Create(_accounts[index], launcher);
            _status.Text = $"Created {Path.GetFileName(path)} on the Desktop.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageWindow.Show(null, "Could not create the shortcut", ex.Message);
        }
    }

    private async void ShowIssues(int index, bool launchBlocked)
    {
        if (index < 0 || index >= _accounts.Count)
        {
            return;
        }

        var account = _accounts[index];
        var issues = _issues[index];

        // Only the viewer checks Gw.exe against ArenaNet, so a blocked launch doesn't wait on the network.
        if (!launchBlocked)
        {
            issues = issues.Concat(await AccountDiagnostics.CheckGwExeVersionAsync(account)).ToList();
        }

        var window = new AccountIssuesWindow(
            account.Name,
            issues,
            launchBlocked,
            issue => ResolveIssue(index, issue));

        // Issues are recomputed when the dialog closes, so a finished resolve clears its warning.
        window.Closed += (_, _) => Refresh();
        OpenDialog(window);
    }
}
