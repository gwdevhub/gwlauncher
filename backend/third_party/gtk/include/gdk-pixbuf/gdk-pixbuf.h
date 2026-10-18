// The gdk-pixbuf types the launcher uses. Matches the upstream gdk-pixbuf headers' ABI. See ../README.md.
#pragma once

#include "../glib.h"

typedef struct _GdkPixbuf GdkPixbuf;
typedef struct _GdkPixbufLoader GdkPixbufLoader;

typedef enum {
    GDK_INTERP_NEAREST,
    GDK_INTERP_TILES,
    GDK_INTERP_BILINEAR,
    GDK_INTERP_HYPER,
} GdkInterpType;
