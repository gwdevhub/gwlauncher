// A file Windows compacted, which Linux can't read. The fix decompresses it in place, behind a progress window.

#include <atomic>
#include <filesystem>
#include <memory>
#include <optional>
#include <string>
#include <vector>

#include "Issue.h"
#include "Launch.h"
#include "ModFiles.h"
#include "Progress.h"
#include "Wof.h"

namespace gwl {

namespace {

class WofIssue : public Issue {
public:
    WofIssue(std::string fileName, std::string problem) : fileName_(std::move(fileName)), problem_(std::move(problem)) {}

    int severity() const override { return kBlocking; }
    std::string message() const override { return problem_; }
    void describe(Json& json) const override { json["wofPath"] = fileName_; }

    void fix(Reply reply, const JobRunner& runJob) override {
        const auto path = gameFilePath(fileName_);
        if (!path) {
            reply.result(nullptr);
            return;
        }
        const Json popup{
            {"title", "Decompressing"},
            {"heading", "Decompressing a file Windows compacted"},
            {"note", "Asks for your password to read the disk. The file is replaced by its decompressed copy."},
            {"cancellable", true},
            {"failure", "The file couldn't be decompressed"},
        };
        const std::string fileName = fileName_;
        runJob(
            popup,
            [path = *path](const ProgressReport& report, const std::atomic<bool>& cancel) { resolveWof(path, report, cancel); },
            [reply, fileName](const std::optional<std::string>& failure) {
                reply.result(failure ? Json(nullptr)
                                     : Json("Decompressed " + fileName + ". It replaces the compressed original."));
            });
    }

private:
    std::string fileName_;
    std::string problem_;
};

}  // namespace

std::vector<std::unique_ptr<Issue>> checkWofProblems(const Account& account) {
    std::vector<std::unique_ptr<Issue>> found;
    if (const auto gwExe = gameFilePath(account.gwpath)) {
        if (const auto problem = wofProblem(*gwExe)) {
            found.push_back(std::make_unique<WofIssue>(account.gwpath, *problem));
        }
    }
    for (const Mod& mod : account.mods) {
        if (!mod.active) {
            continue;
        }
        if (const auto file = gameFilePath(mod.fileName)) {
            if (const auto problem = wofProblem(*file)) {
                found.push_back(std::make_unique<WofIssue>(mod.fileName, *problem));
            }
        }
    }
    // The plugin folders' files load too, so a compacted one there is reported as well. Every entry is checked, since the compacted
    // ones don't read as regular files on Linux.
    if (account.usePluginFolderMods) {
        for (const std::filesystem::path& folder : pluginFolders(gameFilePath(account.gwpath), std::filesystem::current_path())) {
            std::error_code error;
            for (const auto& entry : std::filesystem::directory_iterator(folder, error)) {
                if (const auto problem = wofProblem(entry.path())) {
                    found.push_back(std::make_unique<WofIssue>(entry.path().string(), *problem));
                }
            }
        }
    }
    return found;
}

}  // namespace gwl
