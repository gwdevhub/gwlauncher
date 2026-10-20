#include <cstring>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <string>
#include <system_error>

#include "EmbeddedCli.h"
#include "Launch.h"
#include "utils/Files.h"

namespace gwl {

namespace fs = std::filesystem;

// The GUI launchers define the real bytes. A build without them, such as the tests, has none, and the copy never runs there.
__attribute__((weak)) std::pair<const unsigned char*, std::size_t> embeddedCli() {
    return {nullptr, 0};
}

namespace {

const char* const kCliName = "GW_Launcher.cli.exe";

bool sameAsEmbedded(const fs::path& path, const std::pair<const unsigned char*, std::size_t>& bytes) {
    std::error_code error;
    if (!fs::is_regular_file(path, error) || fs::file_size(path, error) != bytes.second) {
        return false;
    }
    std::ifstream file(path, std::ios::binary);
    std::string text((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    return text.size() == bytes.second && std::memcmp(text.data(), bytes.first, bytes.second) == 0;
}

}  // namespace

fs::path installEmbeddedCli() {
    const auto bytes = embeddedCli();
    if (bytes.second == 0) {
        throw std::runtime_error("This build carries no command-line launcher, so a game can't be started.");
    }
    const fs::path folder = launcherDataDir();
    const fs::path target = folder / kCliName;
    std::error_code error;
    fs::create_directories(folder, error);
    if (sameAsEmbedded(target, bytes)) {
        return target;
    }

    // It's written beside the copy and then moved over it, so a copy that's cut short is never run.
    const fs::path staged = folder / (std::string(kCliName) + ".new");
    {
        std::ofstream file(staged, std::ios::binary | std::ios::trunc);
        file.write(reinterpret_cast<const char*>(bytes.first), static_cast<std::streamsize>(bytes.second));
        if (!file) {
            throw std::runtime_error("The command-line launcher couldn't be written to " + pathToUtf8(staged));
        }
    }
    fs::rename(staged, target, error);
    if (error) {
        fs::remove(staged, error);
        throw std::runtime_error("The command-line launcher is in use, so it can't be replaced: " + pathToUtf8(target));
    }
    return target;
}

}  // namespace gwl
