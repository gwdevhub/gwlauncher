#include "utils/Files.h"

#include <chrono>
#include <fstream>
#include <iterator>
#include <system_error>

namespace gwl {

std::optional<Bytes> readFileBytes(const std::filesystem::path& path) {
    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return std::nullopt;
    }
    return Bytes((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
}

bool writeFileBytes(const std::filesystem::path& path, const Bytes& bytes) {
    std::ofstream file(path, std::ios::binary | std::ios::trunc);
    if (!file) {
        return false;
    }
    file.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    return static_cast<bool>(file);
}

bool isWritableFolder(const std::filesystem::path& folder) {
    std::error_code error;
    std::filesystem::path existing = folder;
    while (!existing.empty() && !std::filesystem::exists(existing, error)) {
        existing = existing.parent_path();
    }
    if (existing.empty() || !std::filesystem::is_directory(existing, error)) {
        return false;
    }
    const auto stamp = std::chrono::steady_clock::now().time_since_epoch().count();
    const std::filesystem::path probe = existing / (".gwl-write-test-" + std::to_string(stamp));
    bool written = false;
    {
        std::ofstream file(probe, std::ios::binary | std::ios::trunc);
        file << "x";
        written = static_cast<bool>(file);
    }
    std::filesystem::remove(probe, error);
    return written;
}

std::string lowerExtension(const std::filesystem::path& path) {
    return lowerCase(path.extension().string());
}

}  // namespace gwl
