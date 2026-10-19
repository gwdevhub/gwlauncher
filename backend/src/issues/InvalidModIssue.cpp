// An active DLL mod's file exists but isn't a 32-bit Windows DLL, so the game can't load it. Launching would only fail
// in the game, so it blocks the launch. Texmods aren't DLLs and aren't checked here.

#include <filesystem>

#include "GMod.h"
#include "Issue.h"
#include "Launch.h"

namespace gwl {

namespace {

class InvalidModIssue : public Issue {
public:
    explicit InvalidModIssue(std::string fileName) : name_(std::filesystem::path(fileName).filename().string()) {}

    int severity() const override { return kBlocking; }
    std::string message() const override { return name_ + " isn't a 32-bit Windows DLL, so the game can't load it."; }

private:
    std::string name_;
};

}  // namespace

std::vector<std::unique_ptr<Issue>> checkModsInvalid(const Account& account) {
    std::vector<std::unique_ptr<Issue>> invalid;
    for (const Mod& mod : account.mods) {
        if (!mod.active || mod.kind != ModType::Dll) {
            continue;
        }
        const auto modFile = linuxPathFor(mod.fileName);
        if (!modFile || !std::filesystem::is_regular_file(*modFile)) {
            continue;  // A missing file is reported by ModMissingIssue.
        }
        if (!isUsableDll(*modFile)) {
            invalid.push_back(std::make_unique<InvalidModIssue>(mod.fileName));
        }
    }
    return invalid;
}

}  // namespace gwl
