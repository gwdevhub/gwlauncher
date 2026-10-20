#include "GMod.h"

#include <array>
#include <cstdint>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <sstream>
#include <stdexcept>
#include <vector>

#include "Messages.h"
#include "Process.h"
#include "utils/Crypto.h"
#include "utils/Files.h"

namespace gwl {

namespace {

namespace fs = std::filesystem;

const char* const kReleases = "https://api.github.com/repos/gwdevhub/gMod/releases";
const char* const kAsset = "gMod.dll";

}  // namespace

bool isUsableDll(const std::string& path) {
    const std::string bytes = textOf(readFileBytes(pathFromUtf8(path)).value_or(Bytes()));
    if (bytes.size() < 0x40 || bytes.compare(0, 2, "MZ") != 0) {
        return false;
    }
    const auto offset = static_cast<std::uint8_t>(bytes[0x3C]) | (static_cast<std::uint8_t>(bytes[0x3D]) << 8) |
                        (static_cast<std::uint8_t>(bytes[0x3E]) << 16) | (static_cast<std::uint8_t>(bytes[0x3F]) << 24);
    if (offset + 6 > bytes.size()) {
        return false;
    }
    return bytes.compare(offset, 4, std::string("PE\0\0", 4)) == 0 && static_cast<std::uint8_t>(bytes[offset + 4]) == 0x4c &&
           static_cast<std::uint8_t>(bytes[offset + 5]) == 0x01;
}

std::string downloadGMod(const std::string& folder, const ProgressReport& report, const std::atomic<bool>& cancel) {
    report("Finding the latest gMod release...", std::nullopt);
    const fs::path releases = fs::temp_directory_path() / "gwl-gmod-releases.json";
    if (runProgram({"curl", "-sfL", "-H", "User-Agent: GWLauncher", "-H", "Accept: application/vnd.github+json", "-o",
                    pathToUtf8(releases), kReleases}) != 0) {
        throw std::runtime_error("Couldn't reach GitHub for gMod.dll.");
    }
    const Json list = Json::parse(textOf(readFileBytes(releases).value_or(Bytes())), nullptr, false);
    fs::remove(releases);
    if (list.is_discarded() || !list.is_array()) {
        throw std::runtime_error("GitHub sent a reply that wasn't a release list.");
    }

    std::string url;
    std::string digest;
    for (const Json& release : list) {
        if (release.value("draft", false) || release.value("prerelease", false) || !release.contains("assets")) {
            continue;
        }
        for (const Json& asset : release["assets"]) {
            if (asset.value("name", std::string()) == kAsset) {
                url = asset.value("browser_download_url", std::string());
                const std::string prefixed = asset.value("digest", std::string());
                digest = prefixed.rfind("sha256:", 0) == 0 ? prefixed.substr(7) : std::string();
                break;
            }
        }
        if (!url.empty()) {
            break;
        }
    }
    if (url.empty()) {
        throw std::runtime_error("The gwdevhub/gMod releases on GitHub don't include gMod.dll.");
    }
    if (cancel.load()) {
        throw std::runtime_error(kCancelledMessage);
    }

    const fs::path target = pathFromUtf8(folder) / kAsset;
    const fs::path temp = pathFromUtf8(folder) / (std::string(kAsset) + ".download");
    report("Downloading gMod.dll...", std::nullopt);
    if (runProgram({"curl", "-sfL", "-o", pathToUtf8(temp), url}) != 0) {
        fs::remove(temp);
        throw std::runtime_error("Couldn't download gMod.dll.");
    }
    if (cancel.load()) {
        fs::remove(temp);
        throw std::runtime_error(kCancelledMessage);
    }
    report("Checking gMod.dll...", std::nullopt);
    if (!digest.empty() && sha256Hex(readFileBytes(temp).value_or(Bytes())) != digest) {
        fs::remove(temp);
        throw std::runtime_error("The downloaded gMod.dll didn't match its published SHA-256 digest, so it was discarded.");
    }
    if (!isUsableDll(pathToUtf8(temp))) {
        fs::remove(temp);
        throw std::runtime_error("The downloaded gMod.dll isn't a 32-bit Windows DLL, so it was discarded.");
    }
    fs::rename(temp, target);
    return pathToUtf8(target);
}

}  // namespace gwl
