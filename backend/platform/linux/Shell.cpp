// Linux: the clipboard through GTK, which the windows already run on, and the desktop through xdg-open and .desktop files.
// GTK is opened at runtime, as the window does, so the clipboard does nothing when GTK isn't installed.

#include "Shell.h"

#include <dlfcn.h>
#include <gtk/gtk.h>
#include <unistd.h>

#include <cstddef>
#include <cstdlib>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

#include "Process.h"

namespace gwl {

namespace {

namespace fs = std::filesystem;

const char* const kIconFile = "gwlauncher-helmet.png";

struct ClipboardApi {
    GtkClipboard* (*clipboardGet)(GdkAtom) = nullptr;
    void (*setText)(GtkClipboard*, const gchar*, gint) = nullptr;
    GdkAtom (*atomIntern)(const gchar*, gboolean) = nullptr;
};

const ClipboardApi* clipboardApi() {
    static const ClipboardApi api = [] {
        ClipboardApi loaded;
        void* library = dlopen("libgtk-3.so.0", RTLD_LAZY | RTLD_LOCAL);
        if (!library) {
            return loaded;
        }
        loaded.clipboardGet = reinterpret_cast<decltype(loaded.clipboardGet)>(dlsym(library, "gtk_clipboard_get"));
        loaded.setText = reinterpret_cast<decltype(loaded.setText)>(dlsym(library, "gtk_clipboard_set_text"));
        loaded.atomIntern = reinterpret_cast<decltype(loaded.atomIntern)>(dlsym(library, "gdk_atom_intern"));
        if (!loaded.clipboardGet || !loaded.setText || !loaded.atomIntern) {
            loaded = ClipboardApi{};
        }
        return loaded;
    }();
    return api.clipboardGet ? &api : nullptr;
}

// Writes the file's bytes, replacing it.
void writeFile(const fs::path& file, const std::string& bytes, const std::string& what) {
    std::ofstream out(file, std::ios::binary | std::ios::trunc);
    out << bytes;
    if (!out) {
        throw std::runtime_error("Could not write the " + what + ": " + file.string());
    }
}

// The desktop folder from the XDG user dirs, which may be localised, else ~/Desktop.
fs::path desktopFromUserDirs(const fs::path& home) {
    std::ifstream file(home / ".config" / "user-dirs.dirs");
    std::string line;
    const std::string prefix = "XDG_DESKTOP_DIR=\"";
    while (std::getline(file, line)) {
        if (line.rfind(prefix, 0) == 0 && line.size() > prefix.size() && line.back() == '"') {
            std::string dir = line.substr(prefix.size(), line.size() - prefix.size() - 1);
            const std::string variable = "$HOME";
            if (dir.rfind(variable, 0) == 0) {
                dir = home.string() + dir.substr(variable.size());
            }
            return dir;
        }
    }
    return home / "Desktop";
}

// The Exec argument, quoted so a shell reads it as one word: the characters the quoted form treats specially are escaped.
std::string execQuote(const std::string& argument) {
    std::string quoted = "\"";
    for (char c : argument) {
        if (c == '"' || c == '`' || c == '$' || c == '\\') {
            quoted += '\\';
        }
        quoted += c;
    }
    return quoted + "\"";
}

std::string desktopEntry(const std::string& name, const std::string& description, const std::string& exec, const std::string& icon,
                         const std::string& workdir) {
    return "[Desktop Entry]\nType=Application\nName=" + name + "\nComment=" + description + "\nExec=" + exec + "\nIcon=" + icon +
           "\nStartupWMClass=gwl_launcher\nPath=" + workdir + "\nTerminal=false\nCategories=Game;\n";
}

}  // namespace

void copyText(const std::string& text) {
    const ClipboardApi* api = clipboardApi();
    if (!api) {
        return;
    }
    GtkClipboard* clipboard = api->clipboardGet(api->atomIntern("CLIPBOARD", FALSE));
    api->setText(clipboard, text.c_str(), static_cast<gint>(text.size()));
}

void openUrl(const std::string& url) {
    runProgramDetached({"xdg-open", url});
}

void revealInFileExplorer(const fs::path& path) {
    const fs::path folder = path.has_parent_path() ? path.parent_path() : path;
    runProgramDetached({"xdg-open", folder.string()});
}

std::optional<fs::path> desktopFolder() {
    const char* home = std::getenv("HOME");
    if (!home) {
        return std::nullopt;
    }
    return desktopFromUserDirs(home);
}

fs::path executablePath() {
    return fs::read_symlink("/proc/self/exe");
}

void createShortcut(const fs::path& file, const fs::path& target, const std::string& arguments, const std::string& description,
                    const std::string& iconPng) {
    const fs::path iconFile = fs::current_path() / kIconFile;
    writeFile(iconFile, iconPng, "shortcut icon");

    const std::string name = file.stem().string();
    std::string exec = execQuote(target.string()) + " " + arguments;
    for (std::size_t at = 0; (at = exec.find('%', at)) != std::string::npos; at += 2) {
        exec.insert(at, "%");
    }
    const fs::path entry = file.parent_path() / (name + ".desktop");
    writeFile(entry, desktopEntry(name, description, exec, iconFile.string(), target.parent_path().string()), "shortcut");
    fs::permissions(entry, fs::perms::owner_all | fs::perms::group_read | fs::perms::group_exec | fs::perms::others_read |
                               fs::perms::others_exec);

    // GNOME won't launch a desktop icon until it's trusted. Best effort, since gio may be missing.
    runProgram({"gio", "set", entry.string(), "metadata::trusted", "true"});
}

}  // namespace gwl
