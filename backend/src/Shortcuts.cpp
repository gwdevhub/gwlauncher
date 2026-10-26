#include "Shortcuts.h"

#include <algorithm>
#include <cstdlib>
#include <map>
#include <mutex>
#include <set>
#include <sstream>
#include <stdexcept>
#include <utility>
#include <vector>

#include "Accounts.h"
#include "Launch.h"
#include "ShellLink.h"
#include "utils/Files.h"

namespace gwl {
namespace fs = std::filesystem;
namespace {

std::mutex targetMutex;
std::map<fs::path, fs::path> targets;

std::string environment(const char* name) {
    const auto value = std::getenv(name);
    return value ? value : "";
}

fs::path localPath(std::string path, const fs::path& folder) {
    if (path.starts_with("~/")) path = environment("HOME") + path.substr(1);
    for (std::size_t at = 0; (at = path.find('%', at)) != std::string::npos;) {
        const auto end = path.find('%', at + 1);
        if (end == std::string::npos) break;
        const auto value = environment(path.substr(at + 1, end - at - 1).c_str());
        if (value.empty()) at = end + 1;
        else {
            path.replace(at, end - at + 1, value);
            at += value.size();
        }
    }
    const auto mapped = linuxPathFor(path);
    if (!mapped) throw std::runtime_error("Shortcut path cannot be resolved: " + path);
    auto native = *mapped;
    if (fs::path::preferred_separator == '/') std::replace(native.begin(), native.end(), '\\', '/');
    auto file = pathFromUtf8(native);
    if (file.is_relative()) file = folder / file;
    return file.lexically_normal();
}

std::optional<fs::path> iconFile(const std::string& name, const fs::path& folder) {
    if (name.empty()) return std::nullopt;
    fs::path direct;
    try { direct = localPath(name, folder); }
    catch (const std::exception&) { return std::nullopt; }
    std::error_code error;
    if (fs::is_regular_file(direct, error)) return direct;
    // Desktop entries commonly use a theme name rather than an absolute icon filename.
    std::string roots = environment("XDG_DATA_HOME");
    if (roots.empty()) roots = environment("HOME") + "/.local/share";
    roots += ":" + (environment("XDG_DATA_DIRS").empty() ? "/usr/local/share:/usr/share" : environment("XDG_DATA_DIRS"));
    std::istringstream directories(roots);
    for (std::string root; std::getline(directories, root, ':');) {
        for (const auto* size : {"64x64", "48x48", "128x128", "256x256", "32x32", "16x16"}) {
            for (const auto* suffix : {"", ".png", ".ico"}) {
                const auto candidate = pathFromUtf8(root) / "icons/hicolor" / size / "apps" / pathFromUtf8(name + suffix);
                if (fs::is_regular_file(candidate, error)) return candidate;
            }
        }
        for (const auto* suffix : {"", ".png", ".ico"}) {
            const auto candidate = pathFromUtf8(root) / "pixmaps" / pathFromUtf8(name + suffix);
            if (fs::is_regular_file(candidate, error)) return candidate;
        }
    }
    return std::nullopt;
}

std::string unescape(std::string value) {
    std::string out;
    for (std::size_t i = 0; i < value.size(); ++i) {
        if (value[i] == '\\' && i + 1 < value.size()) {
            switch (value[i + 1]) {
                case 's': out += ' '; ++i; continue;
                case 'n': out += '\n'; ++i; continue;
                case 't': out += '\t'; ++i; continue;
                case 'r': out += '\r'; ++i; continue;
                case '\\': out += '\\'; ++i; continue;
            }
        }
        out += value[i];
    }
    return out;
}

std::vector<std::string> execWords(const std::string& text) {
    std::vector<std::string> words;
    std::string word;
    bool quoted = false;
    bool started = false;
    const auto flush = [&] {
        if (!started) return;
        words.push_back(std::move(word));
        word.clear();
        started = false;
    };
    for (std::size_t i = 0; i < text.size(); ++i) {
        const char c = text[i];
        if (c == '"') {
            quoted = !quoted;
            started = true;
        } else if (c == '\\' && i + 1 < text.size() && std::string("\\\"`$ \t").find(text[i + 1]) != std::string::npos) {
            word += text[++i];
            started = true;
        } else if (!quoted && (c == ' ' || c == '\t')) {
            flush();
        } else {
            word += c;
            started = true;
        }
    }
    if (quoted) throw std::runtime_error("Unterminated quote in desktop Exec.");
    flush();
    return words;
}

ShortcutTarget desktopTarget(const fs::path& file, bool resolveIcon) {
    const auto bytes = readFileBytes(file);
    if (!bytes) throw std::runtime_error("Desktop shortcut could not be read: " + pathToUtf8(file));
    std::map<std::string, std::string> fields;
    std::istringstream lines(withoutByteOrderMark(textOf(*bytes)));
    bool entry = false;
    for (std::string line; std::getline(lines, line);) {
        line = trimmed(line);
        if (line.empty() || line[0] == '#') continue;
        if (line[0] == '[') { entry = line == "[Desktop Entry]"; continue; }
        const auto equals = line.find('=');
        if (entry && equals != std::string::npos) fields[trimmed(line.substr(0, equals))] = unescape(line.substr(equals + 1));
    }
    if (fields["Type"] != "Application" || fields["Exec"].empty()) throw std::runtime_error("Desktop shortcut requires Type=Application and Exec.");
    if (!resolveIcon && fields["Terminal"] == "true") throw std::runtime_error("Terminal desktop entries cannot launch a Guild Wars client.");
    auto words = execWords(fields["Exec"]);
    if (words.empty() || words.front().empty()) throw std::runtime_error("Desktop shortcut has an empty Exec command.");
    const auto command = lowerCase(pathToUtf8(pathFromUtf8(words[0]).filename()));
    if (command == "wine" || command == "wine64") words.erase(words.begin());
    if (words.empty()) throw std::runtime_error("Desktop shortcut must name an executable after wine.");
    const auto folder = fields["Path"].empty() ? file.parent_path() : localPath(fields["Path"], file.parent_path());
    auto target = localPath(words.front(), folder);
    std::error_code error;
    if (!fs::is_regular_file(target, error) && words.front().find_first_of("/\\:") == std::string::npos) {
        const char separator = fs::path::preferred_separator == '\\' ? ';' : ':';
        std::istringstream paths(environment("PATH"));
        for (std::string directory; std::getline(paths, directory, separator);) {
            auto candidate = pathFromUtf8(directory) / pathFromUtf8(words.front());
            if (fs::is_regular_file(candidate, error)) { target = candidate; break; }
        }
    }
    ShortcutTarget result{target, {}, resolveIcon ? iconFile(fields["Icon"], file.parent_path()) : std::nullopt};
    for (std::size_t i = 1; i < words.size(); ++i) {
        if (words[i] == "%f" || words[i] == "%F" || words[i] == "%u" || words[i] == "%U") continue;
        if (words[i] == "%i") {
            if (!fields["Icon"].empty()) result.arguments += " --icon " + quoteWindowsArgument(fields["Icon"]);
            continue;
        }
        std::string argument;
        for (std::size_t j = 0; j < words[i].size(); ++j) {
            if (words[i][j] != '%') { argument += words[i][j]; continue; }
            if (++j == words[i].size()) throw std::runtime_error("Incomplete desktop Exec field code.");
            switch (words[i][j]) {
                case '%': argument += '%'; break;
                case 'c': argument += fields["Name"]; break;
                case 'k': argument += pathToUtf8(file); break;
                default: throw std::runtime_error("Unsupported desktop Exec field code.");
            }
        }
        result.arguments += " " + quoteWindowsArgument(argument);
    }
    result.arguments = trimmed(result.arguments);
    return result;
}

}  // namespace

bool isShortcut(const fs::path& path) {
    const auto extension = lowerExtension(path);
    return extension == ".lnk" || extension == ".desktop";
}

ShortcutTarget resolveShortcut(const fs::path& path, bool resolveIcon) {
    ShortcutTarget result{path, {}, std::nullopt};
    if (!isShortcut(path)) return result;
    const auto root = fs::absolute(path).lexically_normal();
    {
        std::lock_guard<std::mutex> lock(targetMutex);
        targets.erase(root);
    }
    std::set<fs::path> seen;
    while (isShortcut(result.file)) {
        const auto file = fs::weakly_canonical(result.file);
        if (seen.size() >= 16 || !seen.insert(file).second) throw std::runtime_error("Shortcut chain contains a cycle or is too deep.");
        ShortcutTarget next;
        if (lowerExtension(file) == ".desktop") next = desktopTarget(file, resolveIcon);
        else {
            const auto link = readShellLink(file);
            if (!link.windowsPath) throw std::runtime_error("Windows shortcut has no filesystem target.");
            next = {localPath(*link.windowsPath, file.parent_path()), link.arguments.value_or(""),
                    resolveIcon && link.iconPath ? iconFile(*link.iconPath, file.parent_path()) : std::nullopt, link.iconIndex};
        }
        if (!result.icon && next.icon) {
            result.icon = next.icon;
            result.iconIndex = next.iconIndex;
        }
        result.arguments = trimmed(next.arguments + " " + result.arguments);
        result.file = next.file;
    }
    {
        std::lock_guard<std::mutex> lock(targetMutex);
        targets[root] = result.file;
    }
    return result;
}

std::optional<fs::path> cachedShortcutTarget(const fs::path& path) {
    std::lock_guard<std::mutex> lock(targetMutex);
    const auto found = targets.find(fs::absolute(path).lexically_normal());
    return found == targets.end() ? std::nullopt : std::optional<fs::path>(found->second);
}

Account resolveAccountShortcut(const Account& account) {
    if (!isShortcut(pathFromUtf8(account.gwpath))) return account;
    const auto local = linuxPathFor(account.gwpath);
    if (!local) throw std::runtime_error("Game shortcut path cannot be resolved.");
    const auto path = pathFromUtf8(*local);
    const auto target = resolveShortcut(path, false);
    if (lowerExtension(target.file) != ".exe") throw std::runtime_error("Game shortcuts must target the Guild Wars executable, not a shell or application launcher.");
    Account resolved = account;
    resolved.gwpath = pathToUtf8(target.file);
    resolved.extraargs = trimmed(target.arguments + " " + account.extraargs);
    return resolved;
}

std::optional<fs::path> gameFilePath(const std::string& launcherPath) {
    const auto local = linuxPathFor(launcherPath);
    if (!local) return std::nullopt;
    try { return resolveShortcut(pathFromUtf8(*local), false).file; }
    catch (const std::exception&) { return std::nullopt; }
}

}  // namespace gwl
