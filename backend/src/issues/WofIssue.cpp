// A file Windows compacted, which Linux can't read. The fix decompresses it in place, behind a progress window.

#include <algorithm>
#include <atomic>
#include <filesystem>
#include <memory>
#include <optional>
#include <string>
#include <thread>
#include <vector>

#include "Issue.h"
#include "Launch.h"
#include "ModFiles.h"
#include "Progress.h"
#include "Wof.h"
#include "utils/Files.h"

namespace gwl {

namespace {

// The DLLs Gw.exe imports or names as strings, which it may load from its own folder. Windows searches that folder before
// the system folders, so a compacted copy there is what the game would load.
constexpr const char* kGwDependencies[] = {
    "KERNEL32.DLL", "USER32.DLL",   "ADVAPI32.DLL", "WS2_32.DLL",  "WINMM.DLL",  "WINHTTP.DLL", "SHELL32.DLL",
    "COMCTL32.DLL", "DBGHELP.DLL",  "VERSION.DLL",  "OLEAUT32.DLL", "GDI32.DLL", "OLE32.DLL",   "MSACM32.DLL",
    "SECUR32.DLL",  "steam_api.dll", "libEGL.dll",  "libGLESv2.dll", "OpenAL32.dll",
    "avrt.dll",     "d3d8.dll",     "d3d9.dll",     "ddraw.dll",   "dsound.dll", "dsound3d.dll", "dxgi.dll",
    "gdiplus.dll",  "ImageHlp.dll", "imm32.dll",    "PathEngine.dll", "Shcore.dll", "shfolder.dll",
};

// Checks run this many at a time, so a long list of files doesn't start a thread for each one.
constexpr std::size_t kChecksAtOnce = 10;

struct Candidate {
    std::string fileName;
    std::filesystem::path path;
};

// The problem with each candidate, in order. Own threads, not the shared pool: this check runs on a pool worker, and waiting on
// the pool from there can deadlock.
std::vector<std::optional<std::string>> checkEach(const std::vector<Candidate>& candidates) {
    std::vector<std::optional<std::string>> problems(candidates.size());
    std::atomic<std::size_t> next{0};
    const auto worker = [&] {
        for (std::size_t i = next++; i < candidates.size(); i = next++) {
            problems[i] = wofProblem(candidates[i].path);
        }
    };
    std::vector<std::thread> threads;
    const std::size_t count = std::min(kChecksAtOnce, candidates.size());
    for (std::size_t t = 0; t < count; ++t) {
        threads.emplace_back(worker);
    }
    for (std::thread& thread : threads) {
        thread.join();
    }
    return problems;
}

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
    // Windows reads its own compacted files, so there's nothing to check.
    if (!launchesThroughWine()) {
        return found;
    }
    std::vector<Candidate> candidates;
    if (const auto gwExe = gameFilePath(account.gwpath)) {
        candidates.push_back({account.gwpath, *gwExe});
        for (const char* name : kGwDependencies) {
            const auto dependency = gwExe->parent_path() / name;
            candidates.push_back({pathToUtf8(dependency), dependency});
        }
    }
    for (const Mod& mod : account.mods) {
        if (!mod.active) {
            continue;
        }
        if (const auto file = gameFilePath(mod.fileName)) {
            candidates.push_back({mod.fileName, *file});
        }
    }
    // The plugin folders' files load too, so a compacted one there is reported as well. Every entry is checked, since the compacted
    // ones don't read as regular files on Linux.
    if (account.usePluginFolderMods) {
        for (const std::filesystem::path& folder : pluginFolders(gameFilePath(account.gwpath), std::filesystem::current_path())) {
            std::error_code error;
            for (const auto& entry : std::filesystem::directory_iterator(folder, error)) {
                candidates.push_back({pathToUtf8(entry.path()), entry.path()});
            }
        }
    }
    const auto problems = checkEach(candidates);
    for (std::size_t i = 0; i < candidates.size(); ++i) {
        if (problems[i]) {
            found.push_back(std::make_unique<WofIssue>(candidates[i].fileName, *problems[i]));
        }
    }
    return found;
}

}  // namespace gwl
