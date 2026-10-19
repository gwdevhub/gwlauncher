#include "GwIcon.h"

#include <cstdint>
#include <map>
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
    // The window reads icons on its own thread only, so the cache needs no lock.
    static std::map<fs::path, Cached> cache;
    std::error_code error;
    const fs::file_time_type modified = fs::last_write_time(exe, error);
    if (error) {
        return std::nullopt;
    }
    const auto found = cache.find(exe);
    if (found != cache.end() && found->second.modified == modified) {
        return found->second.url;
    }
    auto url = readIcon(exe);
    cache[exe] = Cached{modified, url};
    return url;
}

}  // namespace gwl
