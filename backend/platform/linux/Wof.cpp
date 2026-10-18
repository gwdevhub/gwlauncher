// Linux: the NTFS reparse data is read through the xattr ntfs-3g exposes, and the compressed data through ntfsinfo and ntfscat
// as root, through pkexec. The volume is found from the mount table.

#include <fcntl.h>
#include <signal.h>
#include <spawn.h>
#include <sys/stat.h>
#include <sys/statvfs.h>
#include <sys/sysmacros.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <sys/xattr.h>
#include <unistd.h>

#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#include "Wof.h"
#include "utils/Bytes.h"

extern char** environ;

namespace gwl {

namespace fs = std::filesystem;

namespace {

const std::uint32_t kWofTag = 0x80000017;
const char* const kStreamMarker = "@@WOFSTREAM@@";

// The NTFS reparse data of the file, or nothing when it has none.
std::optional<Bytes> reparseData(const fs::path& path) {
    Bytes buffer(64, 0);
    const ssize_t length = lgetxattr(path.c_str(), "system.ntfs_reparse_data", buffer.data(), buffer.size());
    if (length < 4) {
        return std::nullopt;
    }
    buffer.resize(static_cast<std::size_t>(length));
    return buffer;
}

std::string quoted(const std::string& value) {
    std::string out = "'";
    for (char c : value) {
        if (c == '\'') {
            out += "'\\''";
        } else {
            out += c;
        }
    }
    return out + "'";
}

// The path without its trailing slashes, as Rust's trim_end_matches('/') gives it.
std::string trimmedSlashes(std::string path) {
    while (!path.empty() && path.back() == '/') {
        path.pop_back();
    }
    return path;
}

// The volume a path is on, and the path inside it, from the mount table. ntfsinfo wants the path relative to the root.
std::pair<std::string, std::string> volumeFor(const fs::path& path) {
    std::error_code error;
    const std::string full = fs::absolute(path, error).string();
    std::ifstream mounts("/proc/mounts");
    if (!mounts) {
        throw std::runtime_error("The mount table couldn't be read.");
    }
    std::string best;
    std::string device;
    std::string line;
    while (std::getline(mounts, line)) {
        std::vector<std::string> fields;
        std::size_t start = 0;
        for (std::size_t i = 0; i <= line.size(); ++i) {
            if (i == line.size() || line[i] == ' ') {
                fields.push_back(line.substr(start, i - start));
                start = i + 1;
            }
        }
        if (fields.size() < 3) {
            continue;
        }
        std::string mount;
        for (std::size_t i = 0; i < fields[1].size(); ++i) {
            if (fields[1].compare(i, 4, "\\040") == 0) {
                mount += ' ';
                i += 3;
            } else {
                mount += fields[1][i];
            }
        }
        const std::string trimmed = trimmedSlashes(mount);
        const bool inside = full == mount || full.rfind(trimmed + "/", 0) == 0;
        if (inside && (best.empty() || mount.size() > best.size())) {
            best = mount;
            device = fields[0];
        }
    }
    if (best.empty()) {
        throw std::runtime_error("No mounted volume contains " + path.string() + ".");
    }
    std::string relative = full.substr(trimmedSlashes(best).size());
    while (!relative.empty() && relative.front() == '/') {
        relative.erase(0, 1);
    }
    return {device, relative};
}

// The compressed data as root, read from the pkexec helper's output. Destroying it stops the helper.
class PrivilegedData : public WofData {
public:
    PrivilegedData(pid_t pid, int fd) : pid_(pid), fd_(fd) {}

    ~PrivilegedData() override {
        ::kill(pid_, SIGKILL);
        int status = 0;
        waitpid(pid_, &status, 0);
        close(fd_);
    }

    bool readExact(void* out, std::size_t count) override {
        auto* bytes = static_cast<std::uint8_t*>(out);
        std::size_t got = 0;
        while (got < count) {
            const ssize_t read_count = ::read(fd_, bytes + got, count - got);
            if (read_count <= 0) {
                return false;
            }
            got += static_cast<std::size_t>(read_count);
        }
        return true;
    }

private:
    pid_t pid_;
    int fd_;
};

}  // namespace

std::optional<std::string> wofProblem(const fs::path& path) {
    const auto data = reparseData(path);
    if (!data || data->size() < 4 || readU32LE(*data, 0) != kWofTag) {
        return std::nullopt;
    }
    return "compressed by Windows (WOF), so Linux can't read it: " + path.string();
}

std::optional<std::uint32_t> wofAlgorithm(const fs::path& path) {
    const auto data = reparseData(path);
    if (!data || data->size() < 24 || readU32LE(*data, 0) != kWofTag) {
        return std::nullopt;
    }
    return readU32LE(*data, 20);
}

// Whether a program is on PATH. Checked before a read, so a missing tool is named rather than reported as a failed read.
bool onPath(const std::string& name) {
    const char* pathVariable = std::getenv("PATH");
    std::istringstream directories(pathVariable ? pathVariable : "");
    std::string directory;
    while (std::getline(directories, directory, ':')) {
        if (access((fs::path(directory) / name).c_str(), X_OK) == 0) {
            return true;
        }
    }
    return false;
}

std::unique_ptr<WofData> openWofData(const fs::path& path) {
    if (!onPath("ntfsinfo") || !onPath("ntfscat")) {
        throw std::runtime_error("The ntfs-3g tools (ntfsinfo and ntfscat) aren't installed, so compressed files can't be read. Install the ntfs-3g package, then try again.");
    }
    if (!onPath("pkexec")) {
        throw std::runtime_error("pkexec isn't installed, so compressed files can't be read as root. Install polkit, which provides pkexec, then try again.");
    }
    const auto [device, relative] = volumeFor(path);
    const std::string script =
        "INFO=$(ntfsinfo -f -F " + quoted(relative) + " " + quoted(device) + " 2>/dev/null); "
        "printf '%s\\n" + std::string(kStreamMarker) + "\\n' \"$INFO\"; "
        "INO=$(printf '%s\\n' \"$INFO\" | sed -n 's/.*Dumping Inode \\([0-9]*\\).*/\\1/p' | head -1); "
        "exec ntfscat -f -q -i \"$INO\" -n WofCompressedData -a 0x80 " + quoted(device);

    int pipeFds[2] = {-1, -1};
    if (pipe(pipeFds) != 0) {
        throw std::runtime_error("The privileged read couldn't be started.");
    }
    posix_spawn_file_actions_t actions;
    posix_spawn_file_actions_init(&actions);
    posix_spawn_file_actions_adddup2(&actions, pipeFds[1], 1);
    posix_spawn_file_actions_addopen(&actions, 0, "/dev/null", O_RDONLY, 0);
    posix_spawn_file_actions_addopen(&actions, 2, "/dev/null", O_WRONLY, 0);
    std::vector<char*> argv{const_cast<char*>("pkexec"), const_cast<char*>("sh"), const_cast<char*>("-c"),
                            const_cast<char*>(script.c_str()), nullptr};
    pid_t pid = 0;
    const int status = posix_spawnp(&pid, "pkexec", &actions, nullptr, argv.data(), environ);
    posix_spawn_file_actions_destroy(&actions);
    close(pipeFds[1]);
    if (status != 0) {
        close(pipeFds[0]);
        throw std::runtime_error(std::string("pkexec couldn't be started (") + std::strerror(status) + "). Install polkit, which provides pkexec, then try again.");
    }
    return std::make_unique<PrivilegedData>(pid, pipeFds[0]);
}

std::uint64_t freeBytesAt(const fs::path& folder) {
    struct statvfs stats {};
    if (statvfs(folder.c_str(), &stats) != 0) {
        throw std::runtime_error("Could not read the free space.");
    }
    return static_cast<std::uint64_t>(stats.f_bavail) * static_cast<std::uint64_t>(stats.f_frsize);
}

}  // namespace gwl
