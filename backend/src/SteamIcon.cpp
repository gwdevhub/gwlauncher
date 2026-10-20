#include "SteamIcon.h"

#include <atomic>
#include <cstdint>
#include <filesystem>
#include <system_error>
#include <thread>

#include "DataUrl.h"
#include "Launch.h"
#include "Process.h"
#include "WebViewWindow.h"
#include "utils/Files.h"
#include "utils/Images.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

const char* const kFavicon = "https://store.steampowered.com/favicon.ico";
const char* const kCacheFile = "steam-favicon.ico";
const std::uint32_t kIconSize = 64;

// The UI thread reads and writes this; the fetch thread only writes the file.
std::optional<std::string> cachedUrl;

fs::path cachePath() {
    return launcherDataDir() / kCacheFile;
}

// The PNG data URL of an icon file's bytes, or nothing when they aren't an icon.
std::optional<std::string> iconUrl(const Bytes& ico) {
    const auto frame = decodeLargestIcon(ico);
    if (!frame) {
        return std::nullopt;
    }
    return pngDataUrl(textOf(encodePng(scaleToFit(*frame, kIconSize))));
}

}  // namespace

std::optional<std::string> steamIconUrl() {
    if (!cachedUrl) {
        if (const auto bytes = readFileBytes(cachePath())) {
            cachedUrl = iconUrl(*bytes);
        }
    }
    return cachedUrl;
}

void fetchSteamIcon(const std::function<void()>& done) {
    static std::atomic<bool> started{false};
    if (steamIconUrl() || started.exchange(true)) {
        return;
    }
    std::thread([done] {
        const fs::path target = cachePath();
        fs::path temp = target;
        temp += ".download";
        std::error_code error;
        fs::create_directories(target.parent_path(), error);
        // -m bounds the wait, so an offline start doesn't hold the launcher up.
        const bool downloaded = runProgram({"curl", "-sfL", "-m", "15", "-o", pathToUtf8(temp), kFavicon}) == 0;
        const auto bytes = downloaded ? readFileBytes(temp) : std::nullopt;
        // The file must decode as an icon, so an error page is never kept as the icon.
        if (!bytes || !iconUrl(*bytes) || !writeFileBytes(target, *bytes)) {
            fs::remove(temp, error);
            return;
        }
        fs::remove(temp, error);
        runOnUiThread(done);
    }).detach();
}

}  // namespace gwl
