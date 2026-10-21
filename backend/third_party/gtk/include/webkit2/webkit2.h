// The WebKitGTK types the launcher uses. Matches the upstream WebKitGTK 4.1 headers' ABI. See ../README.md.
#pragma once

#include "../gtk/gtk.h"

typedef struct _WebKitWebView WebKitWebView;
typedef struct _WebKitUserContentManager WebKitUserContentManager;
typedef struct _WebKitUserMessage WebKitUserMessage;
typedef struct _WebKitSettings WebKitSettings;
typedef struct _WebKitWebInspector WebKitWebInspector;

// The JavaScriptCore value a script message carries, from the JavaScriptCore part of WebKitGTK.
typedef struct _JSCValue JSCValue;

// The load progress a load-changed signal reports.
typedef enum {
    WEBKIT_LOAD_STARTED,
    WEBKIT_LOAD_REDIRECTED,
    WEBKIT_LOAD_COMMITTED,
    WEBKIT_LOAD_FINISHED
} WebKitLoadEvent;
