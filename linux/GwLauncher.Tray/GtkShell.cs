using System.Collections.Concurrent;
using GwLauncher.Linux;
using GwLauncher.Tray.Native;
using GwLauncher.Ui;

namespace GwLauncher.Tray;

// A GTK window holding a WebKitGTK view of the shared page. The main window also owns the tray icon, whose position GTK
// reports, so it opens right beside it. Other windows are popups: they centre, stay open, and close with Hide.
internal sealed class GtkShell : IShell
{
    private readonly ILauncherHost _host;
    private readonly Action? _onClosed;
    private bool _closed;
    private readonly ConcurrentQueue<Action> _uiWork = new();

    // Delegates handed to GTK are kept here, so the garbage collector can't free one GTK still calls.
    private readonly List<Delegate> _callbacks = new();
    private readonly Gtk.SourceFunc _drainUiWork;
    private IntPtr _window;
    private IntPtr _view;
    private IntPtr _icon;
    private int _width;
    private int _height;
    private (int Width, int Height) _lastSize;
    private bool _pinned;
    private bool _isMenu;
    private bool _menuShown;
    private (int X, int Y) _anchor;
    private long _hiddenAt;

    public event Action<int, int>? PageSizeChanged;

    public event Action<int, int>? PositionChanged;

    public GtkShell(ILauncherHost host, Action? onClosed = null)
    {
        _host = host;
        _onClosed = onClosed;
        _width = host.Settings.ListWidth;
        _height = host.Settings.ListHeight;
        _lastSize = (_width, _height);
        _drainUiWork = _ =>
        {
            while (_uiWork.TryDequeue(out var work))
            {
                work();
            }

            return false;
        };
    }

    public bool HasTray { get; private set; }

    public bool Pinned
    {
        set => _pinned = value;
    }

    private bool IsPopup => _onClosed != null;

    // GDK_FUNC_MOVE | GDK_FUNC_CLOSE
    private const int GdkFunctionsMoveAndClose = 4 | 32;

    // GDK_FUNC_RESIZE as well, for the account list, which the user can resize.
    private const int GdkFunctionsMoveCloseAndResize = 2 | 4 | 32;

    // GDK_FUNC_MINIMIZE and GDK_FUNC_MAXIMIZE as well, for the account list outside tray mode.
    private const int GdkFunctionsAll = 2 | 4 | 8 | 16 | 32;

    private bool KeepInTray => HasTray && _host.Settings.KeepInSystemTray;

    public void Attach(Action<string> onMessage) => CreateWindow(WebAssets.SaveIcon(AppPaths.IconFile), onMessage);

    // Builds the tray icon, then runs GTK until Quit.
    public void Run(bool showAtStart)
    {
        var iconPath = WebAssets.SaveIcon(AppPaths.IconFile);

        _icon = Gtk.gtk_status_icon_new_from_file(iconPath);
        Gtk.gtk_status_icon_set_tooltip_text(_icon, LauncherBridge.VersionLine());
        Connect(_icon, "activate", new Gtk.Signal((_, _) => ToggleFromTray()));
        Connect(_icon, "popup-menu", new Gtk.PopupSignal((_, _, _, _) => ToggleFromTray()));

        // The panel embeds the icon shortly after it's created, and lays it out a moment later. Until then there's no telling
        // whether a tray exists, or where the icon is.
        var waited = 0;
        Every(100, () =>
        {
            waited += 100;
            if (IconBounds() == null && waited < 3000)
            {
                return true;
            }

            HasTray = Gtk.gtk_status_icon_is_embedded(_icon);
            Gtk.gtk_status_icon_set_visible(_icon, HasTray && _host.Settings.KeepInSystemTray);
            _host.StartBackground();
            if (showAtStart || !KeepInTray || !_host.Settings.LaunchMinimized)
            {
                Show();
            }

            return false;
        });

        Gtk.gtk_main();
    }

    public void RunOnUi(Action work) => OnUi(work);

    public void Post(string json) => OnUi(() => Gtk.webkit_web_view_run_javascript(_view, $"gwlReceive({json})", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));

    public void Show() => OnUi(() =>
    {
        if (IsPopup)
        {
            Gtk.gtk_window_set_skip_taskbar_hint(_window, _isMenu);
            Gtk.gtk_window_set_decorated(_window, !_isMenu);
        }
        else
        {
            ApplyMainLook();
        }

        Gtk.gtk_window_resize(_window, _width, _height);
        Place();
        Gtk.gtk_widget_show_all(_window);
        Gtk.gtk_window_present(_window);
        _menuShown |= _isMenu;

        ApplyWindowActions();

        // The title bar's height is only known once the window is on screen, so it's placed again then.
        Every(50, () =>
        {
            Place();
            return false;
        });
    });

    // The main window's tray icon, frame and taskbar entry for the current mode. Applied at launch and whenever the setting changes.
    private void ApplyMainLook()
    {
        Gtk.gtk_status_icon_set_visible(_icon, HasTray && _host.Settings.KeepInSystemTray);
        Gtk.gtk_window_set_skip_taskbar_hint(_window, KeepInTray);
        Gtk.gtk_window_set_decorated(_window, !KeepInTray);
    }

    // The window manager draws only the buttons the window allows. Outside tray mode the account list also gets minimise and maximise.
    private void ApplyWindowActions()
    {
        var gdkWindow = Gtk.gtk_widget_get_window(_window);
        var chrome = !IsPopup && !KeepInTray;
        Gtk.gdk_window_set_functions(gdkWindow, IsPopup ? GdkFunctionsMoveAndClose : chrome ? GdkFunctionsAll : GdkFunctionsMoveCloseAndResize);
        AllowActions(gdkWindow, !IsPopup, chrome);
    }

    // Changes the main window to the current mode in place, and shows it when leaving tray mode so it isn't lost.
    public void ApplyWindowMode() => OnUi(() =>
    {
        ApplyMainLook();
        if (Gtk.gtk_widget_get_visible(_window))
        {
            Place();
            ApplyWindowActions();
            Every(50, () =>
            {
                Place();
                return false;
            });
        }
        else if (!KeepInTray)
        {
            Show();
        }
    });

    // Read now rather than on the UI thread, so the pointer is still where the right-click left it.
    public void ShowMenu()
    {
        _isMenu = true;
        _anchor = PointerPosition();
        OnUi(() =>
        {
            Gtk.gtk_window_set_decorated(_window, false);
            Gtk.gtk_window_set_keep_above(_window, true);
        });
    }

    public void Hide() => OnUi(() =>
    {
        if (IsPopup)
        {
            _closed = true;
            Gtk.gtk_widget_destroy(_window);
            _onClosed!();
            return;
        }

        Gtk.gtk_widget_hide(_window);
        _hiddenAt = Environment.TickCount64;
    });

    public void Quit() => OnUi(() =>
    {
        Gtk.gtk_status_icon_set_visible(_icon, false);
        Gtk.gtk_main_quit();
    });

    public void Resize(int width, int height)
    {
        _width = width;
        _height = height;
        OnUi(() =>
        {
            Gtk.gtk_window_resize(_window, width, height);
            if (Gtk.gtk_widget_get_visible(_window))
            {
                Place();
            }
            else if (_isMenu)
            {
                Show();
            }
        });
    }

    public void CopyText(string text) => OnUi(() =>
        Gtk.gtk_clipboard_set_text(Gtk.gtk_clipboard_get(Gtk.gdk_atom_intern("CLIPBOARD", false)), text, -1));

    public Task<string?> PickFile(string title, bool modsOnly)
    {
        var picked = new TaskCompletionSource<string?>();
        OnUi(() =>
        {
            var dialog = Gtk.gtk_file_chooser_native_new(title, _window, 0, "_Open", "_Cancel");
            var filter = Gtk.gtk_file_filter_new();
            Gtk.gtk_file_filter_set_name(filter, modsOnly ? "Mods" : "Guild Wars");
            foreach (var pattern in modsOnly ? new[] { "*.[dD][lL][lL]", "*.[tT][pP][fF]", "*.[zZ][iI][pP]" } : new[] { "[gG][wW].[eE][xX][eE]" })
            {
                Gtk.gtk_file_filter_add_pattern(filter, pattern);
            }

            Gtk.gtk_file_chooser_add_filter(dialog, filter);
            var path = Gtk.gtk_native_dialog_run(dialog) == Gtk.ResponseAccept ? Gtk.TakeString(Gtk.gtk_file_chooser_get_filename(dialog)) : null;
            Gtk.g_object_unref(dialog);
            picked.SetResult(path);
        });
        return picked.Task;
    }

    private void CreateWindow(string iconPath, Action<string> onMessage)
    {
        _window = Gtk.gtk_window_new(0);
        Gtk.gtk_window_set_title(_window, "GW Launcher");
        Gtk.gtk_window_set_icon_from_file(_window, iconPath, IntPtr.Zero);
        Gtk.gtk_window_set_default_size(_window, _width, _height);

        // The page sets each window's size itself. Like the Windows forms, these have no maximise button.
        Gtk.gtk_window_set_resizable(_window, !IsPopup);

        // With the tray in use, closing hides the window and clicking elsewhere dismisses it, like a tray flyout.
        Connect(_window, "delete-event", new Gtk.EventSignal((_, _, _) =>
        {
            if (IsPopup || KeepInTray)
            {
                Hide();
            }
            else
            {
                Quit();
            }

            return true;
        }));
        Connect(_window, "configure-event", new Gtk.EventSignal((_, _, _) =>
        {
            if (!IsPopup && !_closed && Gtk.gtk_widget_get_visible(_window))
            {
                Gtk.gtk_window_get_size(_window, out var width, out var height);
                if ((width, height) != _lastSize)
                {
                    _lastSize = (width, height);
                    _width = width;
                    _height = height;
                    PageSizeChanged?.Invoke(width, height);
                }

                if (!KeepInTray)
                {
                    Gtk.gtk_window_get_position(_window, out var left, out var top);
                    PositionChanged?.Invoke(left, top);
                }
            }

            return false;
        }));
        Connect(_window, "focus-out-event", new Gtk.EventSignal((_, _, _) =>
        {
            if (_isMenu)
            {
                // Focus can leave a menu before it's shown, so only a menu on screen is dismissed by it.
                if (_menuShown)
                {
                    Hide();
                }
            }
            else
            {
                HideUnlessPointerOverNow();
            }

            return false;
        }));

        var manager = Gtk.webkit_user_content_manager_new();
        Connect(manager, "script-message-received::gwl", new Gtk.MessageSignal((_, result, _) =>
            onMessage(Gtk.TakeString(Gtk.jsc_value_to_string(Gtk.webkit_javascript_result_get_js_value(result))) ?? "")));
        Gtk.webkit_user_content_manager_register_script_message_handler(manager, "gwl");

        _view = Gtk.webkit_web_view_new_with_user_content_manager(manager);
        Gtk.gtk_container_add(_window, _view);
        Gtk.webkit_web_view_load_html(_view, WebAssets.Page(), null);
    }

    // Beside the tray icon when there is one, otherwise centred on the primary monitor.
    // EWMH's _NET_WM_ALLOWED_ACTIONS: the window managers that honour it hide the buttons for everything not listed.
    private static void AllowActions(IntPtr gdkWindow, bool resize, bool minimiseAndMaximise)
    {
        var display = Gtk.gdk_x11_get_default_xdisplay();
        var names = new List<string> { "_NET_WM_ACTION_MOVE", "_NET_WM_ACTION_CLOSE" };
        if (resize)
        {
            names.Add("_NET_WM_ACTION_RESIZE");
        }

        if (minimiseAndMaximise)
        {
            names.AddRange(new[] { "_NET_WM_ACTION_MINIMIZE", "_NET_WM_ACTION_MAXIMIZE_HORZ", "_NET_WM_ACTION_MAXIMIZE_VERT" });
        }

        var actions = names.Select(name => (long)Gtk.XInternAtom(display, name, false)).ToArray();
        const int XaAtom = 4;
        const int PropModeReplace = 0;
        Gtk.XChangeProperty(display, Gtk.gdk_x11_window_get_xid(gdkWindow), Gtk.XInternAtom(display, "_NET_WM_ALLOWED_ACTIONS", false),
            (IntPtr)XaAtom, 32, PropModeReplace, actions, actions.Length);
        Gtk.XFlush(display);
    }

    private void Place()
    {
        if (_closed)
        {
            return;
        }

        if (_isMenu)
        {
            PlaceMenu();
            return;
        }

        var display = Gtk.gdk_display_get_default();
        Gtk.gtk_window_get_size(_window, out var contentWidth, out var contentHeight);
        var (frameWidth, frameHeight) = FrameSize();
        var icon = !IsPopup && HasTray ? IconBounds() : null;
        var saved = !IsPopup && !KeepInTray ? SavedPosition() : null;
        var monitor = saved is { } at
            ? Gtk.gdk_display_get_monitor_at_point(display, at.X, at.Y)
            : icon is { } onIcon
                ? Gtk.gdk_display_get_monitor_at_point(display, onIcon.X, onIcon.Y)
                : Gtk.gdk_display_get_primary_monitor(display);
        if (monitor == IntPtr.Zero)
        {
            monitor = Gtk.gdk_display_get_monitor(display, 0);
        }

        Gtk.gdk_monitor_get_workarea(monitor, out var area);
        var workArea = ToScreenRect(area);
        var width = Math.Min(icon is { } && KeepInTray ? WindowPlacement.TrayWidth : contentWidth + frameWidth, workArea.Width);
        var height = Math.Min(contentHeight + frameHeight, workArea.Height);
        var (x, y) = saved is { } placed
            ? WindowPlacement.AtPoint(placed.X, placed.Y, width, height, workArea)
            : icon is { } near
                ? WindowPlacement.NextToIcon(ToScreenRect(near), width, height, workArea)
                : WindowPlacement.Centred(width, height, workArea);

        // Only the page is resized, so the frame is left out. The size is noted so placing it isn't taken for a user resize.
        var content = (width - frameWidth, height - frameHeight);
        if (content != (contentWidth, contentHeight))
        {
            _lastSize = content;
            Gtk.gtk_window_resize(_window, content.Item1, content.Item2);
        }

        Gtk.gtk_window_move(_window, x, y);
    }

    // Where the account list was last left, when it isn't in tray mode.
    private (int X, int Y)? SavedPosition() => _host.Settings.ListX is { } x && _host.Settings.ListY is { } y ? (x, y) : null;

    // The menu is undecorated, so the page's size is the window's. It opens at the pointer, pulled inside the work area.
    private void PlaceMenu()
    {
        var display = Gtk.gdk_display_get_default();
        var monitor = Gtk.gdk_display_get_monitor_at_point(display, _anchor.X, _anchor.Y);
        if (monitor == IntPtr.Zero)
        {
            monitor = Gtk.gdk_display_get_monitor(display, 0);
        }

        Gtk.gdk_monitor_get_workarea(monitor, out var area);
        var workArea = ToScreenRect(area);
        var width = Math.Min(_width, workArea.Width);
        var height = Math.Min(_height, workArea.Height);
        var (x, y) = WindowPlacement.AtPoint(_anchor.X, _anchor.Y, width, height, workArea);
        Gtk.gtk_window_resize(_window, width, height);
        Gtk.gtk_window_move(_window, x, y);
    }

    private static (int X, int Y) PointerPosition()
    {
        var pointer = Gtk.gdk_seat_get_pointer(Gtk.gdk_display_get_default_seat(Gtk.gdk_display_get_default()));
        Gtk.gdk_device_get_position(pointer, out _, out var x, out var y);
        return (x, y);
    }

    // The space the window manager adds around the page: title bar and borders. Zero until the window is on screen.
    private (int Width, int Height) FrameSize()
    {
        var gdkWindow = Gtk.gtk_widget_get_window(_window);
        if (gdkWindow == IntPtr.Zero)
        {
            return (0, 0);
        }

        Gtk.gdk_window_get_frame_extents(gdkWindow, out var frame);
        Gtk.gtk_window_get_size(_window, out var contentWidth, out var contentHeight);
        return (Math.Max(0, frame.Width - contentWidth), Math.Max(0, frame.Height - contentHeight));
    }

    // The icon's place on screen. Null until the panel has embedded it and given it a size; before that GTK reports 1x1 at 0,0.
    private Gtk.Rectangle? IconBounds()
    {
        if (!Gtk.gtk_status_icon_is_embedded(_icon) || !Gtk.gtk_status_icon_get_geometry(_icon, out _, out var icon, out _))
        {
            return null;
        }

        return icon.Width > 1 && icon.Height > 1 ? icon : null;
    }

    private void ToggleFromTray()
    {
        // A click on the icon while the window is open closes it. That click also takes focus, which may have hidden it already.
        if (Gtk.gtk_widget_get_visible(_window) || Environment.TickCount64 - _hiddenAt < 300)
        {
            Hide();
            return;
        }

        Show();
    }

    public void HideUnlessPointerOver() => OnUi(HideUnlessPointerOverNow);

    // Focus moving to the menu from a click on this window isn't a dismissal, so the pointer decides.
    private void HideUnlessPointerOverNow()
    {
        if (!IsPopup && KeepInTray && !_pinned && !PointerOver())
        {
            Hide();
        }
    }

    private bool PointerOver()
    {
        if (!Gtk.gtk_widget_get_visible(_window))
        {
            return false;
        }

        Gtk.gdk_window_get_frame_extents(Gtk.gtk_widget_get_window(_window), out var frame);
        var (x, y) = PointerPosition();
        return x >= frame.X && x < frame.X + frame.Width && y >= frame.Y && y < frame.Y + frame.Height;
    }

    // Work for a window that has closed is dropped, since its GTK widgets are gone.
    private void OnUi(Action work)
    {
        _uiWork.Enqueue(() =>
        {
            if (!_closed)
            {
                work();
            }
        });
        Gtk.g_idle_add(_drainUiWork, IntPtr.Zero);
    }

    private void Every(uint milliseconds, Func<bool> tick)
    {
        Gtk.SourceFunc callback = _ => tick();
        _callbacks.Add(callback);
        Gtk.g_timeout_add(milliseconds, callback, IntPtr.Zero);
    }

    private void Connect(IntPtr instance, string signal, Gtk.Signal handler)
    {
        _callbacks.Add(handler);
        Gtk.g_signal_connect_data(instance, signal, handler, IntPtr.Zero, IntPtr.Zero, 0);
    }

    private void Connect(IntPtr instance, string signal, Gtk.EventSignal handler)
    {
        _callbacks.Add(handler);
        Gtk.g_signal_connect_data(instance, signal, handler, IntPtr.Zero, IntPtr.Zero, 0);
    }

    private void Connect(IntPtr instance, string signal, Gtk.MessageSignal handler)
    {
        _callbacks.Add(handler);
        Gtk.g_signal_connect_data(instance, signal, handler, IntPtr.Zero, IntPtr.Zero, 0);
    }

    private void Connect(IntPtr instance, string signal, Gtk.PopupSignal handler)
    {
        _callbacks.Add(handler);
        Gtk.g_signal_connect_data(instance, signal, handler, IntPtr.Zero, IntPtr.Zero, 0);
    }

    private static ScreenRect ToScreenRect(Gtk.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
