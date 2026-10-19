// Windows-compacted (WOF) files, which Linux can't read. Detection and the reader are per platform; decompressing a file in place
// is shared, and so is the decoding in WofDecode.h.

#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <memory>
#include <optional>
#include <string>

#include "Progress.h"
#include "utils/Strings.h"

namespace gwl {

// The compressed data of a compacted file, read as the platform allows. Destroying it stops the reader.
class WofData {
public:
    virtual ~WofData() = default;
    // Reads exactly count bytes, or returns false when the data ends first.
    virtual bool readExact(void* out, std::size_t count) = 0;
};

// The problem with a file Windows compacted, as Linux sees it, or nothing when it's an ordinary file. Platform.
std::optional<std::string> wofProblem(const std::filesystem::path& path);

// The algorithm a compacted file uses: 0 XPRESS 4K, 1 LZX, 2 XPRESS 8K, 3 XPRESS 16K. Nothing for other files. Platform.
std::optional<std::uint32_t> wofAlgorithm(const std::filesystem::path& path);

// Opens the compressed data of a compacted file. It may ask for the user's password. Throws std::runtime_error with the reason. Platform.
std::unique_ptr<WofData> openWofData(const std::filesystem::path& path);

// The free space on the volume that holds folder. Throws std::runtime_error when it can't be read. Platform.
std::uint64_t freeBytesAt(const std::filesystem::path& folder);

// Decompresses a compacted file in place. The compressed original moves into a "compressed" folder beside it, and the plain copy
// takes its name. Throws std::runtime_error with the reason, or with "Cancelled." when cancel is set.
void resolveWof(const std::filesystem::path& path, const ProgressReport& report, const std::atomic<bool>& cancel);

}  // namespace gwl
