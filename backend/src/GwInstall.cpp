#include "GwInstall.h"

#include <cstdint>
#include <fstream>
#include <iterator>
#include <set>
#include <stdexcept>
#include <system_error>

#include "GwHuffman.h"
#include "GwNet.h"
#include "GwPe.h"
#include "utils/Files.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

const char* const kExeName = "Gw.exe";

// Whether the executable is the latest build, by the CRC ArenaNet sends for it. Any change to the bytes or the build changes it.
bool matchesLatest(const std::vector<std::uint8_t>& data, const GwFileInfo& latest) {
    return gwFileCrc(data, latest.fileId) == static_cast<std::uint32_t>(latest.crc);
}

// The folder the download is kept in, beside the launcher's data.
fs::path downloadFolder() {
    return fs::current_path() / "GwTemp";
}

// Whether the path needs the latest Gw.exe copied to it: any file that isn't that build, or a path with nothing there.
bool needsLatest(const fs::path& path, const GwFileInfo& latest) {
    std::error_code error;
    if (!fs::is_regular_file(path, error)) {
        return true;
    }
    return !matchesLatest(readFileBytes(path).value_or(Bytes()), latest);
}

}  // namespace

fs::path installGw(const fs::path& folder, const GwReport& report, const std::atomic<bool>& cancel) {
    std::error_code error;
    fs::create_directories(folder, error);
    if (error) {
        throw std::runtime_error("Could not create " + pathToUtf8(folder) + ": " + error.message());
    }
    const fs::path exe = folder / kExeName;
    // One connection gives the latest details, and the download, when it's needed, goes over the same connection.
    GwOpenDownload open = openLatestGw();
    const GwFileInfo latest = open.info;

    std::error_code exists;
    if (fs::is_regular_file(exe, exists)) {
        const std::vector<std::uint8_t> existing = readFileBytes(exe).value_or(Bytes());
        if (matchesLatest(existing, latest)) {
            report("Exe already downloaded", 1.0);
            return exe;
        }
    }

    const GwDownload download = downloadOpened(open, cancel, [&](std::size_t received, std::size_t total) {
        report("Downloading compressed executable", static_cast<double>(received) / static_cast<double>(total ? total : 1));
    });
    report("Downloaded compressed executable", 1.0);

    report("Decompressing downloaded executable", 0.0);
    const std::vector<std::uint8_t> data = decompressGw(
        download.compressed, download.decompressedSize,
        [&](double fraction) { report("Decompressing downloaded executable", fraction); }, cancel);

    if (!writeFileBytes(exe, data)) {
        throw std::runtime_error("Could not write " + pathToUtf8(exe));
    }
    report("Decompressed downloaded executable", 1.0);
    return exe;
}

bool gwIsOutdated(const fs::path& path, const GwFileInfo& latest) {
    std::error_code error;
    if (!fs::is_regular_file(path, error)) {
        return false;
    }
    const std::vector<std::uint8_t> data = readFileBytes(path).value_or(Bytes());
    return isGwExecutable(data) && !matchesLatest(data, latest);
}

std::size_t updateGwClients(const std::vector<fs::path>& paths, const GwReport& report, const std::atomic<bool>& cancel) {
    const std::set<fs::path> unique(paths.begin(), paths.end());
    if (unique.empty()) {
        return 0;
    }
    // The update is the time the latest version is wanted fresh, so the cached answer is dropped before it's asked for again.
    clearCachedLatestGwInfo();
    // The file info carries the latest CRC, so each path is checked before anything is downloaded.
    const GwFileInfo latest = latestGwInfo();
    std::vector<fs::path> stale;
    for (const fs::path& path : unique) {
        if (needsLatest(path, latest)) {
            stale.push_back(path);
        }
    }
    if (stale.empty()) {
        report("Every Gw.exe is up to date", 1.0);
        return 0;
    }
    const fs::path source = installGw(downloadFolder(), report, cancel);

    const double total = static_cast<double>(stale.size());
    std::size_t done = 0;
    for (const fs::path& path : stale) {
        report("Copying Gw.exe to client paths", static_cast<double>(done) / total);
        std::error_code error;
        fs::copy_file(source, path, fs::copy_options::overwrite_existing, error);
        if (error) {
            throw std::runtime_error("Could not copy Gw.exe to " + pathToUtf8(path) + ": " + error.message());
        }
        done += 1;
    }
    report("Update completed", 1.0);
    return stale.size();
}

}  // namespace gwl
