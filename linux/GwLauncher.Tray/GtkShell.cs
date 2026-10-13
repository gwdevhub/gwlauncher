using System.Collections.Concurrent;
using GwLauncher.Linux;
using GwLauncher.Tray.Native;
using GwLauncher.Ui;

namespace GwLauncher.Tray;

// The Linux window: a GTK window holding a WebKitGTK view of the shared page, and a tray icon whose position GTK reports,
// so the window opens right beside it.
internal sealed class GtkShell : IShell
{
    private readonly ILauncherHost _host;
    private readonly ConcurrentQueue<Action> _uiWork = new();

    // Delegates handed to GTK are kept here, so the garbage collector can't free one GTK still calls.
    private readonly List<Delegate> _callbacks = new();
    private readonly Gtk.SourceFunc _drainUiWork;
    private readonly IntPtr _positionMenu;
    private LauncherBridge _bridge = null!;
    private IntPtr _window;
    private IntPtr _view;
    private IntPtr _icon;
    private IntPtr _menu;
    private int _width = 220;
    private int _height = 340;
    private bool _pinned;
    private long _hiddenAt;

    public GtkShell(ILauncherHost host)
    {
        _host = host;
        _drainUiWork = _ =>
        {
            while (_uiWork.TryDequeue(out var work))
            {
                work();
            }

            return false;
        };
        _positionMenu = System.Runtime.InteropServices.NativeLibrary.GetExport(
            System.Runtime.InteropServices.NativeLibrary.Load("libgtk-3.so.0"), "gtk_status_icon_position_menu");
    }

    public bool HasTray { get; private set; }

    public bool Pinned
    {
        set => _pinned = value;
    }

    private bool KeepInTray => HasTray && _host.Settings.KeepInSystemTray;

    // Builds the window and tray icon, then runs GTK until Quit.
    public void Run(LauncherBridge bridge, bool showAtStart)
    {
        _bridge = bridge;
        var iconPath = WebAssets.SaveIcon(AppPaths.IconFile);
        CreateWindow(iconPath);

        _icon = Gtk.gtk_status_icon_new_from_file(iconPath);
        Gtk.gtk_status_icon_set_tooltip_text(_icon, "GW Launcher");
        Connect(_icon, "activate", new Gtk.Signal((_, _) => ToggleFromTray()));
        Connect(_icon, "popup-menu", new Gtk.PopupSignal((_, button, time, _) => PopupMenu(button, time)));

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
            Gtk.gtk_status_icon_set_visible(_icon, HasTray);
            _host.StartBackground();
            if (showAtStart || !KeepInTray || !_host.Settings.LaunchMinimized)
            {
                Show();
            }

            return false;
        });

        Gtk.gtk_main();
    }

    public void Post(string json) => OnUi(() => Gtk.webkit_web_view_run_javascript(_view, $"gwlReceive({json})", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));

    public void Show() => OnUi(() =>
    {
        Gtk.gtk_window_set_skip_taskbar_hint(_window, KeepInTray);
        Gtk.gtk_window_resize(_window, _width, _height);
        Place();
        Gtk.gtk_widget_show_all(_window);
        Gtk.gtk_window_present(_window);

        // The title bar's height is only known once the window is on screen, so it's placed again then.
        Every(50, () =>
        {
            Place();
            return false;
        });
    });

    public void Hide() => OnUi(() =>
    {
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

    private void CreateWindow(string iconPath)
    {
        _window = Gtk.gtk_window_new(0);
        Gtk.gtk_window_set_title(_window, "GW Launcher");
        Gtk.gtk_window_set_icon_from_file(_window, iconPath, IntPtr.Zero);
        Gtk.gtk_window_set_default_size(_window, _width, _height);

        // With the tray in use, closing hides the window and clicking elsewhere dismisses it, like a tray flyout.
        Connect(_window, "delete-event", new Gtk.EventSignal((_, _, _) =>
        {
            if (KeepInTray)
            {
                Hide();
            }
            else
            {
                Quit();
            }

            return true;
        }));
        Connect(_window, "focus-out-event", new Gtk.EventSignal((_, _, _) =>
        {
            if (KeepInTray && !_pinned)
            {
                Hide();
            }

            return false;
        }));

        var manager = Gtk.webkit_user_content_manager_new();
        Connect(manager, "script-message-received::gwl", new Gtk.MessageSignal((_, result, _) =>
            _bridge.Receive(Gtk.TakeString(Gtk.jsc_value_to_string(Gtk.webkit_javascript_result_get_js_value(result))) ?? "")));
        Gtk.webkit_user_content_manager_register_script_message_handler(manager, "gwl");

        _view = Gtk.webkit_web_view_new_with_user_content_manager(manager);
        Gtk.gtk_container_add(_window, _view);
        Gtk.webkit_web_view_load_html(_view, WebAssets.Page(), null);
    }

    // Beside the tray icon when there is one, otherwise centred on the primary monitor.
    private void Place()
    {
        var display = Gtk.gdk_display_get_default();
        var (width, height) = OuterSize();
        int x, y;
        if (HasTray && IconBounds() is { } icon)
        {
            Gtk.gdk_monitor_get_workarea(Gtk.gdk_display_get_monitor_at_point(display, icon.X, icon.Y), out var area);
                (x, y) = WindowPlacement.NextToIcon(ToScreenRect(icon), width, height, ToScreenRect(area));
        }
        else
        {
            var monitor = Gtk.gdk_display_get_primary_monitor(display);
            Gtk.gdk_monitor_get_workarea(monitor != IntPtr.Zero ? monitor : Gtk.gdk_display_get_monitor(display, 0), out var area);
            (x, y) = WindowPlacement.Centred(width, height, ToScreenRect(area));
        }

        Gtk.gtk_window_move(_window, x, y);
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

    // The window's size with its title bar and borders, once it's on screen. Until then, the page size.
    private (int Width, int Height) OuterSize()
    {
        var gdkWindow = Gtk.gtk_widget_get_window(_window);
        if (gdkWindow == IntPtr.Zero)
        {
            return (_width, _height);
        }

        Gtk.gdk_window_get_frame_extents(gdkWindow, out var frame);
        return (Math.Max(frame.Width, _width), Math.Max(frame.Height, _height));
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

    // The menu is rebuilt each time it opens, so it always lists the current accounts.
    private void PopupMenu(uint button, uint time)
    {
        if (_menu != IntPtr.Zero)
        {
            Gtk.gtk_widget_destroy(_menu);
        }

        _menu = Gtk.gtk_menu_new();
        foreach (var item in TrayMenu.Build(_host, _bridge, this))
        {
            var widget = item.Separator ? Gtk.gtk_separator_menu_item_new() : Gtk.gtk_menu_item_new_with_label(item.Text);
            if (item.OnClick is { } onClick)
            {
                Connect(widget, "activate", new Gtk.Signal((_, _) => onClick()));
            }
            else if (!item.Separator)
            {
                Gtk.gtk_widget_set_sensitive(widget, false);
            }

            Gtk.gtk_menu_shell_append(_menu, widget);
        }

        Gtk.gtk_widget_show_all(_menu);
        Gtk.gtk_menu_popup(_menu, IntPtr.Zero, IntPtr.Zero, _positionMenu, _icon, button, time);
    }

    private void OnUi(Action work)
    {
        _uiWork.Enqueue(work);
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
