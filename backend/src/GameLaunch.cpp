#include "GameLaunch.h"
#include "utils/Files.h"
#include "utils/Strings.h"

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

std::string gameCommandLine(const std::string& gwpath, const Account& account) {
    // A default-client launch has no email, so it passes no login arguments.
    std::vector<std::pair<std::string, std::string>> arguments;
    if (!account.email.empty()) {
        arguments.emplace_back("-email", account.email);
        arguments.emplace_back("-password", account.password);
        if (!account.character.empty()) {
            arguments.emplace_back("-character", account.character);
        }
    }
    std::string joined;
    for (std::size_t i = 0; i < arguments.size(); ++i) {
        if (i > 0) {
            joined += " ";
        }
        joined += arguments[i].first + " " + quoteWindowsArgument(arguments[i].second);
    }
    // The extra arguments follow a space even when there are none, as the C# launcher writes it.
    return quoteWindowsArgument(gwpath) + " " + joined + " " + account.extraargs;
}

std::optional<fs::path> steamAppIdPath(const std::string& gwpath) {
    const fs::path parent = pathFromUtf8(gwpath).parent_path();
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
