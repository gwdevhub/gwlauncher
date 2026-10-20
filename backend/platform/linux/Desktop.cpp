// Linux: the desktop facts the launcher checks, the error and file dialogs, the pointer, and the default install. GTK is
// opened at runtime, as the window does, so these work only when GTK is installed. The error falls back to zenity.

#include <dlfcn.h>
#include <spawn.h>
#include <sys/wait.h>

#include <gtk/gtk.h>

#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <optional>
#include <utility>
#include <string>
#include <vector>

#include "Launch.h"
#include "Settings.h"
#include "WebViewWindow.h"
#include "utils/Strings.h"
#include "utils/Files.h"

extern char** environ;

namespace gwl {

namespace {

// The GTK and GDK functions the dialogs use, from the libraries opened at runtime. Null when GTK isn't installed.
struct GtkDialogs {
    void* library = nullptr;
    gboolean (*initCheck)(int*, char***) = nullptr;
    GtkWidget* (*newMessageDialog)(GtkWindow*, GtkDialogFlags, GtkMessageType, GtkButtonsType, const gchar*, ...) = nullptr;
    GtkWidget* (*newFileChooser)(const gchar*, GtkWindow*, GtkFileChooserAction, const gchar*, ...) = nullptr;
    GtkFileFilter* (*newFilter)() = nullptr;
    gboolean (*setCurrentFolder)(GtkFileChooser*, const gchar*) = nullptr;
    void (*filterSetName)(GtkFileFilter*, const gchar*) = nullptr;
    void (*filterAddPattern)(GtkFileFilter*, const gchar*) = nullptr;
    void (*chooserAddFilter)(GtkFileChooser*, GtkFileFilter*) = nullptr;
    gint (*run)(GtkDialog*) = nullptr;
    void (*destroy)(GtkWidget*) = nullptr;
    gchar* (*fileName)(GtkFileChooser*) = nullptr;
    void (*freeString)(gpointer) = nullptr;
    GdkDisplay* (*defaultDisplay)() = nullptr;
    void (*displayPointer)(GdkDisplay*, GdkScreen**, gint*, gint*, GdkModifierType*) = nullptr;
    GdkScreen* (*defaultScreen)() = nullptr;
    GdkMonitor* (*primaryMonitor)(GdkDisplay*) = nullptr;
    void (*monitorWorkarea)(GdkMonitor*, GdkRectangle*) = nullptr;
    gint (*screenWidth)(GdkScreen*) = nullptr;
    gint (*screenHeight)(GdkScreen*) = nullptr;
};

template <typename F>
F symbolOf(void* library, const char* name) {
    return reinterpret_cast<F>(dlsym(library, name));
}

const GtkDialogs* gtkDialogs() {
    static const GtkDialogs dialogs = [] {
        GtkDialogs loaded;
        loaded.library = dlopen("libgtk-3.so.0", RTLD_LAZY | RTLD_LOCAL);
        if (!loaded.library) {
            return loaded;
        }
        void* library = loaded.library;
        loaded.initCheck = symbolOf<decltype(loaded.initCheck)>(library, "gtk_init_check");
        loaded.newMessageDialog = symbolOf<decltype(loaded.newMessageDialog)>(library, "gtk_message_dialog_new");
        loaded.newFileChooser = symbolOf<decltype(loaded.newFileChooser)>(library, "gtk_file_chooser_dialog_new");
        loaded.newFilter = symbolOf<decltype(loaded.newFilter)>(library, "gtk_file_filter_new");
        loaded.setCurrentFolder = symbolOf<decltype(loaded.setCurrentFolder)>(library, "gtk_file_chooser_set_current_folder");
        loaded.filterSetName = symbolOf<decltype(loaded.filterSetName)>(library, "gtk_file_filter_set_name");
        loaded.filterAddPattern = symbolOf<decltype(loaded.filterAddPattern)>(library, "gtk_file_filter_add_pattern");
        loaded.chooserAddFilter = symbolOf<decltype(loaded.chooserAddFilter)>(library, "gtk_file_chooser_add_filter");
        loaded.run = symbolOf<decltype(loaded.run)>(library, "gtk_dialog_run");
        loaded.destroy = symbolOf<decltype(loaded.destroy)>(library, "gtk_widget_destroy");
        loaded.fileName = symbolOf<decltype(loaded.fileName)>(library, "gtk_file_chooser_get_filename");
        loaded.freeString = symbolOf<decltype(loaded.freeString)>(library, "g_free");
        loaded.defaultDisplay = symbolOf<decltype(loaded.defaultDisplay)>(library, "gdk_display_get_default");
        loaded.displayPointer = symbolOf<decltype(loaded.displayPointer)>(library, "gdk_display_get_pointer");
        loaded.defaultScreen = symbolOf<decltype(loaded.defaultScreen)>(library, "gdk_screen_get_default");
        loaded.primaryMonitor = symbolOf<decltype(loaded.primaryMonitor)>(library, "gdk_display_get_primary_monitor");
        loaded.monitorWorkarea = symbolOf<decltype(loaded.monitorWorkarea)>(library, "gdk_monitor_get_workarea");
        loaded.screenWidth = symbolOf<decltype(loaded.screenWidth)>(library, "gdk_screen_get_width");
        loaded.screenHeight = symbolOf<decltype(loaded.screenHeight)>(library, "gdk_screen_get_height");
        if (!loaded.initCheck || !loaded.newMessageDialog || !loaded.newFileChooser || !loaded.run || !loaded.destroy ||
            !loaded.fileName || !loaded.freeString || !loaded.initCheck(nullptr, nullptr)) {
            loaded.library = nullptr;
        }
        return loaded;
    }();
    return dialogs.library ? &dialogs : nullptr;
}

// Shows the message with zenity, for when GTK isn't installed. Returns false when zenity isn't there either.
bool showWithZenity(const std::string& message) {
    const char* argv[] = {"zenity", "--error", "--title=GW Launcher", "--text", message.c_str(), nullptr};
    pid_t pid = 0;
    if (posix_spawnp(&pid, "zenity", nullptr, nullptr, const_cast<char**>(argv), environ) != 0) {
        return false;
    }
    int status = 0;
    waitpid(pid, &status, 0);
    return true;
}

// The Windows path the Rust launcher's Wine prefix has for the game, from the Path value under its ArenaNet key.
std::optional<std::string> gameInstallInPrefix(const std::filesystem::path& userReg) {
    std::ifstream file(userReg);
    bool inGameKey = false;
    std::string line;
    while (std::getline(file, line)) {
        if (!line.empty() && line[0] == '[') {
            inGameKey = line.rfind("[Software\\\\ArenaNet\\\\Guild Wars]", 0) == 0;
        } else if (inGameKey && line.rfind("\"Path\"=\"", 0) == 0) {
            std::string value;
            for (std::size_t i = 8; i < line.size() && line[i] != '"'; ++i) {
                if (line[i] == '\\' && i + 1 < line.size()) {
                    ++i;
                }
                value += line[i];
            }
            return value;
        }
    }
    return std::nullopt;
}

}  // namespace

void showError(const std::string& message) {
    std::fprintf(stderr, "%s\n", message.c_str());
    if (const GtkDialogs* gtk = gtkDialogs()) {
        GtkWidget* dialog = gtk->newMessageDialog(nullptr, GTK_DIALOG_MODAL, GTK_MESSAGE_ERROR, GTK_BUTTONS_OK, "%s", message.c_str());
        gtk->run(reinterpret_cast<GtkDialog*>(dialog));
        gtk->destroy(dialog);
        return;
    }
    showWithZenity(message);
}

std::optional<std::u16string> pickFile(const std::u16string& title, bool modsOnly, const std::u16string& startFolder) {
    const GtkDialogs* gtk = gtkDialogs();
    if (!gtk) {
        return std::nullopt;
    }
    GtkWidget* dialog = gtk->newFileChooser(utf16ToUtf8(title).c_str(), nullptr, GTK_FILE_CHOOSER_ACTION_OPEN, "_Cancel", GTK_RESPONSE_CANCEL,
                                            "_Open", GTK_RESPONSE_ACCEPT, nullptr);
    if (!startFolder.empty() && gtk->setCurrentFolder) {
        gtk->setCurrentFolder(reinterpret_cast<GtkFileChooser*>(dialog), utf16ToUtf8(startFolder).c_str());
    }
    // The patterns match the case the Windows filter ignores.
    if (gtk->newFilter && gtk->filterSetName && gtk->filterAddPattern && gtk->chooserAddFilter) {
        GtkFileFilter* filter = gtk->newFilter();
        gtk->filterSetName(filter, modsOnly ? "Mods" : "Guild Wars");
        const std::vector<const char*> patterns = modsOnly
            ? std::vector<const char*>{"*.dll", "*.DLL", "*.tpf", "*.TPF", "*.zip", "*.ZIP"}
            : std::vector<const char*>{"Gw.exe", "gw.exe", "GW.EXE"};
        for (const char* pattern : patterns) {
            gtk->filterAddPattern(filter, pattern);
        }
        gtk->chooserAddFilter(reinterpret_cast<GtkFileChooser*>(dialog), filter);
    }
    const gint response = gtk->run(reinterpret_cast<GtkDialog*>(dialog));
    std::optional<std::string> picked;
    if (response == GTK_RESPONSE_ACCEPT) {
        if (gchar* path = gtk->fileName(reinterpret_cast<GtkFileChooser*>(dialog))) {
            picked = path;
            gtk->freeString(path);
        }
    }
    gtk->destroy(dialog);
    return picked ? std::optional<std::u16string>(utf8ToUtf16(*picked)) : std::nullopt;
}

// The pointer's position on screen, from GDK. Missing when GTK isn't installed.
std::optional<std::pair<int, int>> cursorPosition() {
    const GtkDialogs* gtk = gtkDialogs();
    if (!gtk || !gtk->defaultDisplay || !gtk->displayPointer) {
        return std::nullopt;
    }
    GdkDisplay* display = gtk->defaultDisplay();
    if (!display) {
        return std::nullopt;
    }
    gint x = 0;
    gint y = 0;
    gtk->displayPointer(display, nullptr, &x, &y, nullptr);
    return std::make_pair(static_cast<int>(x), static_cast<int>(y));
}

// The screen's size in pixels, from GDK. Missing when GTK isn't installed.
std::optional<std::pair<int, int>> screenSize() {
    const GtkDialogs* gtk = gtkDialogs();
    if (!gtk || !gtk->defaultScreen || !gtk->screenWidth || !gtk->screenHeight) {
        return std::nullopt;
    }
    GdkScreen* screen = gtk->defaultScreen();
    if (!screen) {
        return std::nullopt;
    }
    return std::make_pair(static_cast<int>(gtk->screenWidth(screen)), static_cast<int>(gtk->screenHeight(screen)));
}

// The primary monitor's work area: the screen less the panels. Missing when GTK isn't installed.
std::optional<Rect> workArea() {
    const GtkDialogs* gtk = gtkDialogs();
    if (!gtk || !gtk->defaultDisplay || !gtk->primaryMonitor || !gtk->monitorWorkarea) {
        return std::nullopt;
    }
    GdkDisplay* display = gtk->defaultDisplay();
    GdkMonitor* monitor = display ? gtk->primaryMonitor(display) : nullptr;
    if (!monitor) {
        return std::nullopt;
    }
    GdkRectangle area{};
    gtk->monitorWorkarea(monitor, &area);
    return Rect{area.x, area.y, area.width, area.height};
}

// Missing: the monitor under a window isn't looked up here, so windows aren't clamped on Linux.
std::optional<Rect> workAreaAt(const Rect&) {
    return std::nullopt;
}

// The game's folder from the Rust launcher's Wine prefix, when the file it's in exists here.
std::optional<std::u16string> defaultInstall() {
    const auto windows = gameInstallInPrefix(launcherDataDir() / "prefix" / "user.reg");
    if (!windows) {
        return std::nullopt;
    }
    const auto linuxFile = linuxPathFor(utf8ToUtf16(*windows));
    // A WOF-compacted Gw.exe fails stat; the throwing overload would fail the whole state reply.
    std::error_code error;
    if (!linuxFile || !std::filesystem::is_regular_file(pathFromUtf16(*linuxFile), error)) {
        return std::nullopt;
    }
    return linuxFile;
}

}  // namespace gwl
