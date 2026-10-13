using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// A tray icon whose left click opens the launcher window and whose right click opens the menu. With a startup window
// (a desktop shortcut that can't launch), it shows that window alone and exits when it's closed.
public sealed class LauncherApp : Application
{
    private readonly ILauncherHost _host;
    private readonly Func<Window>? _startupWindow;
    private readonly DispatcherTimer _menuTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private TrayIcon? _tray;
    private LauncherWindow? _window;
    private string _menuState = "";

    public LauncherApp(ILauncherHost host, Func<Window>? startupWindow = null)
    {
        _host = host;
        _startupWindow = startupWindow;
    }

    // Without a theme Avalonia draws controls unstyled, so buttons look like plain text.
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (_desktop != null)
        {
            _desktop.ShutdownMode = _startupWindow != null ? ShutdownMode.OnLastWindowClose : ShutdownMode.OnExplicitShutdown;
        }

        if (_startupWindow != null && _desktop != null)
        {
            _desktop.MainWindow = _startupWindow();
            _desktop.MainWindow.Show();
        }
        else
        {
            _ = StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync()
    {
        while (_host.Locked)
        {
            var answer = await Dialogs.Password(null, offerRemember: true);
            if (answer == null)
            {
                _desktop?.Shutdown();
                return;
            }

            var error = _host.Unlock(answer.Value.Password, answer.Value.Remember);
            if (error != null)
            {
                Dialogs.Message("GW Launcher", error);
            }
        }

        _host.StartBackground();
        if (_host.HasTray)
        {
            CreateTray();
        }

        // With launch-minimised on, the app starts silently in the tray. Without a tray or with keep-in-tray off, it shows the window.
        var keepInTray = _host.HasTray && _host.Settings.KeepInSystemTray;
        if (!keepInTray || !_host.Settings.LaunchMinimized)
        {
            EnsureWindow().ShowCentered();
        }
    }

    private void CreateTray()
    {
        _tray = new TrayIcon { Icon = AppIcon.Window(), ToolTipText = "GW Launcher" };
        _tray.Clicked += (_, _) => ShowLauncher();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });
        RebuildMenu();

        // The menu is rebuilt when the host's menu state changes, so account or setup changes show up without a reload.
        _menuTimer.Tick += (_, _) =>
        {
            if (_host.MenuState() != _menuState)
            {
                RebuildMenu();
            }
        };
        _menuTimer.Start();
    }

    private LauncherWindow EnsureWindow()
    {
        _window ??= new LauncherWindow(_host, () => _desktop?.Shutdown());
        _window.Refresh();
        return _window;
    }

    private void ShowLauncher()
    {
        // A click on the icon while the window is open closes it, the same as the Windows tray.
        if (_window is { IsVisible: true } || _window?.WasHiddenJustNow == true)
        {
            _window.HideLauncher();
            return;
        }

        var window = EnsureWindow();
        if (_host.TryTrayAnchor(out var x, out var y))
        {
            window.ShowNear(x, y);
        }
        else
        {
            window.Show();
            window.Activate();
        }
    }

    private void RebuildMenu()
    {
        _menuState = _host.MenuState();
        _tray!.Menu = BuildMenu();
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        if (_host.SetupNeeded)
        {
            menu.Items.Add(new NativeMenuItem("Setup needed") { IsEnabled = false });
        }
        else
        {
            var accounts = _host.Accounts(out var error);
            if (error != null)
            {
                menu.Items.Add(new NativeMenuItem(error) { IsEnabled = false });
            }
            else if (accounts.Count == 0)
            {
                menu.Items.Add(new NativeMenuItem("No accounts") { IsEnabled = false });
            }

            for (var index = 0; index < accounts.Count; index++)
            {
                var entry = accounts[index];
                var item = new NativeMenuItem(entry.Name);
                item.Click += (_, _) => _ = LaunchFlow.StartAsync(_host, null, index, entry.Name, entry.Issues);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new NativeMenuItemSeparator());

        if (_host.HasSetup)
        {
            AddItem(menu, "Setup...", () => _host.ShowSetup(_window));
        }

        AddItem(menu, "Settings...", () => new SettingsWindow(_host).Show());
        AddItem(menu, "Reload accounts", RebuildMenu);
        AddItem(menu, "Quit", () => _desktop?.Shutdown());
        return menu;
    }

    private static void AddItem(NativeMenu menu, string header, Action onClick)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => onClick();
        menu.Items.Add(item);
    }
}
