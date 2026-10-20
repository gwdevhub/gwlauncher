#include "GameLaunch.h"
#include "utils/Files.h"

#include <fstream>
#include <stdexcept>
#include <system_error>
#include <utility>
#include <vector>

namespace gwl {

namespace fs = std::filesystem;

namespace {

const char* const kSteamAppIdFile = "steam_appid.txt";

}  // namespace

std::u16string gameCommandLine(const std::u16string& gwpath, const Account& account) {
    // A default-client launch has no email, so it passes no login arguments.
    std::vector<std::pair<std::u16string, std::u16string>> arguments;
    if (!account.email.empty()) {
        arguments.emplace_back(u"-email", account.email);
        arguments.emplace_back(u"-password", account.password);
        if (!account.character.empty()) {
            arguments.emplace_back(u"-character", account.character);
        }
    }
    std::u16string joined;
    for (std::size_t i = 0; i < arguments.size(); ++i) {
        if (i > 0) {
            joined += u" ";
        }
        joined += arguments[i].first + u" \"" + arguments[i].second + u"\"";
    }
    // The extra arguments follow a space even when there are none, as the C# launcher writes it.
    return u"\"" + gwpath + u"\" " + joined + u" " + account.extraargs;
}

std::optional<fs::path> steamAppIdPath(const std::u16string& gwpath) {
    const fs::path parent = pathFromUtf16(gwpath).parent_path();
    if (gwpath.empty()) {
        return std::nullopt;
    }
    return parent / kSteamAppIdFile;
}

void writeSteamAppId(const Account& account) {
    if (!account.isSteamAccount) {
        return;
    }
    const auto path = steamAppIdPath(account.gwpath);
    if (!path) {
        throw std::runtime_error("the game path has no folder");
    }
    std::ofstream file(*path, std::ios::binary | std::ios::trunc);
    file << kSteamAppId;
    if (!file) {
        throw std::runtime_error("steam_appid.txt couldn't be written to " + pathToUtf8(*path));
    }
}

void removeSteamAppId(const Account& account) {
    if (!account.isSteamAccount) {
        return;
    }
    const auto path = steamAppIdPath(account.gwpath);
    if (!path) {
        return;
    }
    std::error_code error;
    fs::remove(*path, error);
    if (error) {
        throw std::runtime_error("steam_appid.txt couldn't be removed: " + error.message());
    }
}

}  // namespace gwl
