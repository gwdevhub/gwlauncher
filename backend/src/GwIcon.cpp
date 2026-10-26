#include "GwIcon.h"

#include <cstdint>
#include <map>
#include <mutex>
#include <system_error>

#include "DataUrl.h"
#include "GwPe.h"
#include "utils/Files.h"
#include "utils/Images.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

const std::uint32_t kIconSize = 64;

struct Cached {
    fs::file_time_type modified;
    std::optional<std::string> url;
};

std::mutex cacheMutex;
std::map<fs::path, Cached> cache;

std::optional<std::string> readIcon(const fs::path& exe) {
    const auto data = readFileBytes(exe);
    if (!data) {
        return std::nullopt;
    }
    const auto ico = gwIconFile(*data);
    if (!ico) {
        return std::nullopt;
    }
    const auto frame = decodeLargestIcon(*ico);
    if (!frame) {
        return std::nullopt;
    }
    return pngDataUrl(textOf(encodePng(scaleToFit(*frame, kIconSize))));
}

}  // namespace

std::optional<std::string> gwIconUrl(const fs::path& exe) {
    std::error_code error;
    const fs::file_time_type modified = fs::last_write_time(exe, error);
    if (error) {
        std::lock_guard<std::mutex> lock(cacheMutex);
        cache.erase(exe);
        return std::nullopt;
    }
    {
        std::lock_guard<std::mutex> lock(cacheMutex);
        const auto found = cache.find(exe);
        if (found != cache.end() && found->second.modified == modified) {
            return found->second.url;
        }
    }
    auto url = readIcon(exe);
    std::lock_guard<std::mutex> lock(cacheMutex);
    cache[exe] = Cached{modified, url};
    return url;
}

std::optional<std::string> cachedGwIconUrl(const fs::path& exe) {
    std::lock_guard<std::mutex> lock(cacheMutex);
    const auto found = cache.find(exe);
    return found == cache.end() ? std::nullopt : found->second.url;
}

}  // namespace gwl
