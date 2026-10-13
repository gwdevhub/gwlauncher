using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using GW_Launcher.Classes;
using GwLauncher.Linux;
using Newtonsoft.Json;

namespace GwLauncher.Tray;

// Tray-only app: the icon is the entry point. Left-click opens the launcher window, right-click shows the menu.
public sealed class App : Application
{
    private TrayIcon? _tray;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private LauncherWindow? _window;
    private SetupWindow? _setup;
    private GlobalSettings _settings = new();
    private string _menuState = "";
    private readonly DispatcherTimer _menuTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    // Without a theme Avalonia draws controls unstyled, so buttons look like plain text.
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    // Set by a desktop-shortcut launch that can't start. The app then only shows that and exits when it's closed.
    public static string? StartupError { get; set; }

    public static Func<Window>? StartupWindow { get; set; }

    public static Window IssuesWindow(LinuxAccount account, IReadOnlyList<AccountIssue> issues, bool launchBlocked, Func<string?>? launch = null) =>
        new AccountIssuesWindow(account.Name, issues, launchBlocked, issue => ResolveIssue(account, issue), launch);

    public override void OnFrameworkInitializationCompleted()
    {
        if ((StartupError != null || StartupWindow != null) && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime startupLifetime)
        {
            startupLifetime.ShutdownMode = ShutdownMode.OnLastWindowClose;
            if (StartupWindow != null)
            {
                startupLifetime.MainWindow = StartupWindow();
                startupLifetime.MainWindow.Show();
            }
            else
            {
                MessageWindow.Show(null, "GW Launcher", StartupError!);
            }

            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _desktop = desktop;
        }

        _settings = LoadSettings();

        // Like the Windows launcher, refresh gMod.dll in the background when updates are on. A failure here is
        // ignored; a launch that needs gMod reports it.
        if (_settings.CheckForUpdates)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await GModProvider.RefreshAsync();
                }
                catch (GModUnavailableException)
                {
                }
            });
        }

        if (HostCapabilities.TrayHost)
        {
            CreateTray();
        }

        // With launch-minimised on, the app starts silently in the tray. Without a tray or with keep-in-tray off, it shows the window.
        if (!KeepInTray || !_settings.LaunchMinimized)
        {
            ShowLauncherCentered();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Without a tray host the icon never appears, so the launcher window is the only way in.
    private bool KeepInTray => _settings.KeepInSystemTray && HostCapabilities.TrayHost;

    private void CreateTray()
    {
        _tray = new TrayIcon
        {
            Icon = AppIcon.Window(),
            ToolTipText = "GW Launcher"
        };
        _tray.Clicked += (_, _) => ShowLauncher();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });

        RebuildMenu();

        // The menu is built once per state change, so accounts or setup changes show up without a manual reload.
        _menuTimer.Tick += (_, _) =>
        {
            if (MenuState() != _menuState)
            {
                RebuildMenu();
            }
        };
        _menuTimer.Start();
    }

    private void ShowLauncherCentered()
    {
        EnsureLauncherWindow().ShowCentered();
    }

    private LauncherWindow EnsureLauncherWindow()
    {
        _window ??= new LauncherWindow(OpenStore, Launch, ShowSetup, () => _desktop?.Shutdown(), () => KeepInTray);
        _window.Refresh();
        return _window;
    }

    private static GlobalSettings LoadSettings()
    {
        try
        {
            return AppSettings.Load();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // An unreadable settings file shouldn't stop the launcher; it runs on defaults until the next save.
            return new GlobalSettings();
        }
    }

    private void ShowSettings()
    {
        var settingsWindow = new SettingsWindow(_settings, saved =>
        {
            AppSettings.Save(saved);
            _settings = saved;
        });
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private static string MenuState()
    {
        var accounts = AppPaths.AccountsFile;
        var accountsState = File.Exists(accounts)
            ? $"{File.GetLastWriteTimeUtc(accounts).Ticks}:{new FileInfo(accounts).Length}"
            : "none";
        return $"{WineInstaller.IsWineOnPath()}|{accountsState}";
    }

    private void ShowLauncher()
    {
        // A click on the icon while the window is open closes it, same as the Windows tray.
        if (_window is { IsVisible: true } || _window?.WasHiddenJustNow == true)
        {
            _window.HideLauncher();
            return;
        }

        var window = EnsureLauncherWindow();

        // The pointer is over the tray icon at click time, so its position is the icon's.
        if (PointerLocator.TryGetPosition(out var x, out var y))
        {
            window.ShowNear(x, y);
        }
        else
        {
            window.Show();
            window.Activate();
        }
    }

    private void ShowSetup()
    {
        _setup ??= new SetupWindow();
        _setup.ShowCentered();
    }

    private void RebuildMenu()
    {
        _menuState = MenuState();
        _tray!.Menu = BuildMenu();
    }

    internal static AccountStore OpenStore()
    {
        var store = new AccountStore(AppPaths.AccountsFile);
        store.Load(() => Environment.GetEnvironmentVariable("GWLAUNCHER_MASTER_PASSWORD")
            ?? throw new InvalidOperationException("Encrypted accounts need GWLAUNCHER_MASTER_PASSWORD set."));
        return store;
    }

    private static (List<LinuxAccount> Accounts, string? Error) LoadAccounts()
    {
        try
        {
            return (OpenStore().Accounts, null);
        }
        catch (Exception ex)
        {
            return (new List<LinuxAccount>(), $"Error: {ex.Message}");
        }
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        var setupComplete = Setup.IsComplete;
        var (accounts, error) = setupComplete ? LoadAccounts() : (new List<LinuxAccount>(), null);

        if (!setupComplete)
        {
            menu.Items.Add(new NativeMenuItem("Setup needed") { IsEnabled = false });
        }
        else if (error != null)
        {
            menu.Items.Add(new NativeMenuItem(error) { IsEnabled = false });
        }
        else if (accounts.Count == 0)
        {
            menu.Items.Add(new NativeMenuItem("No accounts") { IsEnabled = false });
        }

        foreach (var account in accounts)
        {
            var item = new NativeMenuItem(account.Name);
            item.Click += (_, _) => LaunchFromMenu(account);
            menu.Items.Add(item);
        }

        menu.Items.Add(new NativeMenuItemSeparator());

        var setup = new NativeMenuItem("Setup...");
        setup.Click += (_, _) => ShowSetup();
        menu.Items.Add(setup);

        var settings = new NativeMenuItem("Settings...");
        settings.Click += (_, _) => ShowSettings();
        menu.Items.Add(settings);

        var reload = new NativeMenuItem("Reload accounts");
        reload.Click += (_, _) => RebuildMenu();
        menu.Items.Add(reload);

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _desktop?.Shutdown();
        menu.Items.Add(quit);

        return menu;
    }

    // Returns a status message for the window and tooltip.
    // Returns the error, or null when the game started.
    public static string? Launch(LinuxAccount account)
    {
        try
        {
            var psi = WineLauncher.BuildStartInfo(account);
            WineLauncher.PrepareLaunch(account);
            WineLauncher.Start(psi);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // The shortcut and menu have no launcher window to resolve issues from, so only the gMod and Gw.exe fixes are offered here.
    private static async Task<string?> ResolveIssue(LinuxAccount account, AccountIssue issue)
    {
        if (issue.CanUpdateGwExe)
        {
            return await GwExeFix.RunAsync(null, account);
        }

        if (!issue.CanDownloadGMod)
        {
            return null;
        }

        try
        {
            var path = await GModProvider.RefreshAsync();
            return $"gMod.dll saved to {path}. Click the shortcut again.";
        }
        catch (GModUnavailableException ex)
        {
            return ex.Message;
        }
    }

    private static async void LaunchFromMenu(LinuxAccount account)
    {
        var gwIssues = await AccountDiagnostics.CheckGwExeVersionAsync(account);
        if (gwIssues.Count > 0)
        {
            IssuesWindow(account, AccountDiagnostics.Check(account).Concat(gwIssues).ToList(), launchBlocked: true, () => Launch(account)).Show();
            return;
        }

        ReportLaunchError(account, Launch(account));
    }

    private static void ReportLaunchError(LinuxAccount account, string? error)
    {
        if (error != null)
        {
            MessageWindow.Show(null, $"Can't start {account.Name}", error);
        }
    }
}
