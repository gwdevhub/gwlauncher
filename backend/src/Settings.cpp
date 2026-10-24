#include "Settings.h"
#include "utils/Files.h"
#include "utils/Parsing.h"

#include <fstream>
#include <sstream>
#include <stdexcept>

namespace gwl {

namespace {

bool boolValue(const std::string& key, const Json& value) {
    if (!value.is_boolean()) {
        throw std::runtime_error("Settings.json is invalid: " + key + " isn't true or false");
    }
    return value.get<bool>();
}

std::int64_t intValue(const std::string& key, const Json& value) {
    if (!value.is_number_integer()) {
        throw std::runtime_error("Settings.json is invalid: " + key + " isn't a whole number");
    }
    return value.get<std::int64_t>();
}

std::string stringValue(const std::string& key, const Json& value) {
    if (!value.is_string()) {
        throw std::runtime_error("Settings.json is invalid: " + key + " isn't text");
    }
    return value.get<std::string>();
}

std::optional<std::int64_t> optionalIntValue(const std::string& key, const Json& value) {
    if (value.is_null()) {
        return std::nullopt;
    }
    return intValue(key, value);
}

}  // namespace

Settings Settings::effective(bool tray) const {
    Settings settings = *this;
    settings.checkForUpdates = true;
    if (!tray) {
        settings.keepInSystemTray = false;
        settings.launchMinimized = false;
        settings.showTitleBarInTray = false;
    }
    return settings;
}

Json Settings::forPage() const {
    return {
        {"promptForUpdates", promptForUpdates},
        {"keepInSystemTray", keepInSystemTray},
        {"launchMinimized", launchMinimized},
        {"showTitleBarInTray", showTitleBarInTray},
        {"listWidth", listWidth},
        {"listHeight", listHeight},
        {"timeoutOnModlaunch", timeoutOnModlaunch},
        {"timeoutOnGameWindow", timeoutOnGameWindow},
        {"steamPath", steamPath},
        {"uiScale", uiScale},
        {"winePath", winePath},
    };
}

Settings loadSettings(const std::string& path) {
    const auto bytes = readFileBytes(pathFromUtf8(path));
    if (!bytes) {
        return Settings{};
    }
    // A byte-order mark, which C# writes on Windows, is not part of the JSON.
    std::string text = withoutByteOrderMark(textOf(*bytes));

    nlohmann::ordered_json entries = nlohmann::ordered_json::parse(text, nullptr, false);
    if (entries.is_discarded()) {
        throw std::runtime_error("Settings.json is invalid: the file isn't JSON");
    }
    if (!entries.is_object()) {
        throw std::runtime_error("Settings.json is invalid: the file isn't a JSON object");
    }

    Settings settings;
    for (const auto& [key, value] : entries.items()) {
        if (key == "CheckForUpdates") {
            settings.checkForUpdates = boolValue(key, value);
        } else if (key == "PromptForUpdates") {
            settings.promptForUpdates = boolValue(key, value);
        } else if (key == "LaunchMinimized") {
            settings.launchMinimized = boolValue(key, value);
        } else if (key == "KeepInSystemTray") {
            settings.keepInSystemTray = boolValue(key, value);
        } else if (key == "ShowTitleBarInTray") {
            settings.showTitleBarInTray = boolValue(key, value);
        } else if (key == "KeepLauncherOpen" && !value.is_null()) {
            // The old inverse setting: a null value leaves the current setting alone.
            settings.keepInSystemTray = !boolValue(key, value);
        } else if (key == "ListWidth") {
            settings.listWidth = intValue(key, value);
        } else if (key == "ListHeight") {
            settings.listHeight = intValue(key, value);
        } else if (key == "ListX") {
            settings.listX = optionalIntValue(key, value);
        } else if (key == "ListY") {
            settings.listY = optionalIntValue(key, value);
        } else if (key == "UiScale") {
            settings.uiScale = intValue(key, value);
            if (settings.uiScale < 50 || settings.uiScale > 200) {
                throw std::runtime_error("Settings.json is invalid: " + key + " is out of range");
            }
        } else if (key == "SteamPath") {
            settings.steamPath = stringValue(key, value);
        } else if (key == "WinePath") {
            settings.winePath = stringValue(key, value);
        } else if (key == "TimeoutOnModlaunch" || key == "TimeoutOnGameWindow") {
            const std::int64_t timeout = intValue(key, value);
            const auto milliseconds = intToUint32(timeout);
            if (!milliseconds) {
                throw std::runtime_error("Settings.json is invalid: " + key + " is out of range");
            }
            if (key == "TimeoutOnGameWindow") {
                if (timeout == 0 || timeout == UINT32_MAX) {
                    throw std::runtime_error("Settings.json is invalid: " + key + " is out of range");
                }
                settings.timeoutOnGameWindow = *milliseconds;
            } else {
                settings.timeoutOnModlaunch = *milliseconds;
            }
        }
    }
    return settings;
}

void Settings::save(const std::string& path) const {
    // ordered_json keeps the keys in the order the C# file lists them.
    nlohmann::ordered_json json = {
        {"CheckForUpdates", true},
        {"PromptForUpdates", promptForUpdates},
        {"LaunchMinimized", launchMinimized},
        {"KeepInSystemTray", keepInSystemTray},
        {"ShowTitleBarInTray", showTitleBarInTray},
        {"ListWidth", listWidth},
        {"ListHeight", listHeight},
        {"ListX", listX ? Json(*listX) : Json(nullptr)},
        {"ListY", listY ? Json(*listY) : Json(nullptr)},
        {"TimeoutOnModlaunch", timeoutOnModlaunch},
        {"TimeoutOnGameWindow", timeoutOnGameWindow},
        {"SteamPath", steamPath},
        {"UiScale", uiScale},
        {"WinePath", winePath},
    };
    std::string text = json.dump(2);
    std::string crlf;
    crlf.reserve(text.size() + text.size() / 16);
    for (char c : text) {
        if (c == '\n') {
            crlf += '\r';
        }
        crlf += c;
    }
    if (!writeFileBytes(pathFromUtf8(path), bytesOf(crlf))) {
        throw std::runtime_error("Settings.json couldn't be written.");
    }
}


Settings Settings::applyFields(const Json& fields) const {
    Settings updated = *this;
    if (fields.contains("promptForUpdates")) {
        updated.promptForUpdates = fields["promptForUpdates"].get<bool>();
    }
    if (fields.contains("keepInSystemTray")) {
        updated.keepInSystemTray = fields["keepInSystemTray"].get<bool>();
    }
    if (fields.contains("launchMinimized")) {
        updated.launchMinimized = fields["launchMinimized"].get<bool>();
    }
    if (fields.contains("showTitleBarInTray")) {
        updated.showTitleBarInTray = fields["showTitleBarInTray"].get<bool>();
    }
    if (fields.contains("listWidth")) {
        updated.listWidth = fields["listWidth"].get<std::int64_t>();
    }
    if (fields.contains("listHeight")) {
        updated.listHeight = fields["listHeight"].get<std::int64_t>();
    }
    if (fields.contains("uiScale")) {
        const Json& scale = fields["uiScale"];
        if (!scale.is_number_integer() || scale.get<std::int64_t>() < 50 || scale.get<std::int64_t>() > 200) {
            throw std::runtime_error("The UI scale must be a whole number from 50 to 200.");
        }
        updated.uiScale = scale.get<std::int64_t>();
    }
    if (fields.contains("steamPath")) {
        updated.steamPath = fields["steamPath"].get<std::string>();
    }
    if (fields.contains("winePath")) {
        updated.winePath = fields["winePath"].get<std::string>();
    }
    for (const char* key : {"timeoutOnModlaunch", "timeoutOnGameWindow"}) {
        if (!fields.contains(key)) {
            continue;
        }
        const Json& timeout = fields[key];
        const auto milliseconds = timeout.is_number_integer() ? intToUint32(timeout.get<std::int64_t>()) : std::nullopt;
        if (!milliseconds || *milliseconds == 0 || *milliseconds == UINT32_MAX) {
            throw std::runtime_error("Timeouts must be greater than zero and less than 4294967.295 seconds.");
        }
        if (std::string(key) == "timeoutOnGameWindow") {
            updated.timeoutOnGameWindow = *milliseconds;
        } else {
            updated.timeoutOnModlaunch = *milliseconds;
        }
    }
    return updated;
}

}  // namespace gwl
