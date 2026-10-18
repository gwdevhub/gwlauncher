// Prints the sizes, offsets and constant values that the launcher relies on. Built once against the upstream GTK, GLib, GIO and WebKitGTK
// headers and once against this folder's stubs, the two outputs must match:
//   cc -I<upstream include paths> abi_probe.c -o probe-upstream && ./probe-upstream > upstream.txt
//   cc -Iinclude abi_probe.c -o probe-stub && ./probe-stub > stub.txt && diff upstream.txt stub.txt
#include <stddef.h>
#include <stdio.h>
#include <gtk/gtk.h>
#include <webkit2/webkit2.h>

#define SIZE(t) printf("sizeof(" #t ")=%zu\n", sizeof(t))
#define OFFSET(t, m) printf("offsetof(" #t ", " #m ")=%zu\n", offsetof(t, m))
#define VALUE(x) printf(#x "=%lld\n", (long long)(x))

int main(void) {
    SIZE(GVariantBuilder);
    SIZE(GDBusInterfaceVTable);
    OFFSET(GDBusInterfaceVTable, method_call);
    OFFSET(GDBusInterfaceVTable, get_property);
    OFFSET(GDBusInterfaceVTable, set_property);
    OFFSET(GDBusInterfaceVTable, padding);
    SIZE(GdkRectangle);
    OFFSET(GdkRectangle, x);
    OFFSET(GdkRectangle, height);
    SIZE(GError);
    OFFSET(GError, message);
    SIZE(GdkModifierType);
    SIZE(GtkWindowType);
    SIZE(GtkResponseType);
    VALUE(GTK_WINDOW_TOPLEVEL);
    VALUE(GTK_WINDOW_POPUP);
    VALUE(GTK_WIN_POS_NONE);
    VALUE(GTK_WIN_POS_CENTER);
    VALUE(GTK_MESSAGE_INFO);
    VALUE(GTK_MESSAGE_ERROR);
    VALUE(GTK_BUTTONS_NONE);
    VALUE(GTK_BUTTONS_OK);
    VALUE(GTK_BUTTONS_CLOSE);
    VALUE(GTK_DIALOG_MODAL);
    VALUE(GTK_DIALOG_DESTROY_WITH_PARENT);
    VALUE(GTK_RESPONSE_REJECT);
    VALUE(GTK_RESPONSE_ACCEPT);
    VALUE(GTK_RESPONSE_CANCEL);
    VALUE(GTK_RESPONSE_OK);
    VALUE(GTK_FILE_CHOOSER_ACTION_OPEN);
    VALUE(GTK_FILE_CHOOSER_ACTION_SELECT_FOLDER);
    VALUE(GDK_INTERP_NEAREST);
    VALUE(GDK_INTERP_BILINEAR);
    VALUE(GDK_CURRENT_TIME);
    VALUE(G_BUS_TYPE_NONE);
    VALUE(G_BUS_TYPE_SYSTEM);
    VALUE(G_BUS_TYPE_SESSION);
    VALUE(G_DBUS_CALL_FLAGS_NONE);
    VALUE(G_DBUS_CONNECTION_FLAGS_NONE);
    VALUE(G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_CLIENT);
    VALUE(G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_SERVER);
    VALUE(G_DBUS_CONNECTION_FLAGS_AUTHENTICATION_ALLOW_ANONYMOUS);
    VALUE(G_DBUS_CONNECTION_FLAGS_MESSAGE_BUS_CONNECTION);
    VALUE(G_DBUS_CONNECTION_FLAGS_DELAY_MESSAGE_PROCESSING);
    VALUE(G_CONNECT_AFTER);
    VALUE(G_CONNECT_SWAPPED);
    return 0;
}
