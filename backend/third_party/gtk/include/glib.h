// The GLib basics the launcher uses. Matches the upstream GLib headers' ABI. See ../README.md.
#pragma once

#include <stddef.h>
#include <stdint.h>
#include <sys/types.h>

typedef char gchar;
typedef unsigned char guchar;
typedef int gint;
typedef int gint32;
typedef unsigned int guint;
typedef uint32_t guint32;
typedef int gboolean;

#define TRUE 1
#define FALSE 0
typedef void* gpointer;
typedef const void* gconstpointer;
typedef size_t gsize;
typedef ssize_t gssize;
typedef unsigned long gulong;
typedef uint32_t GQuark;

typedef void (*GCallback)(void);
typedef void (*GDestroyNotify)(gpointer data);
typedef void (*GClosureNotify)(gpointer data, void* closure);
typedef gboolean (*GSourceFunc)(gpointer data);
typedef void (*GAsyncReadyCallback)(void* source, void* result, gpointer user_data);

typedef struct _GError GError;
struct _GError {
    GQuark domain;
    gint code;
    gchar* message;
};

typedef struct _GCancellable GCancellable;
typedef struct _GVariant GVariant;
typedef struct _GVariantType GVariantType;

// The builder is used by value, so its size and alignment are the upstream ones: 128 bytes.
typedef struct _GVariantBuilder GVariantBuilder;
struct _GVariantBuilder {
    union {
        struct {
            gsize partial_magic;
            const GVariantType* type;
            uintptr_t y[14];
        } s;
        uintptr_t x[16];
    } u;
};

typedef enum {
    G_CONNECT_AFTER = 1 << 0,
    G_CONNECT_SWAPPED = 1 << 1,
} GConnectFlags;
