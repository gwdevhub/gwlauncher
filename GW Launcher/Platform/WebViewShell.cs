using GwLauncher.Ui;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GW_Launcher.Platform;

// The Windows window: a form holding a WebView2 view of the shared page, and a tray icon whose rectangle Windows
// reports, so the window opens right beside it.
internal sealed class WebViewShell : Form, IShell
{
    private readonly ILauncherHost _host;
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _menu = new();
    private LauncherBridge _bridge = null!;
    private Size _pageSize = new(220, 340);
    private bool _pinned;
    private bool _quitting;
    private long _hiddenAt;

    public WebViewShell(ILauncherHost host)
    {
        _host = host;
        Text = "GW Launcher";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Controls.Add(_view);

        _tray.Icon = Icon;
        _tray.Text = "GW Launcher";
        _tray.ContextMenuStrip = _menu;
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleFromTray();
            }
        };
        _menu.Opening += (_, _) => BuildMenu();

        // With the tray in use, closing hides the window and clicking elsewhere dismisses it, like a tray flyout.
        Deactivate += (_, _) =>
        {
            if (KeepInTray && !_pinned)
            {
                HideWindow();
            }
        };
        FormClosing += (_, e) =>
        {
            if (_quitting)
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

        // Invoke needs a window handle, and the page loads into a hidden window before it's first shown.
        _ = Handle;
    }

    // Windows always has a notification area.
    bool IShell.HasTray => true;

    bool IShell.Pinned
    {
        set => _pinned = value;
    }

    private bool KeepInTray => _host.Settings.KeepInSystemTray;

    public async Task Start(LauncherBridge bridge, bool showAtStart)
    {
        _bridge = bridge;
        _tray.Visible = true;

        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GW_Launcher", "WebView2");
        await _view.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(userDataFolder: userData));
        _view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _view.CoreWebView2.WebMessageReceived += (_, e) => _bridge.Receive(e.TryGetWebMessageAsString());
        _view.CoreWebView2.NavigateToString(WebAssets.Page());

        _host.StartBackground();
        if (showAtStart || !KeepInTray || !_host.Settings.LaunchMinimized)
        {
            ShowNearTray();
        }
    }

    public void Post(string json) => OnUi(() => _view.CoreWebView2?.ExecuteScriptAsync($"gwlReceive({json})"));

    void IShell.Show() => OnUi(ShowNearTray);

    void IShell.Hide() => OnUi(HideWindow);

    void IShell.Quit() => OnUi(() =>
    {
        _quitting = true;
        _tray.Visible = false;
        Application.Exit();
    });

    void IShell.Resize(int width, int height)
    {
        _pageSize = new Size(width, height);
        OnUi(() =>
        {
            if (Visible)
            {
                Place();
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
            picked.SetResult(dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null);
        });
        return picked.Task;
    }

    private void ShowNearTray()
    {
        ShowInTaskbar = !KeepInTray;
        Place();
        Show();
        Activate();
    }

    private void HideWindow()
    {
        Hide();
        _hiddenAt = Environment.TickCount64;
    }

    // Sizes the window to the page, in this monitor's pixels, and puts it beside the tray icon, or centred when the icon's
    // rectangle can't be read.
    private void Place()
    {
        Size = SizeFromClientSize(new Size(LogicalToDeviceUnits(_pageSize.Width), LogicalToDeviceUnits(_pageSize.Height)));
        var icon = TrayIconBounds();
        var area = Screen.FromPoint(icon?.Location ?? Cursor.Position).WorkingArea;
        var workArea = new ScreenRect(area.X, area.Y, area.Width, area.Height);
        var (x, y) = icon is { } bounds
            ? WindowPlacement.NextToIcon(new ScreenRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), Width, Height, workArea)
            : WindowPlacement.Centred(Width, Height, workArea);
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

    // The menu is rebuilt each time it opens, so it always lists the current accounts.
    private void BuildMenu()
    {
        _menu.Items.Clear();
        foreach (var item in TrayMenu.Build(_host, _bridge, this))
        {
            if (item.Separator)
            {
                _menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            var menuItem = new ToolStripMenuItem(item.Text) { Enabled = item.OnClick != null };
            if (item.OnClick is { } onClick)
            {
                menuItem.Click += (_, _) => onClick();
            }

            _menu.Items.Add(menuItem);
        }
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
