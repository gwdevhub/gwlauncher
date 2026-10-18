// The GIO types the launcher's D-Bus code uses. Matches the upstream GIO headers' ABI. See ../README.md.
#pragma once

#include "glib.h"

typedef struct _GDBusConnection GDBusConnection;
typedef struct _GDBusNodeInfo GDBusNodeInfo;
typedef struct _GDBusInterfaceInfo GDBusInterfaceInfo;
typedef struct _GDBusMethodInvocation GDBusMethodInvocation;
typedef struct _GDBusAuthObserver GDBusAuthObserver;

typedef enum {
    G_BUS_TYPE_NONE = 0,
    G_BUS_TYPE_SYSTEM = 1,
    G_BUS_TYPE_SESSION = 2,
} GBusType;

typedef enum {
    G_DBUS_CALL_FLAGS_NONE = 0,
} GDBusCallFlags;

typedef enum {
    G_DBUS_CONNECTION_FLAGS_NONE = 0,
    G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_CLIENT = 1 << 0,
    G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_SERVER = 1 << 1,
    G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_ALLOW_ANONYMOUS = 1 << 2,
    G_DBUS_CONNECTION_FLAGS_MESSAGE_BUS_CONNECTION = 1 << 3,
    G_DBUS_CONNECTION_FLAGS_DELAY_MESSAGE_PROCESSING = 1 << 4,
} GDBusConnectionFlags;

typedef void (*GDBusInterfaceMethodCallFunc)(GDBusConnection* connection, const gchar* sender, const gchar* object_path,
                                             const gchar* interface_name, const gchar* method_name, GVariant* parameters,
                                             GDBusMethodInvocation* invocation, gpointer user_data);
typedef GVariant* (*GDBusInterfaceGetPropertyFunc)(GDBusConnection* connection, const gchar* sender, const gchar* object_path,
                                                   const gchar* interface_name, const gchar* property_name, GError** error,
                                                   gpointer user_data);
typedef gboolean (*GDBusInterfaceSetPropertyFunc)(GDBusConnection* connection, const gchar* sender, const gchar* object_path,
                                                  const gchar* interface_name, const gchar* property_name, GVariant* value,
                                                  GError** error, gpointer user_data);

typedef struct _GDBusInterfaceVTable GDBusInterfaceVTable;
struct _GDBusInterfaceVTable {
    GDBusInterfaceMethodCallFunc method_call;
    GDBusInterfaceGetPropertyFunc get_property;
    GDBusInterfaceSetPropertyFunc set_property;
    gpointer padding[8];
};
