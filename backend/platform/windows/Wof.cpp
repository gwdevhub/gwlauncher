// Windows: Windows reads its own compacted files, so none of these are needed here.

#include <stdexcept>

#include "Wof.h"

namespace gwl {

std::optional<std::string> wofProblem(const std::filesystem::path&) {
    return std::nullopt;
}

std::optional<std::uint32_t> wofAlgorithm(const std::filesystem::path&) {
    return std::nullopt;
}

std::unique_ptr<WofData> openWofData(const std::filesystem::path& path) {
    throw std::runtime_error(path.string() + " is not a Windows-compacted file.");
}

std::uint64_t freeBytesAt(const std::filesystem::path&) {
    throw std::runtime_error("Free space isn't read on Windows for compacted files.");
}

}  // namespace gwl
