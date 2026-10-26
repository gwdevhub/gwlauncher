#include "GwIcon.h"

#include <cstdint>
#include <map>
#include <mutex>
#include <system_error>

#include "GwPe.h"
#include "utils/Files.h"
#include "utils/Images.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

const std::uint32_t kIconSize = 64;

struct Cached {
    std::mutex mutex;
    std::optional<fs::file_time_type> modified;
    std::optional<Bytes> png;
};

std::mutex cacheMutex;
std::map<fs::path, Cached> cache;

std::optional<Bytes> readIcon(const fs::path& path) {
    const auto data = readFileBytes(path);
    if (!data) {
        return std::nullopt;
    }
    auto frame = decodePng(*data);
    if (!frame) frame = decodeLargestIcon(*data);
    if (!frame) {
        if (const auto ico = gwIconFile(*data)) frame = decodeLargestIcon(*ico);
    }
    if (!frame) {
        return std::nullopt;
    }
    return encodePng(scaleToFit(*frame, kIconSize));
}

}  // namespace

std::optional<Bytes> pathIcon(const fs::path& path) {
    std::error_code error;
    const auto key = fs::weakly_canonical(path, error);
    if (error) return std::nullopt;
    Cached* entry;
    {
        std::lock_guard<std::mutex> lock(cacheMutex);
        // Entries are never erased, so their addresses remain valid for the process lifetime.
        entry = &cache.try_emplace(key).first->second;
    }
    // Serialize requests for the same file without holding up unrelated icon loads.
    std::lock_guard<std::mutex> lock(entry->mutex);
    const auto modified = fs::last_write_time(key, error);
    if (error) {
        entry->modified.reset();
        entry->png.reset();
        return std::nullopt;
    }
    if (entry->modified != modified) {
        entry->png = readIcon(key);
        entry->modified = modified;
    }
    return entry->png;
}

}  // namespace gwl
