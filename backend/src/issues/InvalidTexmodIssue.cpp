// An active texmod's file isn't a .tpf or .zip, the only kinds gMod loads. Gw.exe can't use it, so it blocks the launch.
// Its extension is checked as the Rust launcher checks it; the contents aren't read.

#include <algorithm>
#include <cctype>
#include <filesystem>

#include "Issue.h"
#include "utils/Files.h"
#include "Launch.h"

namespace gwl {

namespace {

class InvalidTexmodIssue : public Issue {
public:
    explicit InvalidTexmodIssue(std::string fileName) : name_(pathToUtf8(pathFromUtf8(fileName).filename())) {}

    int severity() const override { return kBlocking; }
    std::string message() const override { return name_ + " isn't a .tpf or .zip texmod, so gMod can't load it."; }

private:
    std::string name_;
};

bool isTexmodExtension(const std::string& fileName) {
    const std::string extension = gwl::lowerExtension(pathFromUtf8(fileName));
    return extension == ".tpf" || extension == ".zip";
}

}  // namespace

std::vector<std::unique_ptr<Issue>> checkTexmodsInvalid(const Account& account) {
    std::vector<std::unique_ptr<Issue>> invalid;
    for (const Mod& mod : account.mods) {
        if (!mod.active || mod.kind != ModType::Texmod) {
            continue;
        }
        const auto modFile = linuxPathFor(mod.fileName);
        if (!modFile || !std::filesystem::is_regular_file(pathFromUtf8(*modFile))) {
            continue;  // A missing file is reported by ModMissingIssue.
        }
        if (!isTexmodExtension(mod.fileName)) {
            invalid.push_back(std::make_unique<InvalidTexmodIssue>(mod.fileName));
        }
    }
    return invalid;
}

}  // namespace gwl
