// Linux: where the desktop's tray is on the screen, so the list opens beside it. The accessibility tree (AT-SPI) gives the
// panel's icon widgets, and the X11 tray selection gives a legacy tray's window. A StatusNotifierItem reports no position,
// so the desktop is asked instead. Each lookup gives nothing when the desktop doesn't provide it.

#include <dlfcn.h>

#include <cstddef>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "tray/Tray.h"
#include "TrayBus.h"
#include "tray/TrayRegion.h"

namespace gwl {

namespace {

// Panel application names in the accessibility tree. Only Cinnamon's panel has been checked.
const char* const kPanelApps[] = {"cinnamon"};
const char* const kAccessible = "org.a11y.atspi.Accessible";
const char* const kComponent = "org.a11y.atspi.Component";
const char* const kRegistry = "org.a11y.atspi.Registry";
const char* const kRoot = "/org/a11y/atspi/accessible/root";
const int kMaxDepth = 12;
// How far the panel's band reaches into the screen from the panel's edge.
const int kPanelBand = 100;
// The most a call to another application may take before the lookup gives up on it.
const int kCallTimeout = 2000;

// The accessibility bus, from the session bus's address for it, as a connection of the lookup's own. Null when the bus
// isn't running. The caller releases it.
GDBusConnection* accessibilityBus(const TrayApi& api) {
    GError* error = nullptr;
    GDBusConnection* session = api.busGet(G_BUS_TYPE_SESSION, nullptr, &error);
    if (!session) {
        if (error) {
            api.errorFree(error);
        }
        return nullptr;
    }
    GVariant* reply = api.callSync(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus", "GetAddress", nullptr,
                                   reinterpret_cast<const GVariantType*>("(s)"), G_DBUS_CALL_FLAGS_NONE, kCallTimeout, nullptr, &error);
    if (!reply) {
        if (error) {
            api.errorFree(error);
        }
        return nullptr;
    }
    const gchar* address = nullptr;
    api.getVariant(reply, "(&s)", &address);
    const std::string text = address ? address : "";
    api.variantUnref(reply);
    if (text.empty()) {
        return nullptr;
    }
    // The accessibility bus is a bus daemon of its own, so this connection says hello to it, as a bus client does.
    GDBusConnection* bus = api.connectionNew(text.c_str(),
                                            static_cast<GDBusConnectionFlags>(G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_CLIENT | G_DBUS_CONNECTION_FLAGS_MESSAGE_BUS_CONNECTION),
                                            nullptr, nullptr, &error);
    if (!bus && error) {
        api.errorFree(error);
    }
    return bus;
}

// A call to an accessible object on the bus. The reply is null when the call fails; the caller releases it. The parameters
// are taken over by the call.
GVariant* callOn(const TrayApi& api, GDBusConnection* bus, const std::string& name, const std::string& path, const char* interface,
                 const char* method, GVariant* parameters, const char* replyType) {
    GError* error = nullptr;
    GVariant* reply = api.callSync(bus, name.c_str(), path.c_str(), interface, method, parameters,
                                   reinterpret_cast<const GVariantType*>(replyType), G_DBUS_CALL_FLAGS_NONE, kCallTimeout, nullptr,
                                   &error);
    if (!reply && error) {
        api.errorFree(error);
    }
    return reply;
}

// The children of an accessible object, as (bus name, object path).
std::vector<std::pair<std::string, std::string>> childrenOf(const TrayApi& api, GDBusConnection* bus, const std::string& name,
                                                             const std::string& path) {
    std::vector<std::pair<std::string, std::string>> children;
    GVariant* reply = callOn(api, bus, name, path, kAccessible, "GetChildren", nullptr, "(a(so))");
    if (!reply) {
        return children;
    }
    GVariant* list = api.childValue(reply, 0);
    const gsize count = api.childCount(list);
    for (gsize i = 0; i < count; ++i) {
        GVariant* child = api.childValue(list, i);
        const gchar* childBus = nullptr;
        const gchar* childPath = nullptr;
        api.getVariant(child, "(&s&o)", &childBus, &childPath);
        children.emplace_back(childBus, childPath);
        api.variantUnref(child);
    }
    api.variantUnref(list);
    api.variantUnref(reply);
    return children;
}

// The accessible name of an object, which is how the panel's application is found.
std::string nameOf(const TrayApi& api, GDBusConnection* bus, const std::string& name, const std::string& path) {
    GVariant* reply = callOn(api, bus, name, path, "org.freedesktop.DBus.Properties", "Get",
                             api.newVariant("(ss)", kAccessible, "Name"), "(v)");
    if (!reply) {
        return {};
    }
    GVariant* value = nullptr;
    api.getVariant(reply, "(v)", &value);
    const gchar* text = nullptr;
    if (value) {
        api.getVariant(value, "&s", &text);
    }
    const std::string result = text ? text : "";
    if (value) {
        api.variantUnref(value);
    }
    api.variantUnref(reply);
    return result;
}

// The role of an object, such as a label or an icon.
std::string roleOf(const TrayApi& api, GDBusConnection* bus, const std::string& name, const std::string& path) {
    GVariant* reply = callOn(api, bus, name, path, kAccessible, "GetRoleName", nullptr, "(s)");
    if (!reply) {
        return {};
    }
    const gchar* role = nullptr;
    api.getVariant(reply, "(&s)", &role);
    const std::string result = role ? role : "";
    api.variantUnref(reply);
    return result;
}

// The screen extents of an object, when it has a size.
std::optional<Rect> extentsOf(const TrayApi& api, GDBusConnection* bus, const std::string& name, const std::string& path) {
    // GetExtents answers with its rectangle inside a struct.
    GVariant* reply = callOn(api, bus, name, path, kComponent, "GetExtents", api.newVariant("(u)", 0u), "((iiii))");
    if (!reply) {
        return std::nullopt;
    }
    gint32 x = 0;
    gint32 y = 0;
    gint32 width = 0;
    gint32 height = 0;
    api.getVariant(reply, "((iiii))", &x, &y, &width, &height);
    api.variantUnref(reply);
    if (width <= 0 || height <= 0) {
        return std::nullopt;
    }
    return Rect{x, y, width, height};
}

// The panel's band: the panel strip, and the widgets' reach a little way into the screen from it. Icons on the panel's edge
// can sit just outside the strip, so the band takes them in.
Rect panelBand(const Rect& strip, const Rect& screen) {
    Rect band = strip;
    if (strip.isVertical()) {
        if (strip.x == screen.x) {
            band.width += kPanelBand;
        } else {
            band.x -= kPanelBand;
            band.width += kPanelBand;
        }
    } else if (strip.y == screen.y) {
        band.height += kPanelBand;
    } else {
        band.y -= kPanelBand;
        band.height += kPanelBand;
    }
    return band;
}

// Adds the widgets on the panel's band, at the tray's end of it, with their roles. The tray sits at the far end of the panel:
// the right on a horizontal panel, the bottom on a vertical one. Walks the panel's whole tree.
void collectPanelWidgets(const TrayApi& api, GDBusConnection* bus, const std::string& name, const std::string& path, const Rect& band,
                         const Rect& strip, int depth, std::vector<TrayWidget>& out) {
    if (depth > kMaxDepth) {
        return;
    }
    for (const auto& [childBus, childPath] : childrenOf(api, bus, name, path)) {
        if (const auto rect = extentsOf(api, bus, childBus, childPath)) {
            const bool onTrayEnd = strip.isVertical() ? rect->centreY() >= strip.centreY() : rect->centreX() >= strip.centreX();
            if (band.overlaps(*rect) && onTrayEnd) {
                out.push_back({*rect, roleOf(api, bus, childBus, childPath)});
            }
        }
        collectPanelWidgets(api, bus, childBus, childPath, band, strip, depth + 1, out);
    }
}

std::optional<Rect> atspiTrayRegion(const TrayApi& api, const Rect& strip, const Rect& screen) {
    GDBusConnection* bus = accessibilityBus(api);
    if (!bus) {
        return std::nullopt;
    }
    std::vector<TrayWidget> widgets;
    for (const auto& [appBus, appPath] : childrenOf(api, bus, kRegistry, kRoot)) {
        const std::string appName = nameOf(api, bus, appBus, appPath);
        for (const char* panel : kPanelApps) {
            if (appName == panel) {
                collectPanelWidgets(api, bus, appBus, appPath, panelBand(strip, screen), strip, 0, widgets);
            }
        }
    }
    api.objectUnref(bus);
    return trayFromWidgets(widgets, strip.isVertical());
}

// The X11 functions the legacy tray lookup uses, opened from libX11. Xlib's own types are stood in for by these.
struct XDisplay;
using XId = unsigned long;

struct XApi {
    void* library = nullptr;
    XDisplay* (*openDisplay)(const char*) = nullptr;
    int (*defaultScreen)(XDisplay*) = nullptr;
    XId (*internAtom)(XDisplay*, const char*, int) = nullptr;
    XId (*selectionOwner)(XDisplay*, XId) = nullptr;
    int (*getGeometry)(XDisplay*, XId, XId*, int*, int*, unsigned*, unsigned*, unsigned*, unsigned*) = nullptr;
    XId (*rootWindow)(XDisplay*, int) = nullptr;
    int (*translateCoordinates)(XDisplay*, XId, XId, int, int, int*, int*, XId*) = nullptr;
    int (*closeDisplay)(XDisplay*) = nullptr;
};

const XApi* xApi() {
    static const XApi loaded = [] {
        XApi api;
        api.library = dlopen("libX11.so.6", RTLD_LAZY | RTLD_LOCAL);
        if (!api.library) {
            return api;
        }
        void* lib = api.library;
        api.openDisplay = reinterpret_cast<decltype(api.openDisplay)>(dlsym(lib, "XOpenDisplay"));
        api.defaultScreen = reinterpret_cast<decltype(api.defaultScreen)>(dlsym(lib, "XDefaultScreen"));
        api.internAtom = reinterpret_cast<decltype(api.internAtom)>(dlsym(lib, "XInternAtom"));
        api.selectionOwner = reinterpret_cast<decltype(api.selectionOwner)>(dlsym(lib, "XGetSelectionOwner"));
        api.getGeometry = reinterpret_cast<decltype(api.getGeometry)>(dlsym(lib, "XGetGeometry"));
        api.rootWindow = reinterpret_cast<decltype(api.rootWindow)>(dlsym(lib, "XRootWindow"));
        api.translateCoordinates = reinterpret_cast<decltype(api.translateCoordinates)>(dlsym(lib, "XTranslateCoordinates"));
        api.closeDisplay = reinterpret_cast<decltype(api.closeDisplay)>(dlsym(lib, "XCloseDisplay"));
        if (!api.openDisplay || !api.defaultScreen || !api.internAtom || !api.selectionOwner || !api.getGeometry ||
            !api.rootWindow || !api.translateCoordinates || !api.closeDisplay) {
            api.library = nullptr;
        }
        return api;
    }();
    return loaded.library ? &loaded : nullptr;
}

// The window of a legacy system tray, when one owns the tray selection, at its place on the screen.
std::optional<Rect> x11TrayRegion() {
    const XApi* x = xApi();
    if (!x) {
        return std::nullopt;
    }
    XDisplay* display = x->openDisplay(nullptr);
    if (!display) {
        return std::nullopt;
    }
    std::optional<Rect> region;
    const int screen = x->defaultScreen(display);
    const std::string selection = "_NET_SYSTEM_TRAY_S" + std::to_string(screen);
    const XId atom = x->internAtom(display, selection.c_str(), 1);
    const XId owner = atom ? x->selectionOwner(display, atom) : 0;
    if (owner) {
        XId root = 0;
        int left = 0;
        int top = 0;
        unsigned width = 0;
        unsigned height = 0;
        unsigned border = 0;
        unsigned depth = 0;
        if (x->getGeometry(display, owner, &root, &left, &top, &width, &height, &border, &depth)) {
            int screenX = 0;
            int screenY = 0;
            XId child = 0;
            x->translateCoordinates(display, owner, x->rootWindow(display, screen), 0, 0, &screenX, &screenY, &child);
            region = Rect{screenX, screenY, static_cast<int>(width), static_cast<int>(height)};
        }
    }
    x->closeDisplay(display);
    return region;
}

}  // namespace

std::optional<Rect> locateTray(const Rect& screen, const Rect& workArea) {
    // The panel is the strip the work area leaves out, on whichever edge it's on. Without one there's no panel to search.
    const auto strip = panelStrip(screen, workArea);
    if (!strip) {
        return std::nullopt;
    }
    std::optional<Rect> region;
    if (const TrayApi* api = trayApi()) {
        region = atspiTrayRegion(*api, *strip, screen);
    }
    if (!region) {
        region = x11TrayRegion();
    }
    // A legacy tray's selection can belong to a helper window parked off the screen, which says nothing about the icon row.
    if (region && !screen.contains(*region)) {
        return std::nullopt;
    }
    return region;
}

}  // namespace gwl
