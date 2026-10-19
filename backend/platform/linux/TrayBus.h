// The GIO, GLib and GTK functions the Linux tray and its position lookup use. They're opened at runtime, from libgtk-3, which
// brings the rest with it: see trayApi.

#pragma once

// GLib is opened at runtime, so its variant type macros are written out as casts instead of calls into it.
#include <gdk-pixbuf/gdk-pixbuf.h>
#include <gio/gio.h>
#include <gtk/gtk.h>

#include <string>

namespace gwl {

// The functions the tray uses, opened from GTK, which brings GIO, GLib and gdk-pixbuf with it. Null when GTK isn't installed.
struct TrayApi {
    void* library = nullptr;

    GDBusConnection* (*busGet)(GBusType, GCancellable*, GError**) = nullptr;
    GVariant* (*callSync)(GDBusConnection*, const gchar*, const gchar*, const gchar*, const gchar*, GVariant*, const GVariantType*,
                          GDBusCallFlags, gint, GCancellable*, GError**) = nullptr;
    GVariant* (*newVariant)(const gchar*, ...) = nullptr;
    void (*getVariant)(GVariant*, const gchar*, ...) = nullptr;
    GVariant* (*fixedArray)(const GVariantType*, gconstpointer, gsize, gsize) = nullptr;
    GVariant* (*newArray)(const GVariantType*, GVariant* const*, gsize) = nullptr;
    void (*variantUnref)(GVariant*) = nullptr;
    GVariantBuilder* (*builderNew)(const GVariantType*) = nullptr;
    void (*builderAdd)(GVariantBuilder*, const gchar*, ...) = nullptr;
    GVariant* (*builderEnd)(GVariantBuilder*) = nullptr;
    void (*builderUnref)(GVariantBuilder*) = nullptr;
    GDBusNodeInfo* (*nodeInfoForXml)(const gchar*, GError**) = nullptr;
    GDBusInterfaceInfo* (*lookupInterface)(GDBusNodeInfo*, const gchar*) = nullptr;
    void (*nodeInfoUnref)(GDBusNodeInfo*) = nullptr;
    guint (*registerObject)(GDBusConnection*, const gchar*, GDBusInterfaceInfo*, const GDBusInterfaceVTable*, gpointer, GDestroyNotify,
                            GError**) = nullptr;
    gboolean (*unregisterObject)(GDBusConnection*, guint) = nullptr;
    void (*returnValue)(GDBusMethodInvocation*, GVariant*) = nullptr;
    void (*returnError)(GDBusMethodInvocation*, const gchar*, const gchar*) = nullptr;
    void (*errorFree)(GError*) = nullptr;
    void (*objectUnref)(gpointer) = nullptr;
    GDBusConnection* (*connectionNew)(const gchar*, GDBusConnectionFlags, GDBusAuthObserver*, GCancellable*, GError**) = nullptr;
    gsize (*childCount)(GVariant*) = nullptr;
    GVariant* (*childValue)(GVariant*, gsize) = nullptr;

    GdkPixbufLoader* (*loaderNew)() = nullptr;
    gboolean (*loaderWrite)(GdkPixbufLoader*, const guchar*, gsize, GError**) = nullptr;
    gboolean (*loaderClose)(GdkPixbufLoader*, GError**) = nullptr;
    GdkPixbuf* (*loaderPixbuf)(GdkPixbufLoader*) = nullptr;
    GdkPixbuf* (*scale)(const GdkPixbuf*, gint, gint, GdkInterpType) = nullptr;
    guchar* (*pixels)(const GdkPixbuf*) = nullptr;
    int (*rowstride)(const GdkPixbuf*) = nullptr;
    int (*channels)(const GdkPixbuf*) = nullptr;
    int (*width)(const GdkPixbuf*) = nullptr;
    int (*height)(const GdkPixbuf*) = nullptr;
};

// Throws with the error GIO reports, or the fallback text when it reports none.
[[noreturn]] inline void fail(const TrayApi& api, GError* error, const std::string& fallback) {
    std::string message = fallback;
    if (error) {
        message = error->message;
        api.errorFree(error);
    }
    throw std::runtime_error(message);
}

// The loaded functions, or null when GTK or any of them is missing.
const TrayApi* trayApi();

}  // namespace gwl
