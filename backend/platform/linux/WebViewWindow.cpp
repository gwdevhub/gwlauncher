// Linux: GTK 3 and WebKitGTK 4.1. The libraries are opened when the first window is made, so the program starts without
// them and reports the missing library when it's needed. Only the headers are used at build time.

#include <dlfcn.h>

#include <gtk/gtk.h>
#include <webkit2/webkit2.h>

#include <stdexcept>
#include <string>

#include "WebViewWindow.h"

namespace gwl {

struct NativeWindow {
    GtkWidget* window = nullptr;
    WebKitWebView* view = nullptr;
    WebKitUserContentManager* manager = nullptr;
    // The window stays hidden until its page has loaded, so it never shows as an empty frame.
    bool pageLoaded = false;
    bool wantsVisible = false;
    bool wantsFocus = false;
};

namespace {

// The launcher's icon, set by setApplicationIcon and given to each window as it opens.
GdkPixbuf* applicationIcon = nullptr;

// The GTK and WebKitGTK functions this window uses, resolved from the opened library.
struct GtkApi {
    gboolean (*init_check)(int*, char***) = nullptr;
    GtkWidget* (*window_new)(GtkWindowType) = nullptr;
    void (*window_set_title)(GtkWindow*, const gchar*) = nullptr;
    void (*window_set_default_size)(GtkWindow*, gint, gint) = nullptr;
    void (*window_set_position)(GtkWindow*, GtkWindowPosition) = nullptr;
    void (*window_resize)(GtkWindow*, gint, gint) = nullptr;
    void (*window_move)(GtkWindow*, gint, gint) = nullptr;
    void (*window_get_position)(GtkWindow*, gint*, gint*) = nullptr;
    void (*window_get_size)(GtkWindow*, gint*, gint*) = nullptr;
    void (*window_set_decorated)(GtkWindow*, gboolean) = nullptr;
    void (*window_set_skip_taskbar_hint)(GtkWindow*, gboolean) = nullptr;
    void (*window_set_keep_above)(GtkWindow*, gboolean) = nullptr;
    void (*window_present)(GtkWindow*) = nullptr;
    void (*window_present_with_time)(GtkWindow*, guint32) = nullptr;
    void (*window_deiconify)(GtkWindow*) = nullptr;
    void (*window_set_icon)(GtkWindow*, GdkPixbuf*) = nullptr;
    void (*container_add)(GtkContainer*, GtkWidget*) = nullptr;
    void (*widget_show)(GtkWidget*) = nullptr;
    void (*widget_show_all)(GtkWidget*) = nullptr;
    GdkWindow* (*widget_get_window)(GtkWidget*) = nullptr;
    guint32 (*x11_server_time)(GdkWindow*) = nullptr;
    void (*window_begin_resize_drag)(GtkWindow*, GdkWindowEdge, gint, gint, gint, guint32) = nullptr;
    void (*window_begin_move_drag)(GtkWindow*, gint, gint, gint, guint32) = nullptr;
    void (*window_iconify)(GtkWindow*) = nullptr;
    void (*window_maximize)(GtkWindow*) = nullptr;
    void (*window_unmaximize)(GtkWindow*) = nullptr;
    gboolean (*window_is_maximized)(GtkWindow*) = nullptr;
    void (*window_close)(GtkWindow*) = nullptr;
    void (*widget_hide)(GtkWidget*) = nullptr;
    void (*widget_destroy)(GtkWidget*) = nullptr;
    void (*main_loop)() = nullptr;
    void (*main_quit)() = nullptr;
    guint (*idle_add)(GSourceFunc, gpointer) = nullptr;
    WebKitUserContentManager* (*user_content_manager_new)() = nullptr;
    gboolean (*user_content_manager_register_script_message_handler_with_reply)(WebKitUserContentManager*, const gchar*,
                                                                                const gchar*) = nullptr;
    GtkWidget* (*web_view_new_with_user_content_manager)(WebKitUserContentManager*) = nullptr;
    void (*web_view_load_html)(WebKitWebView*, const gchar*, const gchar*) = nullptr;
    void (*web_view_load_uri)(WebKitWebView*, const gchar*) = nullptr;
    void (*web_view_run_javascript)(WebKitWebView*, const gchar*, GCancellable*, GAsyncReadyCallback, gpointer) = nullptr;
    WebKitSettings* (*web_view_get_settings)(WebKitWebView*) = nullptr;
    void (*settings_set_enable_developer_extras)(WebKitSettings*, gboolean) = nullptr;
    WebKitWebInspector* (*web_view_get_inspector)(WebKitWebView*) = nullptr;
    void (*web_inspector_show)(WebKitWebInspector*) = nullptr;
    gulong (*g_signal_connect_data)(gpointer, const gchar*, GCallback, gpointer, GClosureNotify, GConnectFlags) = nullptr;
    void (*g_free)(gpointer) = nullptr;
    char* (*jsc_value_to_string)(JSCValue*) = nullptr;
};

template <typename F>
void bind(F& slot, void* library, const char* name) {
    void* symbol = dlsym(library, name);
    if (!symbol) {
        throw std::runtime_error(std::string("WebKitGTK 4.1 is missing ") + name + ".");
    }
    slot = reinterpret_cast<F>(symbol);
}

GtkApi loadApi() {
    // The WebKitGTK library depends on GTK and GLib, so symbols from those resolve through this handle too.
    void* library = dlopen("libwebkit2gtk-4.1.so.0", RTLD_LAZY | RTLD_LOCAL);
    if (!library) {
        throw std::runtime_error(std::string("WebKitGTK 4.1 couldn't be opened: ") + dlerror());
    }
    GtkApi api;
    bind(api.init_check, library, "gtk_init_check");
    bind(api.window_new, library, "gtk_window_new");
    bind(api.window_set_title, library, "gtk_window_set_title");
    bind(api.window_set_default_size, library, "gtk_window_set_default_size");
    bind(api.window_set_position, library, "gtk_window_set_position");
    bind(api.window_resize, library, "gtk_window_resize");
    bind(api.window_move, library, "gtk_window_move");
    bind(api.window_get_position, library, "gtk_window_get_position");
    bind(api.window_get_size, library, "gtk_window_get_size");
    bind(api.window_set_decorated, library, "gtk_window_set_decorated");
    bind(api.window_set_skip_taskbar_hint, library, "gtk_window_set_skip_taskbar_hint");
    bind(api.window_set_keep_above, library, "gtk_window_set_keep_above");
    bind(api.window_present, library, "gtk_window_present");
    bind(api.window_present_with_time, library, "gtk_window_present_with_time");
    bind(api.window_deiconify, library, "gtk_window_deiconify");
    bind(api.window_begin_resize_drag, library, "gtk_window_begin_resize_drag");
    bind(api.window_begin_move_drag, library, "gtk_window_begin_move_drag");
    bind(api.window_iconify, library, "gtk_window_iconify");
    bind(api.window_maximize, library, "gtk_window_maximize");
    bind(api.window_unmaximize, library, "gtk_window_unmaximize");
    bind(api.window_is_maximized, library, "gtk_window_is_maximized");
    bind(api.window_close, library, "gtk_window_close");
    bind(api.window_set_icon, library, "gtk_window_set_icon");
    bind(api.container_add, library, "gtk_container_add");
    bind(api.widget_show, library, "gtk_widget_show");
    bind(api.widget_show_all, library, "gtk_widget_show_all");
    bind(api.widget_get_window, library, "gtk_widget_get_window");
    // Only on X11. Without it the focus request carries no time, and the window manager may refuse it.
    api.x11_server_time = reinterpret_cast<decltype(api.x11_server_time)>(dlsym(library, "gdk_x11_get_server_time"));
    bind(api.widget_hide, library, "gtk_widget_hide");
    bind(api.widget_destroy, library, "gtk_widget_destroy");
    bind(api.main_loop, library, "gtk_main");
    bind(api.main_quit, library, "gtk_main_quit");
    bind(api.idle_add, library, "g_idle_add");
    bind(api.user_content_manager_new, library, "webkit_user_content_manager_new");
    bind(api.user_content_manager_register_script_message_handler_with_reply, library,
         "webkit_user_content_manager_register_script_message_handler_with_reply");
    bind(api.web_view_new_with_user_content_manager, library, "webkit_web_view_new_with_user_content_manager");
    bind(api.web_view_load_html, library, "webkit_web_view_load_html");
    bind(api.web_view_load_uri, library, "webkit_web_view_load_uri");
    bind(api.web_view_run_javascript, library, "webkit_web_view_run_javascript");
    bind(api.web_view_get_settings, library, "webkit_web_view_get_settings");
    bind(api.settings_set_enable_developer_extras, library, "webkit_settings_set_enable_developer_extras");
    bind(api.web_view_get_inspector, library, "webkit_web_view_get_inspector");
    bind(api.web_inspector_show, library, "webkit_web_inspector_show");
    bind(api.g_signal_connect_data, library, "g_signal_connect_data");
    bind(api.g_free, library, "g_free");
    bind(api.jsc_value_to_string, library, "jsc_value_to_string");
    return api;
}

const GtkApi& api() {
    static const GtkApi loaded = loadApi();
    return loaded;
}

void ensureGtk() {
    static const bool initialized = [] {
        if (!api().init_check(nullptr, nullptr)) {
            throw std::runtime_error("GTK couldn't open a display.");
        }
        return true;
    }();
    (void)initialized;
}

// A script message from the page: window.webkit.messageHandlers.gwl.postMessage(text).
void onScriptMessage(WebKitUserContentManager*, JSCValue* value, WebKitUserMessage*, gpointer data) {
    auto* window = static_cast<WebViewWindow*>(data);
    char* text = api().jsc_value_to_string(value);
    if (text) {
        window->receive(text);
        api().g_free(text);
    }
}

// Returning TRUE keeps the window open. The window decides, through requestClose.
gboolean onFocusOut(GtkWidget*, GdkEvent*, gpointer data) {
    static_cast<WebViewWindow*>(data)->notifyFocusLost();
    return FALSE;
}

gboolean onDeleteEvent(GtkWidget*, GdkEvent*, gpointer data) {
    // A close hides the window, so GTK keeps it.
    static_cast<WebViewWindow*>(data)->requestClose();
    return TRUE;
}

// The X server's time, which a window manager needs on a focus or move request. GDK_CURRENT_TIME when it isn't available.
guint32 serverTime(GtkWidget* widget) {
    if (!api().x11_server_time) {
        return GDK_CURRENT_TIME;
    }
    GdkWindow* gdkWindow = api().widget_get_window(widget);
    return gdkWindow ? api().x11_server_time(gdkWindow) : GDK_CURRENT_TIME;
}

// Shows the window once its page has loaded, if it's wanted yet. Focus goes to it only if it was asked for.
void showLoadedPage(NativeWindow* native) {
    native->pageLoaded = true;
    if (!native->window || !native->wantsVisible) {
        return;
    }
    api().widget_show_all(native->window);
    if (native->wantsFocus) {
        native->wantsFocus = false;
        api().window_present_with_time(reinterpret_cast<GtkWindow*>(native->window), serverTime(native->window));
    }
}

void onLoadChanged(WebKitWebView*, WebKitLoadEvent event, gpointer data) {
    if (event == WEBKIT_LOAD_FINISHED) {
        showLoadedPage(static_cast<NativeWindow*>(data));
    }
}

}  // namespace

void NativeWindowDeleter::operator()(NativeWindow* native) const {
    if (native && native->window) {
        api().widget_destroy(native->window);
    }
    delete native;
}

void WebViewWindow::platformOpen() {
    ensureGtk();
    const GtkApi& gtk = api();
    native_.reset(new NativeWindow);
    NativeWindow& native = *native_;

    native.window = gtk.window_new(GTK_WINDOW_TOPLEVEL);
    auto* window = reinterpret_cast<GtkWindow*>(native.window);
    gtk.window_set_title(window, title().c_str());
    gtk.window_set_default_size(window, scaledSize(options_.width), scaledSize(options_.height));
    // Opened in the middle of the screen. A caller that places the window moves it after it's opened.
    gtk.window_set_position(window, GTK_WIN_POS_CENTER);
    // The page draws the title bar, so the window manager draws none.
    gtk.window_set_decorated(window, FALSE);
    // Left resizable even for an autosize window: GTK keeps a fixed-size web view window at least 200 pixels square, and
    // a frameless window offers the user no edges unless the page draws them.
    gtk.window_set_skip_taskbar_hint(window, options_.skipTaskbar);
    gtk.window_set_keep_above(window, options_.alwaysOnTop);
    // The launcher's icon. Set on each window: the taskbar and the window list read the window's own icon.
    if (applicationIcon) {
        gtk.window_set_icon(window, applicationIcon);
    }

    native.manager = gtk.user_content_manager_new();
    // The signal is connected before the handler is registered, as WebKitGTK recommends.
    gtk.g_signal_connect_data(native.manager, "script-message-with-reply-received::gwl",
                              reinterpret_cast<GCallback>(onScriptMessage), this, nullptr, static_cast<GConnectFlags>(0));
    gtk.user_content_manager_register_script_message_handler_with_reply(native.manager, "gwl", nullptr);
    native.view = reinterpret_cast<WebKitWebView*>(gtk.web_view_new_with_user_content_manager(native.manager));
    gtk.container_add(reinterpret_cast<GtkContainer*>(native.window), reinterpret_cast<GtkWidget*>(native.view));
    native.wantsVisible = options_.visible;
    gtk.g_signal_connect_data(native.view, "load-changed", reinterpret_cast<GCallback>(onLoadChanged), &native, nullptr,
                              static_cast<GConnectFlags>(0));

    gtk.g_signal_connect_data(native.window, "delete-event", reinterpret_cast<GCallback>(onDeleteEvent), this, nullptr,
                              static_cast<GConnectFlags>(0));
    // The window is closing: GTK destroys it after this, so the NativeWindow must stop pointing at it.
    gtk.g_signal_connect_data(native.window, "destroy", reinterpret_cast<GCallback>(+[](GtkWidget*, gpointer data) {
                                  static_cast<NativeWindow*>(data)->window = nullptr;
                              }), &native, nullptr,
                              static_cast<GConnectFlags>(0));
    gtk.g_signal_connect_data(native.window, "focus-out-event", reinterpret_cast<GCallback>(onFocusOut), this, nullptr,
                              static_cast<GConnectFlags>(0));

    const std::string url = devUrl();
    if (!url.empty()) {
        gtk.web_view_load_uri(native.view, url.c_str());
    } else {
        gtk.web_view_load_html(native.view, html().c_str(), nullptr);
    }
}

void WebViewWindow::platformUnload() {
    native_.reset();
}

void WebViewWindow::platformPresent() {
    if (!native_ || !native_->window) {
        return;
    }
    native_->wantsVisible = true;
    if (!native_->pageLoaded) {
        native_->wantsFocus = true;
        return;
    }
    // Restores it from minimised, shows it if it's hidden, and asks the window manager to raise it and give it the focus. The
    // timestamp is the current time, since a window manager may ignore a request that has none.
    auto* window = reinterpret_cast<GtkWindow*>(native_->window);
    api().window_deiconify(window);
    api().widget_show_all(native_->window);
    api().window_present_with_time(window, serverTime(native_->window));
}

// The drags below follow the left button the page saw pressed, from where the pointer is now.
void WebViewWindow::platformBeginMove() {
    const auto pointer = cursorPosition();
    if (native_ && native_->window && pointer) {
        api().window_begin_move_drag(reinterpret_cast<GtkWindow*>(native_->window), 1, pointer->first, pointer->second,
                                     serverTime(native_->window));
    }
}

void WebViewWindow::platformBeginResize(ResizeEdge edge) {
    const auto pointer = cursorPosition();
    if (native_ && native_->window && pointer) {
        api().window_begin_resize_drag(reinterpret_cast<GtkWindow*>(native_->window), static_cast<GdkWindowEdge>(edge), 1,
                                       pointer->first, pointer->second, serverTime(native_->window));
    }
}

void WebViewWindow::platformMinimize() {
    if (native_ && native_->window) {
        api().window_iconify(reinterpret_cast<GtkWindow*>(native_->window));
    }
}

void WebViewWindow::platformToggleMaximize() {
    if (native_ && native_->window) {
        auto* window = reinterpret_cast<GtkWindow*>(native_->window);
        if (api().window_is_maximized(window)) {
            api().window_unmaximize(window);
        } else {
            api().window_maximize(window);
        }
    }
}

// Sends the delete event the window manager's close would, so onDeleteEvent asks requestClose.
void WebViewWindow::platformClose() {
    if (native_ && native_->window) {
        api().window_close(reinterpret_cast<GtkWindow*>(native_->window));
    }
}

void WebViewWindow::platformShow(bool visible) {
    if (!native_ || !native_->window) {
        return;
    }
    native_->wantsVisible = visible;
    if (!native_->pageLoaded) {
        return;
    }
    if (visible) {
        api().widget_show_all(native_->window);
    } else {
        api().widget_hide(native_->window);
    }
}

void WebViewWindow::platformResize(int width, int height) {
    if (native_ && native_->window) {
        api().window_resize(reinterpret_cast<GtkWindow*>(native_->window), width, height);
    }
}

void WebViewWindow::platformMove(int x, int y) {
    if (native_ && native_->window) {
        api().window_move(reinterpret_cast<GtkWindow*>(native_->window), x, y);
    }
}

// GTK sizes are already in the units resize takes: the display's scale is GTK's own.
int WebViewWindow::platformToIndependent(int pixels) const {
    return pixels;
}

std::optional<Rect> WebViewWindow::platformFrame() const {
    if (!native_ || !native_->window) {
        return std::nullopt;
    }
    auto* window = reinterpret_cast<GtkWindow*>(native_->window);
    gint x = 0;
    gint y = 0;
    gint width = 0;
    gint height = 0;
    api().window_get_position(window, &x, &y);
    api().window_get_size(window, &width, &height);
    return Rect{x, y, width, height};
}

void WebViewWindow::platformSetSkipTaskbar() {
    if (native_ && native_->window) {
        api().window_set_skip_taskbar_hint(reinterpret_cast<GtkWindow*>(native_->window), options_.skipTaskbar);
    }
}

void WebViewWindow::platformSetTitle(const std::string& title) {
    if (native_ && native_->window) {
        api().window_set_title(reinterpret_cast<GtkWindow*>(native_->window), title.c_str());
    }
}

void WebViewWindow::platformOpenDevTools() {
    if (native_ && native_->view) {
        // WebKitGTK keeps the inspector off until its developer extras are on.
        api().settings_set_enable_developer_extras(api().web_view_get_settings(native_->view), TRUE);
        api().web_inspector_show(api().web_view_get_inspector(native_->view));
    }
}

void WebViewWindow::platformRunScript(const std::string& script) {
    if (native_ && native_->view) {
        api().web_view_run_javascript(native_->view, script.c_str(), nullptr, nullptr, nullptr);
    }
}

std::optional<RuntimeProblem> missingWindowingRuntime() {
    if (!dlopen("libgtk-3.so.0", RTLD_LAZY | RTLD_LOCAL)) {
        return RuntimeProblem{"GTK 3 isn't installed, so the launcher's windows can't open. Install GTK 3 (libgtk-3-0 on Debian and Ubuntu, gtk3 on Fedora and Arch) and start the launcher again.", ""};
    }
    if (!dlopen("libwebkit2gtk-4.1.so.0", RTLD_LAZY | RTLD_LOCAL)) {
        return RuntimeProblem{"WebKitGTK 4.1 isn't installed, so the launcher's windows can't open. Install it (libwebkit2gtk-4.1-0 on Debian and Ubuntu, webkit2gtk4.1 on Fedora, webkit2gtk-4.1 on Arch) and start the launcher again.", ""};
    }
    try {
        if (!api().init_check(nullptr, nullptr)) {
            return RuntimeProblem{"GTK couldn't open a display. Start the launcher from a desktop session.", ""};
        }
    } catch (const std::exception& error) {
        return RuntimeProblem{std::string("The windowing libraries are incomplete: ") + error.what(), ""};
    }
    return std::nullopt;
}

void offerLink(const std::string& message, const std::string&) {
    // Linux has no runtime that's offered as a download, so the message is all there is to show.
    showError(message);
}

void initializeWindowing() {
    requireWindowingRuntime();
    ensureGtk();
}

void setApplicationIcon(const std::string& png) {
    ensureGtk();
    void* library = dlopen("libgtk-3.so.0", RTLD_LAZY | RTLD_LOCAL);
    if (!library) {
        return;
    }
    using LoaderNew = GdkPixbufLoader* (*)();
    using LoaderWrite = gboolean (*)(GdkPixbufLoader*, const guchar*, gsize, GError**);
    using LoaderClose = gboolean (*)(GdkPixbufLoader*, GError**);
    using LoaderPixbuf = GdkPixbuf* (*)(GdkPixbufLoader*);
    using Scale = GdkPixbuf* (*)(const GdkPixbuf*, gint, gint, GdkInterpType);
    using Unref = void (*)(gpointer);
    using Size = int (*)(const GdkPixbuf*);
    auto loaderNew = reinterpret_cast<LoaderNew>(dlsym(library, "gdk_pixbuf_loader_new"));
    auto loaderWrite = reinterpret_cast<LoaderWrite>(dlsym(library, "gdk_pixbuf_loader_write"));
    auto loaderClose = reinterpret_cast<LoaderClose>(dlsym(library, "gdk_pixbuf_loader_close"));
    auto loaderPixbuf = reinterpret_cast<LoaderPixbuf>(dlsym(library, "gdk_pixbuf_loader_get_pixbuf"));
    auto scale = reinterpret_cast<Scale>(dlsym(library, "gdk_pixbuf_scale_simple"));
    auto unref = reinterpret_cast<Unref>(dlsym(library, "g_object_unref"));
    auto width = reinterpret_cast<Size>(dlsym(library, "gdk_pixbuf_get_width"));
    auto height = reinterpret_cast<Size>(dlsym(library, "gdk_pixbuf_get_height"));
    if (!loaderNew || !loaderWrite || !loaderClose || !loaderPixbuf || !scale || !unref || !width || !height) {
        return;
    }
    GdkPixbufLoader* loader = loaderNew();
    const bool written = loaderWrite(loader, reinterpret_cast<const guchar*>(png.data()), png.size(), nullptr) &&
                         loaderClose(loader, nullptr);
    GdkPixbuf* decoded = written ? loaderPixbuf(loader) : nullptr;
    // The picture is kept at the size it was made, so it isn't stretched; the copy is owned here, and the loader's picture is released.
    GdkPixbuf* scaled = decoded ? scale(decoded, width(decoded), height(decoded), GDK_INTERP_BILINEAR) : nullptr;
    unref(loader);
    if (scaled) {
        if (applicationIcon) {
            unref(applicationIcon);
        }
        applicationIcon = scaled;
    }
}

void runEventLoop() {
    ensureGtk();
    api().main_loop();
}

void stopEventLoop() {
    api().main_quit();
}

namespace {

gboolean runQueuedTask(gpointer data) {
    auto* task = static_cast<std::function<void()>*>(data);
    (*task)();
    delete task;
    return FALSE;
}

}  // namespace

void runOnUiThread(std::function<void()> task) {
    api().idle_add(runQueuedTask, new std::function<void()>(std::move(task)));
}

}  // namespace gwl
