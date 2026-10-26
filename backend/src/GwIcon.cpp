#include "GwIcon.h"

#include <cstdint>
#include <map>
#include <mutex>
#include <system_error>
#include <utility>

#include "GwPe.h"
#include "Shortcuts.h"
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
std::map<std::pair<fs::path, int>, Cached> cache;

std::optional<Bytes> readIcon(const fs::path& path, int iconIndex) {
    const auto data = readFileBytes(path);
    if (!data) {
        return std::nullopt;
    }
    auto frame = decodePng(*data);
    if (!frame) frame = decodeLargestIcon(*data);
    if (!frame) {
        if (const auto ico = gwIconFile(*data, iconIndex)) frame = decodeLargestIcon(*ico);
    }
    if (!frame) {
        return std::nullopt;
    }
    return encodePng(scaleToFit(*frame, kIconSize));
}

}  // namespace

namespace {

std::optional<Bytes> cachedIcon(const fs::path& path, int iconIndex = 0) {
    std::error_code error;
    const auto file = fs::weakly_canonical(path, error);
    if (error) return std::nullopt;
    Cached* entry;
    {
        std::lock_guard<std::mutex> lock(cacheMutex);
        // Entries are never erased, so their addresses remain valid for the process lifetime.
        entry = &cache.try_emplace(std::make_pair(file, iconIndex)).first->second;
    }
    // Serialize requests for the same file without holding up unrelated icon loads.
    std::lock_guard<std::mutex> lock(entry->mutex);
    const auto modified = fs::last_write_time(file, error);
    if (error) {
        entry->modified.reset();
        entry->png.reset();
        return std::nullopt;
    }
    if (entry->modified != modified) {
        entry->png = readIcon(file, iconIndex);
        entry->modified = modified;
    }
    return entry->png;
}

}  // namespace

std::optional<Bytes> pathIcon(const fs::path& path) {
    const auto target = resolveShortcut(path);
    if (target.icon) {
        if (auto png = cachedIcon(resolveShortcut(*target.icon).file, target.iconIndex)) return png;
    }
    return cachedIcon(target.file);
}

}  // namespace gwl
