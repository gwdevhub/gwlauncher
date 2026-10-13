using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// The window a tray click opens. With the tray in use, closing or losing focus hides it so the launcher keeps running.
// Sized to the Windows launcher's main form.
public sealed class LauncherWindow : AppWindow
{
    private readonly ILauncherHost _host;
    private readonly Action _quit;
    private readonly ListBox _accountList = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4) };
    private readonly Grid _accountsView = new() { RowDefinitions = new RowDefinitions("*,Auto") };
    private readonly StackPanel _setupView = new() { Margin = new Thickness(16), Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContextMenu _contextMenu = new();
    private readonly MenuItem _editItem = new() { Header = "Edit Selected" };
    private readonly MenuItem _moveUpItem = new() { Header = "Move Up" };
    private readonly MenuItem _moveDownItem = new() { Header = "Move Down" };
    private readonly MenuItem _removeItem = new() { Header = "Remove Selected" };
    private readonly MenuItem _launchItem = new() { Header = "Launch Selected" };
    private readonly MenuItem _shortcutItem = new() { Header = "Create Desktop Shortcut" };
    private readonly MenuItem _issuesItem = new() { Header = "View issues..." };
    private IReadOnlyList<AccountEntry> _entries = new List<AccountEntry>();
    private DateTime _lastHiddenUtc = DateTime.MinValue;

    // Focus moves to the context menu and to dialogs; the window must stay up while they're open.
    private bool _menuOpen;
    private bool _dialogOpen;

    public LauncherWindow(ILauncherHost host, Action quit)
    {
        _host = host;
        _quit = quit;

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

        _setupView.Children.Add(Widgets.Text("Setup is needed before accounts can launch."));
        if (host.HasSetup)
        {
            var setupButton = Widgets.Button("Open setup", () => host.ShowSetup(this), 0);
            setupButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            setupButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            setupButton.MinHeight = 36;
            _setupView.Children.Add(setupButton);
        }

        Content = new Grid { Children = { _accountsView, _setupView } };

        Closing += (_, e) =>
        {
            // With keep-in-tray off the window is a normal app window, so closing it exits, as on Windows.
            if (!KeepInTray)
            {
                return;
            }

            e.Cancel = true;
            HideLauncher();
        };

        // Clicking anywhere else takes focus away, which dismisses the window like a tray flyout.
        Deactivated += (_, _) =>
        {
            if (KeepInTray && !_menuOpen && !_dialogOpen)
            {
                HideLauncher();
            }
        };
    }

    private bool KeepInTray => _host.HasTray && _host.Settings.KeepInSystemTray;

    public void ShowCentered()
    {
        ShowInTaskbar = !KeepInTray;
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

    // Anchors the window to the tray icon: above a bottom panel, below a top one, clamped on screen.
    public void ShowNear(int anchorX, int anchorY)
    {
        ShowInTaskbar = !KeepInTray;
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
        if (_host.SetupNeeded)
        {
            _accountsView.IsVisible = false;
            _setupView.IsVisible = true;
            return;
        }

        _setupView.IsVisible = false;
        _accountsView.IsVisible = true;

        _entries = _host.Accounts(out var error);
        _status.Text = error ?? (_entries.Count == 0 ? "No accounts yet. Right-click to add one." : "");
        _accountList.ItemsSource = _entries.Select(entry => entry.Blocked ? "⚠ " + entry.Name : entry.Name).ToList();
    }

    private void BuildContextMenu()
    {
        var refresh = new MenuItem { Header = "Refresh Accounts" };
        refresh.Click += (_, _) => Refresh();

        var add = new MenuItem { Header = "Add New" };
        add.Click += (_, _) => OpenEditor(null);

        _editItem.Click += (_, _) =>
        {
            var index = _accountList.SelectedIndex;
            if (index >= 0)
            {
                OpenEditor(index);
            }
        };

        _moveUpItem.Click += (_, _) => Modify(() => _host.Move(SelectedIndex, SelectedIndex - 1), SelectedIndex - 1);
        _moveDownItem.Click += (_, _) => Modify(() => _host.Move(SelectedIndex, SelectedIndex + 1), SelectedIndex + 1);
        _removeItem.Click += async (_, _) => await ConfirmRemove();
        _launchItem.Click += (_, _) => LaunchSelected();
        _shortcutItem.Click += (_, _) => CreateDesktopShortcut();
        _issuesItem.Click += (_, _) => ShowIssues(SelectedIndex, launchBlocked: false);

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

    private int SelectedIndex => _accountList.SelectedIndex;

    private void UpdateMenuState()
    {
        var index = SelectedIndex;
        var hasSelection = index >= 0;

        _editItem.IsEnabled = hasSelection;
        _removeItem.IsEnabled = hasSelection;
        _launchItem.IsEnabled = hasSelection;
        _shortcutItem.IsEnabled = hasSelection && _host.CanCreateShortcuts;
        _issuesItem.IsEnabled = hasSelection;
        _moveUpItem.IsEnabled = hasSelection && index > 0;
        _moveDownItem.IsEnabled = hasSelection && index < _entries.Count - 1;
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

    // Applies a host change, then reloads and reselects. The host's account file stays the source of truth.
    private void Modify(Func<string?> change, int? selectIndex)
    {
        var error = change();
        if (error != null)
        {
            _status.Text = $"Could not save: {error}";
            return;
        }

        Refresh();
        if (selectIndex is { } index && index >= 0 && index < _entries.Count)
        {
            _accountList.SelectedIndex = index;
        }
    }

    private async void OpenEditor(int? index)
    {
        var editor = new AccountEditorWindow(_host, index, () =>
        {
            Refresh();
            _accountList.SelectedIndex = index ?? _entries.Count - 1;
        });
        await ShowModal(editor);
    }

    private async Task ConfirmRemove()
    {
        var index = SelectedIndex;
        if (index < 0)
        {
            return;
        }

        var name = _entries[index].Name;
        _dialogOpen = true;
        try
        {
            if (await Dialogs.Confirm(this, "Remove Account", $"Are you sure you want to remove '{name}'?"))
            {
                Modify(() => _host.Remove(index), null);
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    // Shows a dialog owned by this window, keeping this window open until it closes.
    private async Task ShowModal(Window dialog)
    {
        _dialogOpen = true;
        try
        {
            await dialog.ShowDialog(this);
        }
        finally
        {
            _dialogOpen = false;
            Activate();
        }
    }

    private async void LaunchSelected()
    {
        var index = SelectedIndex;
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var entry = _entries[index];
        _dialogOpen = true;
        await LaunchFlow.StartAsync(_host, this, index, entry.Name, entry.Issues, () => { _dialogOpen = false; Refresh(); });
    }

    private void CreateDesktopShortcut()
    {
        var index = SelectedIndex;
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var error = _host.CreateShortcut(index);
        if (error != null)
        {
            Dialogs.Message("Could not create the shortcut", error);
            return;
        }

        _status.Text = $"Created a shortcut for {_entries[index].Name}.";
    }

    private async void ShowIssues(int index, bool launchBlocked)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var entry = _entries[index];
        var issues = entry.Issues;

        // Only the viewer checks Gw.exe against ArenaNet, so a blocked launch doesn't wait on the network.
        if (!launchBlocked)
        {
            issues = issues.Concat(await _host.VersionIssues(index)).ToList();
        }

        _dialogOpen = true;
        LaunchFlow.ShowIssues(_host, this, index, entry.Name, issues, launchBlocked, withLaunch: false, () => { _dialogOpen = false; Refresh(); });
    }
}
