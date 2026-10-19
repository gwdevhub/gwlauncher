// The GTK 3 and GDK types, enums and macros the launcher uses. Matches the upstream GTK headers' ABI. See ../README.md.
#pragma once

#include "../glib.h"
#include "../gio/gio.h"
#include "../gdk-pixbuf/gdk-pixbuf.h"

typedef struct _GtkWidget GtkWidget;
typedef struct _GtkWindow GtkWindow;
typedef struct _GtkContainer GtkContainer;
typedef struct _GtkDialog GtkDialog;
typedef struct _GtkFileChooser GtkFileChooser;
typedef struct _GtkFileFilter GtkFileFilter;
typedef struct _GtkClipboard GtkClipboard;
typedef struct _GtkMenu GtkMenu;
typedef struct _GtkMenuShell GtkMenuShell;
typedef struct _GtkMenuItem GtkMenuItem;

typedef struct _GdkScreen GdkScreen;
typedef struct _GdkWindow GdkWindow;

typedef enum {
    GDK_WINDOW_EDGE_NORTH_WEST,
    GDK_WINDOW_EDGE_NORTH,
    GDK_WINDOW_EDGE_NORTH_EAST,
    GDK_WINDOW_EDGE_WEST,
    GDK_WINDOW_EDGE_EAST,
    GDK_WINDOW_EDGE_SOUTH_WEST,
    GDK_WINDOW_EDGE_SOUTH,
    GDK_WINDOW_EDGE_SOUTH_EAST,
} GdkWindowEdge;
typedef struct _GdkDisplay GdkDisplay;
typedef struct _GdkMonitor GdkMonitor;
typedef struct _GdkEvent GdkEvent;
typedef struct _GdkAtom* GdkAtom;

typedef unsigned int GdkModifierType;

typedef struct _GdkRectangle GdkRectangle;
struct _GdkRectangle {
    int x;
    int y;
    int width;
    int height;
};

#define GDK_CURRENT_TIME ((guint32)0)

typedef enum {
    GTK_WINDOW_TOPLEVEL,
    GTK_WINDOW_POPUP,
} GtkWindowType;

typedef enum {
    GTK_WIN_POS_NONE,
    GTK_WIN_POS_CENTER,
    GTK_WIN_POS_MOUSE,
    GTK_WIN_POS_CENTER_ALWAYS,
    GTK_WIN_POS_CENTER_ON_PARENT,
} GtkWindowPosition;

typedef enum {
    GTK_MESSAGE_INFO,
    GTK_MESSAGE_WARNING,
    GTK_MESSAGE_QUESTION,
    GTK_MESSAGE_ERROR,
    GTK_MESSAGE_OTHER,
} GtkMessageType;

typedef enum {
    GTK_BUTTONS_NONE,
    GTK_BUTTONS_OK,
    GTK_BUTTONS_CLOSE,
    GTK_BUTTONS_CANCEL,
    GTK_BUTTONS_YES_NO,
    GTK_BUTTONS_OK_CANCEL,
} GtkButtonsType;

typedef enum {
    GTK_DIALOG_MODAL = 1 << 0,
    GTK_DIALOG_DESTROY_WITH_PARENT = 1 << 1,
    GTK_DIALOG_USE_HEADER_BAR = 1 << 2,
} GtkDialogFlags;

typedef enum {
    GTK_RESPONSE_NONE = -1,
    GTK_RESPONSE_REJECT = -2,
    GTK_RESPONSE_ACCEPT = -3,
    GTK_RESPONSE_DELETE_EVENT = -4,
    GTK_RESPONSE_OK = -5,
    GTK_RESPONSE_CANCEL = -6,
    GTK_RESPONSE_CLOSE = -7,
    GTK_RESPONSE_YES = -8,
    GTK_RESPONSE_NO = -9,
} GtkResponseType;

typedef enum {
    GTK_FILE_CHOOSER_ACTION_OPEN,
    GTK_FILE_CHOOSER_ACTION_SAVE,
    GTK_FILE_CHOOSER_ACTION_SELECT_FOLDER,
    GTK_FILE_CHOOSER_ACTION_CREATE_FOLDER,
} GtkFileChooserAction;

typedef void (*GtkMenuPositionFunc)(GtkMenu* menu, gint* x, gint* y, gboolean* push_in, gpointer user_data);
