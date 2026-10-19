#include "Wof.h"

#include <stdexcept>

#include "WofDecode.h"

namespace gwl {

namespace fs = std::filesystem;

void resolveWof(const fs::path& path, const ProgressReport& report, const std::atomic<bool>& cancel) {
    const auto algorithm = wofAlgorithm(path);
    if (!algorithm) {
        throw std::runtime_error(path.string() + " is not a Windows-compacted file.");
    }
    wof::chunkSize(*algorithm);

    const fs::path folder = path.parent_path().empty() ? fs::path(".") : path.parent_path();
    std::error_code error;
    const fs::path work = folder / (path.filename().string() + ".decompressing");

    {
        // The reader is closed before the files move, so the helper that read the disk has finished.
        report("Waiting for permission to read the disk...", std::nullopt);
        auto data = openWofData(path);
        report("Decompressing...", 0.0);
        try {
            wof::decodeStream([&data](void* out, std::size_t count) { return data->readExact(out, count); }, *algorithm, work,
                              freeBytesAt(folder), report, cancel);
        } catch (...) {
            fs::remove(work, error);
            throw;
        }
    }

    if (cancel.load()) {
        fs::remove(work, error);
        throw std::runtime_error(kCancelledMessage);
    }
    // The decompressed file replaces the original, which isn't kept.
    report("Replacing the original...", std::nullopt);
    fs::rename(work, path, error);
    if (error) {
        fs::remove(work, error);
        throw std::runtime_error("Could not put the plain file in place: " + error.message());
    }
    report("Done.", 1.0);
}

}  // namespace gwl
