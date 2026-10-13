using System.Runtime.InteropServices;

namespace GwLauncher.Tray.Native;

// The GTK 3, GDK, GLib and WebKit2GTK 4.1 calls the shell uses. Called directly, so the launcher needs no binding library.
internal static class Gtk
{
    public const string WebKitLibrary = "libwebkit2gtk-4.1.so.0";
    private const string GtkLibrary = "libgtk-3.so.0";
    private const string GdkLibrary = "libgdk-3.so.0";
    private const string GLibLibrary = "libglib-2.0.so.0";
    private const string GObjectLibrary = "libgobject-2.0.so.0";
    private const string JavaScriptCoreLibrary = "libjavascriptcoregtk-4.1.so.0";

    public const int ResponseAccept = -3;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rectangle
    {
        public int X, Y, Width, Height;
    }

    public delegate bool SourceFunc(IntPtr data);
    public delegate void Signal(IntPtr instance, IntPtr data);
    public delegate bool EventSignal(IntPtr widget, IntPtr gdkEvent, IntPtr data);
    public delegate void MessageSignal(IntPtr manager, IntPtr result, IntPtr data);
    public delegate void PopupSignal(IntPtr icon, uint button, uint time, IntPtr data);

    // Main loop
    [DllImport(GtkLibrary)] public static extern bool gtk_init_check(IntPtr argc, IntPtr argv);
    [DllImport(GtkLibrary)] public static extern void gtk_main();
    [DllImport(GtkLibrary)] public static extern void gtk_main_quit();
    [DllImport(GtkLibrary)] public static extern bool gtk_events_pending();
    [DllImport(GtkLibrary)] public static extern bool gtk_main_iteration();
    [DllImport(GLibLibrary)] public static extern uint g_idle_add(SourceFunc function, IntPtr data);
    [DllImport(GLibLibrary)] public static extern uint g_timeout_add(uint interval, SourceFunc function, IntPtr data);
    [DllImport(GLibLibrary)] public static extern void g_free(IntPtr memory);
    [DllImport(GObjectLibrary)] public static extern void g_object_unref(IntPtr instance);
    [DllImport(GObjectLibrary)] public static extern ulong g_signal_connect_data(IntPtr instance, string signal, Signal handler, IntPtr data, IntPtr destroy, int flags);
    [DllImport(GObjectLibrary)] public static extern ulong g_signal_connect_data(IntPtr instance, string signal, EventSignal handler, IntPtr data, IntPtr destroy, int flags);
    [DllImport(GObjectLibrary)] public static extern ulong g_signal_connect_data(IntPtr instance, string signal, MessageSignal handler, IntPtr data, IntPtr destroy, int flags);
    [DllImport(GObjectLibrary)] public static extern ulong g_signal_connect_data(IntPtr instance, string signal, PopupSignal handler, IntPtr data, IntPtr destroy, int flags);

    // Window
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_window_new(int type);
    [DllImport(GtkLibrary)] public static extern void gtk_window_set_title(IntPtr window, string title);
    [DllImport(GtkLibrary)] public static extern bool gtk_window_set_icon_from_file(IntPtr window, string path, IntPtr error);
    [DllImport(GtkLibrary)] public static extern void gtk_window_set_default_size(IntPtr window, int width, int height);
    [DllImport(GtkLibrary)] public static extern void gtk_window_resize(IntPtr window, int width, int height);
    [DllImport(GtkLibrary)] public static extern void gtk_window_move(IntPtr window, int x, int y);
    [DllImport(GtkLibrary)] public static extern void gtk_window_set_skip_taskbar_hint(IntPtr window, bool skip);
    [DllImport(GtkLibrary)] public static extern void gtk_window_present(IntPtr window);
    [DllImport(GtkLibrary)] public static extern void gtk_container_add(IntPtr container, IntPtr child);
    [DllImport(GtkLibrary)] public static extern void gtk_widget_show_all(IntPtr widget);
    [DllImport(GtkLibrary)] public static extern void gtk_widget_hide(IntPtr widget);
    [DllImport(GtkLibrary)] public static extern bool gtk_widget_get_visible(IntPtr widget);
    [DllImport(GtkLibrary)] public static extern void gtk_widget_destroy(IntPtr widget);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_widget_get_window(IntPtr widget);
    [DllImport(GdkLibrary)] public static extern void gdk_window_get_frame_extents(IntPtr window, out Rectangle rect);

    // Tray icon and menu
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_status_icon_new_from_file(string path);
    [DllImport(GtkLibrary)] public static extern void gtk_status_icon_set_tooltip_text(IntPtr icon, string text);
    [DllImport(GtkLibrary)] public static extern void gtk_status_icon_set_visible(IntPtr icon, bool visible);
    [DllImport(GtkLibrary)] public static extern bool gtk_status_icon_is_embedded(IntPtr icon);
    [DllImport(GtkLibrary)] public static extern bool gtk_status_icon_get_geometry(IntPtr icon, out IntPtr screen, out Rectangle area, out int orientation);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_menu_new();
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_menu_item_new_with_label(string label);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_separator_menu_item_new();
    [DllImport(GtkLibrary)] public static extern void gtk_menu_shell_append(IntPtr menu, IntPtr item);
    [DllImport(GtkLibrary)] public static extern void gtk_widget_set_sensitive(IntPtr widget, bool sensitive);
    [DllImport(GtkLibrary)] public static extern void gtk_menu_popup(IntPtr menu, IntPtr parentShell, IntPtr parentItem, IntPtr positionFunc, IntPtr data, uint button, uint time);

    // Dialogs and clipboard
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_message_dialog_new(IntPtr parent, int flags, int type, int buttons, string format);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_dialog_add_button(IntPtr dialog, string text, int response);
    [DllImport(GtkLibrary)] public static extern int gtk_dialog_run(IntPtr dialog);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_file_chooser_native_new(string title, IntPtr parent, int action, string accept, string cancel);
    [DllImport(GtkLibrary)] public static extern int gtk_native_dialog_run(IntPtr dialog);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_file_chooser_get_filename(IntPtr chooser);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_file_filter_new();
    [DllImport(GtkLibrary)] public static extern void gtk_file_filter_set_name(IntPtr filter, string name);
    [DllImport(GtkLibrary)] public static extern void gtk_file_filter_add_pattern(IntPtr filter, string pattern);
    [DllImport(GtkLibrary)] public static extern void gtk_file_chooser_add_filter(IntPtr chooser, IntPtr filter);
    [DllImport(GtkLibrary)] public static extern IntPtr gtk_clipboard_get(IntPtr selection);
    [DllImport(GtkLibrary)] public static extern void gtk_clipboard_set_text(IntPtr clipboard, string text, int length);
    [DllImport(GdkLibrary)] public static extern IntPtr gdk_atom_intern(string name, bool onlyIfExists);

    // Monitors
    [DllImport(GdkLibrary)] public static extern IntPtr gdk_display_get_default();
    [DllImport(GdkLibrary)] public static extern IntPtr gdk_display_get_monitor_at_point(IntPtr display, int x, int y);
    [DllImport(GdkLibrary)] public static extern IntPtr gdk_display_get_primary_monitor(IntPtr display);
    [DllImport(GdkLibrary)] public static extern IntPtr gdk_display_get_monitor(IntPtr display, int index);
    [DllImport(GdkLibrary)] public static extern void gdk_monitor_get_workarea(IntPtr monitor, out Rectangle area);

    // WebKit
    [DllImport(WebKitLibrary)] public static extern IntPtr webkit_user_content_manager_new();
    [DllImport(WebKitLibrary)] public static extern bool webkit_user_content_manager_register_script_message_handler(IntPtr manager, string name);
    [DllImport(WebKitLibrary)] public static extern IntPtr webkit_web_view_new_with_user_content_manager(IntPtr manager);
    [DllImport(WebKitLibrary)] public static extern void webkit_web_view_load_html(IntPtr view, string html, string? baseUri);
    [DllImport(WebKitLibrary)] public static extern void webkit_web_view_run_javascript(IntPtr view, string script, IntPtr cancellable, IntPtr callback, IntPtr data);
    [DllImport(WebKitLibrary)] public static extern IntPtr webkit_javascript_result_get_js_value(IntPtr result);
    [DllImport(JavaScriptCoreLibrary)] public static extern IntPtr jsc_value_to_string(IntPtr value);

    // Reads and frees a string GLib allocated.
    public static string? TakeString(IntPtr text)
    {
        if (text == IntPtr.Zero)
        {
            return null;
        }

        var value = Marshal.PtrToStringUTF8(text);
        g_free(text);
        return value;
    }

    // Lets GTK finish drawing, such as closing a dialog, before blocking work starts.
    public static void Pump()
    {
        while (gtk_events_pending())
        {
            gtk_main_iteration();
        }
    }
}
