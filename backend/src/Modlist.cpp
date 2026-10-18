#include "Modlist.h"

#include <fstream>
#include <iterator>
#include <stdexcept>
#include <system_error>

namespace gwl {

namespace fs = std::filesystem;

namespace {

const char* const kModlist = "modlist.txt";

std::optional<std::string> readText(const fs::path& path) {
    std::ifstream file(path, std::ios::binary);
    if (!file) {
        return std::nullopt;
    }
    return std::string((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
}

bool writeText(const fs::path& path, const std::string& text) {
    std::ofstream file(path, std::ios::binary | std::ios::trunc);
    file << text;
    return static_cast<bool>(file);
}

// Blanks the game folder's list so gMod doesn't read it too. Failures are ignored, as in the C# launcher.
std::optional<std::pair<fs::path, std::string>> blankGameList(const fs::path& gameFile, const fs::path& gmodFile) {
    if (gameFile == gmodFile) {
        return std::nullopt;
    }
    auto original = readText(gameFile);
    if (!original || !writeText(gameFile, "")) {
        return std::nullopt;
    }
    return std::make_pair(gameFile, *original);
}

}  // namespace

std::unique_ptr<ModlistGuard> ModlistGuard::prepare(const std::vector<fs::path>& texmods, const std::optional<fs::path>& gmodDll,
                                                    const fs::path& gameExe) {
    if (!gmodDll || texmods.empty()) {
        return nullptr;
    }
    const fs::path gmodFile = gmodDll->parent_path() / kModlist;
    const fs::path gameFile = gameExe.parent_path() / kModlist;

    std::optional<std::string> gmodOriginal;
    std::error_code error;
    if (fs::exists(gmodFile, error)) {
        gmodOriginal = readText(gmodFile);
        if (!gmodOriginal) {
            throw std::runtime_error("Failed to read " + gmodFile.string());
        }
    }

    std::string list;
    for (std::size_t i = 0; i < texmods.size(); ++i) {
        if (i > 0) {
            list += "\n";
        }
        list += texmods[i].string();
    }
    if (!writeText(gmodFile, list)) {
        throw std::runtime_error("Failed to write texmods to " + gmodFile.string());
    }

    auto gameOriginal = blankGameList(gameFile, gmodFile);
    return std::unique_ptr<ModlistGuard>(new ModlistGuard(gmodFile, gmodOriginal, std::move(gameOriginal)));
}

ModlistGuard::ModlistGuard(fs::path gmodFile, std::optional<std::string> gmodOriginal,
                           std::optional<std::pair<fs::path, std::string>> gameFile)
    : gmodFile_(std::move(gmodFile)), gmodOriginal_(std::move(gmodOriginal)), gameFile_(std::move(gameFile)) {}

ModlistGuard::~ModlistGuard() {
    // With no original, the C# launcher leaves the file empty rather than removing it; that's kept here.
    std::error_code error;
    if (fs::exists(gmodFile_, error)) {
        writeText(gmodFile_, gmodOriginal_.value_or(""));
    }
    if (gameFile_) {
        writeText(gameFile_->first, gameFile_->second);
    }
}

}  // namespace gwl
