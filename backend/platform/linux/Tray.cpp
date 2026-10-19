// Linux: the tray icon is a StatusNotifierItem on the session bus, registered with the desktop's watcher. GTK, GIO and
// gdk-pixbuf are opened at runtime, as the rest of the Linux layer opens GTK (see TrayBus.h). The item has no D-Bus menu: a right click
// asks for the launcher's own menu, a GTK menu with Open and Quit.

#include "tray/Tray.h"
#include "TrayBus.h"

#include <dlfcn.h>
#include <unistd.h>

#include <cstddef>
#include <stdexcept>
#include <string>
#include <vector>

#include "Settings.h"

namespace gwl {

namespace {

const char* const kLibrary = "libgtk-3.so.0";
const char* const kWatcher = "org.kde.StatusNotifierWatcher";
const char* const kItemPath = "/StatusNotifierItem";
const char* const kItemInterface = "org.kde.StatusNotifierItem";
const char* const kPropertiesInterface = "org.freedesktop.DBus.Properties";
const gint kIconSize = 22;

// The item's interfaces. GIO checks each call against them; the property values are answered by the handler.
const char* const kIntrospection = R"XML(<node>
  <interface name="org.kde.StatusNotifierItem">
    <method name="Activate"><arg name="x" type="i" direction="in"/><arg name="y" type="i" direction="in"/></method>
    <method name="SecondaryActivate"><arg name="x" type="i" direction="in"/><arg name="y" type="i" direction="in"/></method>
    <method name="ContextMenu"><arg name="x" type="i" direction="in"/><arg name="y" type="i" direction="in"/></method>
    <method name="Scroll"><arg name="delta" type="i" direction="in"/><arg name="orientation" type="s" direction="in"/></method>
    <property name="Category" type="s" access="read"/>
    <property name="Id" type="s" access="read"/>
    <property name="Title" type="s" access="read"/>
    <property name="Status" type="s" access="read"/>
    <property name="WindowId" type="u" access="read"/>
    <property name="IconThemePath" type="s" access="read"/>
    <property name="Menu" type="o" access="read"/>
    <property name="ItemIsMenu" type="b" access="read"/>
    <property name="IconPixmap" type="a(iiay)" access="read"/>
  </interface>
  <interface name="org.freedesktop.DBus.Properties">
    <method name="Get"><arg name="interface" type="s" direction="in"/><arg name="name" type="s" direction="in"/><arg name="value" type="v" direction="out"/></method>
    <method name="GetAll"><arg name="interface" type="s" direction="in"/><arg name="properties" type="a{sv}" direction="out"/></method>
  </interface>
</node>)XML";

const char* const kProperties[] = {"Category", "Id", "Title", "Status", "WindowId", "IconThemePath", "Menu", "ItemIsMenu", "IconPixmap"};

template <typename F>
F symbolOf(void* library, const char* name) {
    return reinterpret_cast<F>(dlsym(library, name));
}

}  // namespace

// The loaded functions, or null when any is missing: the tray needs all of them.
const TrayApi* trayApi() {
    static const TrayApi loaded = [] {
        TrayApi api;
        api.library = dlopen(kLibrary, RTLD_LAZY | RTLD_LOCAL);
        if (!api.library) {
            return api;
        }
        void* lib = api.library;
        api.busGet = symbolOf<decltype(api.busGet)>(lib, "g_bus_get_sync");
        api.callSync = symbolOf<decltype(api.callSync)>(lib, "g_dbus_connection_call_sync");
        api.newVariant = symbolOf<decltype(api.newVariant)>(lib, "g_variant_new");
        api.getVariant = symbolOf<decltype(api.getVariant)>(lib, "g_variant_get");
        api.fixedArray = symbolOf<decltype(api.fixedArray)>(lib, "g_variant_new_fixed_array");
        api.newArray = symbolOf<decltype(api.newArray)>(lib, "g_variant_new_array");
        api.variantUnref = symbolOf<decltype(api.variantUnref)>(lib, "g_variant_unref");
        api.builderNew = symbolOf<decltype(api.builderNew)>(lib, "g_variant_builder_new");
        api.builderAdd = symbolOf<decltype(api.builderAdd)>(lib, "g_variant_builder_add");
        api.builderEnd = symbolOf<decltype(api.builderEnd)>(lib, "g_variant_builder_end");
        api.builderUnref = symbolOf<decltype(api.builderUnref)>(lib, "g_variant_builder_unref");
        api.nodeInfoForXml = symbolOf<decltype(api.nodeInfoForXml)>(lib, "g_dbus_node_info_new_for_xml");
        api.lookupInterface = symbolOf<decltype(api.lookupInterface)>(lib, "g_dbus_node_info_lookup_interface");
        api.nodeInfoUnref = symbolOf<decltype(api.nodeInfoUnref)>(lib, "g_dbus_node_info_unref");
        api.registerObject = symbolOf<decltype(api.registerObject)>(lib, "g_dbus_connection_register_object");
        api.unregisterObject = symbolOf<decltype(api.unregisterObject)>(lib, "g_dbus_connection_unregister_object");
        api.returnValue = symbolOf<decltype(api.returnValue)>(lib, "g_dbus_method_invocation_return_value");
        api.returnError = symbolOf<decltype(api.returnError)>(lib, "g_dbus_method_invocation_return_dbus_error");
        api.errorFree = symbolOf<decltype(api.errorFree)>(lib, "g_error_free");
        api.objectUnref = symbolOf<decltype(api.objectUnref)>(lib, "g_object_unref");
        api.connectionNew = symbolOf<decltype(api.connectionNew)>(lib, "g_dbus_connection_new_for_address_sync");
        api.childCount = symbolOf<decltype(api.childCount)>(lib, "g_variant_n_children");
        api.childValue = symbolOf<decltype(api.childValue)>(lib, "g_variant_get_child_value");
        api.loaderNew = symbolOf<decltype(api.loaderNew)>(lib, "gdk_pixbuf_loader_new");
        api.loaderWrite = symbolOf<decltype(api.loaderWrite)>(lib, "gdk_pixbuf_loader_write");
        api.loaderClose = symbolOf<decltype(api.loaderClose)>(lib, "gdk_pixbuf_loader_close");
        api.loaderPixbuf = symbolOf<decltype(api.loaderPixbuf)>(lib, "gdk_pixbuf_loader_get_pixbuf");
        api.scale = symbolOf<decltype(api.scale)>(lib, "gdk_pixbuf_scale_simple");
        api.pixels = symbolOf<decltype(api.pixels)>(lib, "gdk_pixbuf_get_pixels");
        api.rowstride = symbolOf<decltype(api.rowstride)>(lib, "gdk_pixbuf_get_rowstride");
        api.channels = symbolOf<decltype(api.channels)>(lib, "gdk_pixbuf_get_n_channels");
        api.width = symbolOf<decltype(api.width)>(lib, "gdk_pixbuf_get_width");
        api.height = symbolOf<decltype(api.height)>(lib, "gdk_pixbuf_get_height");
        api.menuNew = symbolOf<decltype(api.menuNew)>(lib, "gtk_menu_new");
        api.menuItemNew = symbolOf<decltype(api.menuItemNew)>(lib, "gtk_menu_item_new_with_label");
        api.menuShellAppend = symbolOf<decltype(api.menuShellAppend)>(lib, "gtk_menu_shell_append");
        api.showAll = symbolOf<decltype(api.showAll)>(lib, "gtk_widget_show_all");
        api.menuPopup = symbolOf<decltype(api.menuPopup)>(lib, "gtk_menu_popup");
        api.currentEventTime = symbolOf<decltype(api.currentEventTime)>(lib, "gtk_get_current_event_time");
        api.connect = symbolOf<decltype(api.connect)>(lib, "g_signal_connect_data");
        api.destroy = symbolOf<decltype(api.destroy)>(lib, "gtk_widget_destroy");
        const bool complete = api.busGet && api.callSync && api.newVariant && api.getVariant && api.fixedArray && api.newArray &&
                              api.variantUnref && api.builderNew && api.builderAdd && api.builderEnd && api.builderUnref &&
                              api.nodeInfoForXml && api.lookupInterface && api.nodeInfoUnref && api.registerObject &&
                              api.unregisterObject && api.returnValue && api.returnError && api.errorFree && api.objectUnref &&
                              api.loaderNew && api.loaderWrite && api.loaderClose && api.loaderPixbuf && api.scale && api.pixels &&
                              api.rowstride && api.channels && api.width && api.height && api.menuNew && api.menuItemNew &&
                              api.menuShellAppend && api.showAll && api.menuPopup && api.currentEventTime && api.connect &&
                              api.destroy && api.connectionNew && api.childCount && api.childValue;
        if (!complete) {
            api.library = nullptr;
        }
        return api;
    }();
    return loaded.library ? &loaded : nullptr;
}

namespace {

}  // namespace

struct TrayItem {
    TrayHandler onEvent;
    // The icon as ARGB in network byte order, the layout StatusNotifierItem pixmaps use.
    std::vector<unsigned char> icon;
    int iconWidth = 0;
    int iconHeight = 0;
    GDBusConnection* connection = nullptr;
    // The item's bus name. The watcher drops the item when the name is released, so the name is kept until release.
    std::string busName;
    GDBusNodeInfo* node = nullptr;
    guint objects[2] = {0, 0};
    GtkWidget* menu = nullptr;

    TrayItem(const std::string& iconPng, TrayHandler handler);
    ~TrayItem();
    // Unregisters what the item registered, so it can be released after a failed start as well.
    void release();
};

namespace {

// Reads the PNG into the icon at the tray's size, ARGB in network byte order. False when the PNG can't be read.
bool decodeIcon(const TrayApi& api, const std::string& png, TrayItem& item) {
    GdkPixbufLoader* loader = api.loaderNew();
    if (!loader) {
        return false;
    }
    const bool written = api.loaderWrite(loader, reinterpret_cast<const guchar*>(png.data()), png.size(), nullptr) &&
                         api.loaderClose(loader, nullptr);
    // The loader owns the decoded picture, so it's scaled into a copy the tray owns.
    GdkPixbuf* decoded = written ? api.loaderPixbuf(loader) : nullptr;
    GdkPixbuf* scaled = decoded ? api.scale(decoded, kIconSize, kIconSize, GDK_INTERP_BILINEAR) : nullptr;
    bool ok = false;
    if (scaled && (api.channels(scaled) == 3 || api.channels(scaled) == 4)) {
        const int width = api.width(scaled);
        const int height = api.height(scaled);
        const int channels = api.channels(scaled);
        const int stride = api.rowstride(scaled);
        const guchar* data = api.pixels(scaled);
        item.iconWidth = width;
        item.iconHeight = height;
        item.icon.assign(static_cast<std::size_t>(width * height * 4), 0);
        for (int y = 0; y < height; ++y) {
            for (int x = 0; x < width; ++x) {
                const guchar* pixel = data + y * stride + x * channels;
                unsigned char* out = &item.icon[static_cast<std::size_t>((y * width + x) * 4)];
                out[0] = channels == 4 ? pixel[3] : 255;
                out[1] = pixel[0];
                out[2] = pixel[1];
                out[3] = pixel[2];
            }
        }
        ok = true;
    }
    if (scaled) {
        api.objectUnref(scaled);
    }
    api.objectUnref(loader);
    return ok;
}

// The IconPixmap property: one icon, as an array of (width, height, pixels).
GVariant* iconPixmap(const TrayApi& api, const TrayItem& item) {
    GVariant* pixels = api.fixedArray(reinterpret_cast<const GVariantType*>("y"), item.icon.data(), item.icon.size(), 1);
    GVariant* icon = api.newVariant("(ii@ay)", item.iconWidth, item.iconHeight, pixels);
    return api.newArray(reinterpret_cast<const GVariantType*>("(iiay)"), &icon, 1);
}

// A property's value, or null when the item has no property of that name.
GVariant* propertyValue(const TrayApi& api, const TrayItem& item, const std::string& name) {
    if (name == "Category") {
        return api.newVariant("s", "ApplicationStatus");
    }
    if (name == "Id") {
        return api.newVariant("s", "gwlauncher");
    }
    if (name == "Title") {
        return api.newVariant("s", "GW Launcher");
    }
    if (name == "Status") {
        return api.newVariant("s", "Active");
    }
    if (name == "WindowId") {
        return api.newVariant("u", 0u);
    }
    if (name == "IconThemePath") {
        return api.newVariant("s", "");
    }
    if (name == "Menu") {
        return api.newVariant("o", "/NO_DBUSMENU");
    }
    if (name == "ItemIsMenu") {
        return api.newVariant("b", FALSE);
    }
    if (name == "IconPixmap") {
        return iconPixmap(api, item);
    }
    return nullptr;
}

// The launcher's own menu for the tray: Open and Quit.
GtkWidget* buildMenu(const TrayApi& api, TrayItem& item) {
    GtkWidget* menu = api.menuNew();
    const struct {
        const char* label;
        GCallback picked;
    } entries[] = {
        {"Open GW Launcher", reinterpret_cast<GCallback>(+[](GtkMenuItem*, gpointer data) {
             static_cast<TrayItem*>(data)->onEvent(TrayAction::Open, 0, 0);
         })},
        {"Quit", reinterpret_cast<GCallback>(+[](GtkMenuItem*, gpointer data) {
             static_cast<TrayItem*>(data)->onEvent(TrayAction::Quit, 0, 0);
         })},
    };
    for (const auto& entry : entries) {
        GtkWidget* widget = api.menuItemNew(entry.label);
        api.connect(widget, "activate", entry.picked, &item, nullptr, static_cast<GConnectFlags>(0));
        api.menuShellAppend(reinterpret_cast<GtkMenuShell*>(menu), widget);
    }
    api.showAll(menu);
    return menu;
}

void onMethodCall(GDBusConnection*, const gchar*, const gchar*, const gchar* interface, const gchar* method, GVariant* parameters,
                  GDBusMethodInvocation* invocation, gpointer data) {
    const TrayApi& api = *trayApi();
    TrayItem& item = *static_cast<TrayItem*>(data);
    const std::string iface = interface;
    const std::string name = method;

    if (iface == kItemInterface) {
        if (name == "Activate" || name == "SecondaryActivate" || name == "ContextMenu") {
            gint32 x = 0;
            gint32 y = 0;
            api.getVariant(parameters, "(ii)", &x, &y);
            if (name == "Activate") {
                item.onEvent(TrayAction::Activate, x, y);
            } else if (name == "ContextMenu") {
                api.menuPopup(reinterpret_cast<GtkMenu*>(item.menu), nullptr, nullptr, nullptr, nullptr, 0, api.currentEventTime());
            }
            api.returnValue(invocation, nullptr);
            return;
        }
        if (name == "Scroll") {
            api.returnValue(invocation, nullptr);
            return;
        }
    } else if (iface == kPropertiesInterface) {
        if (name == "Get") {
            const gchar* owner = nullptr;
            const gchar* property = nullptr;
            api.getVariant(parameters, "(&s&s)", &owner, &property);
            if (GVariant* value = propertyValue(api, item, property)) {
                api.returnValue(invocation, api.newVariant("(v)", value));
            } else {
                api.returnError(invocation, "org.freedesktop.DBus.Error.UnknownProperty", "No such property.");
            }
            return;
        }
        if (name == "GetAll") {
            GVariantBuilder* builder = api.builderNew(reinterpret_cast<const GVariantType*>("a{sv}"));
            for (const char* property : kProperties) {
                if (GVariant* value = propertyValue(api, item, property)) {
                    api.builderAdd(builder, "{sv}", property, value);
                }
            }
            GVariant* all = api.builderEnd(builder);
            api.builderUnref(builder);
            api.returnValue(invocation, api.newVariant("(@a{sv})", all));
            return;
        }
    }
    api.returnError(invocation, "org.freedesktop.DBus.Error.UnknownMethod", "Unknown method.");
}

}  // namespace

TrayItem::TrayItem(const std::string& iconPng, TrayHandler handler) : onEvent(std::move(handler)) {
    const TrayApi* api = trayApi();
    if (!api) {
        throw std::runtime_error("GTK isn't installed, so the tray icon can't be shown.");
    }
    if (!decodeIcon(*api, iconPng, *this)) {
        throw std::runtime_error("The tray icon couldn't be read.");
    }
    try {
        GError* error = nullptr;
        connection = api->busGet(G_BUS_TYPE_SESSION, nullptr, &error);
        if (!connection) {
            fail(*api, error, "The session bus isn't running.");
        }

        // The item's name, which the watcher is told about: unique to this process, and owned by it.
        busName = "org.kde.StatusNotifierItem-" + std::to_string(getpid()) + "-1";
        GVariant* requested = api->callSync(connection, "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus",
                                            "RequestName", api->newVariant("(su)", busName.c_str(), 0u), reinterpret_cast<const GVariantType*>("(u)"),
                                            G_DBUS_CALL_FLAGS_NONE, -1, nullptr, &error);
        if (!requested) {
            fail(*api, error, "The tray name couldn't be requested.");
        }
        guint32 owner = 0;
        api->getVariant(requested, "(u)", &owner);
        api->variantUnref(requested);
        if (owner != 1) {
            throw std::runtime_error("Another tray item already has this launcher's name.");
        }

        node = api->nodeInfoForXml(kIntrospection, &error);
        if (!node) {
            fail(*api, error, "The tray's interfaces couldn't be described.");
        }
        GDBusInterfaceInfo* itemInfo = api->lookupInterface(node, kItemInterface);
        GDBusInterfaceInfo* propertiesInfo = api->lookupInterface(node, kPropertiesInterface);
        if (!itemInfo || !propertiesInfo) {
            throw std::runtime_error("The tray's interfaces are missing.");
        }
        GDBusInterfaceVTable vtable{};
        vtable.method_call = onMethodCall;
        objects[0] = api->registerObject(connection, kItemPath, itemInfo, &vtable, this, nullptr, &error);
        if (!objects[0]) {
            fail(*api, error, "The tray item couldn't be published.");
        }
        objects[1] = api->registerObject(connection, kItemPath, propertiesInfo, &vtable, this, nullptr, &error);
        if (!objects[1]) {
            fail(*api, error, "The tray item couldn't be published.");
        }

        menu = buildMenu(*api, *this);

        GVariant* registered = api->callSync(connection, kWatcher, "/StatusNotifierWatcher", kWatcher, "RegisterStatusNotifierItem",
                                             api->newVariant("(s)", busName.c_str()), nullptr, G_DBUS_CALL_FLAGS_NONE, -1, nullptr,
                                             &error);
        if (!registered) {
            fail(*api, error, "The desktop has no system tray to show the icon in.");
        }
        api->variantUnref(registered);
    } catch (...) {
        release();
        throw;
    }
}

TrayItem::~TrayItem() {
    release();
}

void TrayItem::release() {
    const TrayApi* api = trayApi();
    if (!api) {
        return;
    }
    for (guint& object : objects) {
        if (object != 0 && connection) {
            api->unregisterObject(connection, object);
        }
        object = 0;
    }
    if (menu) {
        api->destroy(menu);
        menu = nullptr;
    }
    if (node) {
        api->nodeInfoUnref(node);
        node = nullptr;
    }
    // Releasing the name is what takes the item off the tray: the watcher removes an item whose name has gone.
    if (connection && !busName.empty()) {
        GError* error = nullptr;
        if (GVariant* released = api->callSync(connection, "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus",
                                               "ReleaseName", api->newVariant("(s)", busName.c_str()),
                                               reinterpret_cast<const GVariantType*>("(u)"), G_DBUS_CALL_FLAGS_NONE, -1, nullptr, &error)) {
            api->variantUnref(released);
        } else if (error) {
            api->errorFree(error);
        }
        busName.clear();
    }
}

TrayIcon::TrayIcon(const std::string& iconPng, TrayHandler onEvent)
    : item_(std::make_unique<TrayItem>(iconPng, std::move(onEvent))) {}

TrayIcon::~TrayIcon() = default;

// The StatusNotifierItem doesn't report where it's drawn, so the list is placed by the tray's region instead.
std::optional<Rect> TrayIcon::rect() const {
    return std::nullopt;
}

// The desktop's watcher has the launcher's item, so the tray is there: the watcher owns its name on the session bus.
bool hasTray() {
    const TrayApi* api = trayApi();
    if (!api) {
        return false;
    }
    GError* error = nullptr;
    GDBusConnection* connection = api->busGet(G_BUS_TYPE_SESSION, nullptr, &error);
    if (!connection) {
        if (error) {
            api->errorFree(error);
        }
        return false;
    }
    GVariant* reply = api->callSync(connection, "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "NameHasOwner",
                                    api->newVariant("(s)", kWatcher), reinterpret_cast<const GVariantType*>("(b)"), G_DBUS_CALL_FLAGS_NONE, -1, nullptr,
                                    &error);
    if (!reply) {
        if (error) {
            api->errorFree(error);
        }
        return false;
    }
    gboolean owned = FALSE;
    api->getVariant(reply, "(b)", &owned);
    api->variantUnref(reply);
    return owned != FALSE;
}

}  // namespace gwl
