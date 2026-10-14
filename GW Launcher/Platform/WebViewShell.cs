using GwLauncher.Ui;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace GW_Launcher.Platform;

// A form holding a WebView2 view of the shared page. The main window also has the tray icon, whose rectangle Windows
// reports, so it opens right beside it. Other windows are popups: they centre, stay open, and close with Hide.
internal sealed class WebViewShell : Form, IShell
{
    private static Task<CoreWebView2Environment>? _environment;
    private readonly ILauncherHost _host;
    private readonly Action? _onClosed;
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly NotifyIcon _tray = new();
    private Task _ready = Task.CompletedTask;
    private Size _pageSize;
    private bool _pinned;
    private bool _isMenu;
    private bool _menuShown;
    private Point _menuAnchor;
    private bool _quitting;
    private long _hiddenAt;

    public WebViewShell(ILauncherHost host, Action? onClosed = null)
    {
        _host = host;
        _onClosed = onClosed;
        _pageSize = new Size(host.Settings.ListWidth, host.Settings.ListHeight);
        Text = "GW Launcher";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Controls.Add(_view);

        _tray.Icon = Icon;
        _tray.Text = LauncherBridge.VersionLine();
        _tray.MouseClick += (_, _) => ToggleFromTray();

        // With the tray in use, closing hides the window and clicking elsewhere dismisses it, like a tray flyout.
        Deactivate += (_, _) =>
        {
            if (_isMenu)
            {
                // Focus can leave a menu before it's shown, so only a menu on screen is dismissed by it.
                if (_menuShown && Visible)
                {
                    HideWindow();
                }
            }
            else
            {
                HideUnlessPointerOver();
            }
        };
        FormClosing += (_, e) =>
        {
            if (_quitting || IsPopup)
            {
                return;
            }

            e.Cancel = true;
            if (KeepInTray)
            {
                HideWindow();
            }
            else
            {
                ((IShell)this).Quit();
            }
        };

        // A resize by dragging the edges is remembered once it ends. Resizes from the page are reported by Resize.
        ResizeEnd += (_, _) =>
        {
            if (!IsPopup && Visible)
            {
                _pageSize = new Size(ClientSize.Width * 96 / DeviceDpi, ClientSize.Height * 96 / DeviceDpi);
                PageSizeChanged?.Invoke(_pageSize.Width, _pageSize.Height);
            }
        };

        LocationChanged += (_, _) =>
        {
            if (!IsPopup && !KeepInTray && Visible)
            {
                PositionChanged?.Invoke(Location.X, Location.Y);
            }
        };

        FormClosed += (_, _) =>
        {
            if (IsPopup)
            {
                _onClosed!();
                Dispose();
            }
        };

        // Invoke needs a window handle, and the page loads into a hidden window before it's first shown.
        _ = Handle;
    }

    private bool IsPopup => _onClosed != null;

    // Windows always has a notification area.
    bool IShell.HasTray => true;

    bool IShell.Pinned
    {
        set => _pinned = value;
    }

    private bool KeepInTray => _host.Settings.KeepInSystemTray;

    public void Attach(Action<string> onMessage) => _ready = LoadPage(onMessage);

    public async Task Start(bool showAtStart)
    {
        _tray.Visible = KeepInTray;

        await _ready;
        ApplyMainLook();
        _host.StartBackground();
        if (showAtStart || !KeepInTray || !_host.Settings.LaunchMinimized)
        {
            ShowNearTray();
        }
    }

    private async Task LoadPage(Action<string> onMessage)
    {
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GW_Launcher", "WebView2");
        _environment ??= CoreWebView2Environment.CreateAsync(userDataFolder: userData);
        await _view.EnsureCoreWebView2Async(await _environment);
        _view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _view.CoreWebView2.WebMessageReceived += (_, e) => onMessage(e.TryGetWebMessageAsString());
        _view.CoreWebView2.NavigateToString(WebAssets.Page());
    }

    public void RunOnUi(Action work) => OnUi(work);

    public void Post(string json) => OnUi(() => _view.CoreWebView2?.ExecuteScriptAsync($"gwlReceive({json})"));

    void IShell.Show() => OnUi(ShowNearTray);

    void IShell.ShowMenu() => OnUi(() =>
    {
        _isMenu = true;
        _menuAnchor = Cursor.Position;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
    });

    void IShell.Hide() => OnUi(HideWindow);

    void IShell.HideUnlessPointerOver() => OnUi(HideUnlessPointerOver);

    void IShell.Quit() => OnUi(() =>
    {
        _quitting = true;
        _tray.Visible = false;
        Application.Exit();
    });

    public event Action<int, int>? PageSizeChanged;

    public event Action<int, int>? PositionChanged;

    void IShell.Resize(int width, int height)
    {
        _pageSize = new Size(width, height);
        if (!IsPopup)
        {
            PageSizeChanged?.Invoke(width, height);
        }

        OnUi(() =>
        {
            if (Visible)
            {
                Place();
            }
            else if (_isMenu)
            {
                ShowNearTray();
            }
        });
    }

    public void CopyText(string text) => OnUi(() => Clipboard.SetText(text));


    public Task<string?> PickFile(string title, bool modsOnly)
    {
        var picked = new TaskCompletionSource<string?>();
        OnUi(() =>
        {
            using var dialog = new OpenFileDialog { Title = title, Filter = modsOnly ? "Mods|*.dll;*.tpf;*.zip" : "Guild Wars|Gw.exe" };
            if (!modsOnly && GuildWarsFolder() is { } folder)
            {
                dialog.InitialDirectory = folder;
            }

            picked.SetResult(dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null);
        });
        return picked.Task;
    }

    // The install folder the Guild Wars installer records, so the browse starts where Gw.exe is.
    private static string? GuildWarsFolder() =>
        Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\ArenaNet\Guild Wars", "Path", null) as string ??
        Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\ArenaNet\Guild Wars", "Path", null) as string;

    private void ShowNearTray()
    {
        if (IsPopup)
        {
            ShowInTaskbar = !_isMenu;
            FormBorderStyle = _isMenu ? FormBorderStyle.None : FormBorderStyle.Sizable;
        }
        else
        {
            ApplyMainLook();
        }

        Place();
        Show();
        Activate();
        _menuShown |= _isMenu;
    }

    // The main window's tray icon, frame and taskbar entry for the current mode. Applied at launch and whenever the setting changes.
    private void ApplyMainLook()
    {
        _tray.Visible = KeepInTray;
        FormBorderStyle = KeepInTray ? FormBorderStyle.None : FormBorderStyle.Sizable;
        MinimizeBox = MaximizeBox = !KeepInTray;
        ShowInTaskbar = !KeepInTray;
    }

    // Changes the main window to the current mode in place, and shows it when leaving tray mode so it isn't lost.
    public void ApplyWindowMode() => OnUi(() =>
    {
        ApplyMainLook();
        if (Visible)
        {
            Place();
        }
        else if (!KeepInTray)
        {
            ShowNearTray();
        }
    });

    // Focus moving to the menu from a click on this window isn't a dismissal, so the pointer decides.
    private void HideUnlessPointerOver()
    {
        if (!IsPopup && KeepInTray && !_pinned && !(Visible && Bounds.Contains(Cursor.Position)))
        {
            HideWindow();
        }
    }

    private void HideWindow()
    {
        if (IsPopup)
        {
            Close();
            return;
        }

        Hide();
        _hiddenAt = Environment.TickCount64;
    }

    // Sizes the window to the page, in this monitor's pixels, and puts it beside the tray icon, or centred when the icon's
    // rectangle can't be read.
    private void Place()
    {
        if (_isMenu)
        {
            PlaceMenu();
            return;
        }

        var icon = IsPopup ? null : TrayIconBounds();
        var saved = !IsPopup && !KeepInTray ? SavedPosition() : null;
        var area = Screen.FromPoint(saved ?? icon?.Location ?? Cursor.Position).WorkingArea;
        var workArea = new ScreenRect(area.X, area.Y, area.Width, area.Height);
        var wanted = SizeFromClientSize(new Size(LogicalToDeviceUnits(_pageSize.Width), LogicalToDeviceUnits(_pageSize.Height)));
        var height = Math.Min(wanted.Height, workArea.Height);

        var tray = icon is { } && _host.Settings.KeepInSystemTray;
        var width = Math.Min(tray ? LogicalToDeviceUnits(WindowPlacement.TrayWidth) : wanted.Width, workArea.Width);
        var (x, y) = saved is { } at
            ? WindowPlacement.AtPoint(at.X, at.Y, width, height, workArea)
            : icon is { } near
                ? WindowPlacement.NextToIcon(new ScreenRect(near.X, near.Y, near.Width, near.Height), width, height, workArea)
                : WindowPlacement.Centred(width, height, workArea);

        Size = new Size(width, height);
        Location = new Point(x, y);
    }

    private Point? SavedPosition() => _host.Settings.ListX is { } x && _host.Settings.ListY is { } y ? new Point(x, y) : null;

    private void PlaceMenu()
    {
        var area = Screen.FromPoint(_menuAnchor).WorkingArea;
        var workArea = new ScreenRect(area.X, area.Y, area.Width, area.Height);
        var size = SizeFromClientSize(new Size(LogicalToDeviceUnits(_pageSize.Width), LogicalToDeviceUnits(_pageSize.Height)));
        var (x, y) = WindowPlacement.AtPoint(_menuAnchor.X, _menuAnchor.Y, size.Width, size.Height, workArea);
        Size = size;
        Location = new Point(x, y);
    }

    private Rectangle? TrayIconBounds()
    {
        try
        {
            var bounds = NotifyIconHelper.GetIconRect(_tray);
            return bounds.Width > 0 && bounds.Height > 0 ? bounds : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void ToggleFromTray()
    {
        // A click on the icon while the window is open closes it. That click also takes focus, which may have hidden it already.
        if (Visible || Environment.TickCount64 - _hiddenAt < 300)
        {
            HideWindow();
            return;
        }

        ShowNearTray();
    }


    private void OnUi(Action work)
    {
        if (InvokeRequired)
        {
            BeginInvoke(work);
        }
        else
        {
            work();
        }
    }
}
