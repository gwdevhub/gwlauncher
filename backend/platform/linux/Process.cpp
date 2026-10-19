// Linux: running programs with posix_spawn, with nothing on stdin, stdout or stderr.

#include "Process.h"

#include <fcntl.h>
#include <spawn.h>
#include <sys/wait.h>
#include <unistd.h>

#include <thread>

extern char** environ;

namespace gwl {

namespace {

pid_t spawnQuiet(const std::vector<std::string>& argv) {
    std::vector<char*> args;
    for (const auto& arg : argv) {
        args.push_back(const_cast<char*>(arg.c_str()));
    }
    args.push_back(nullptr);
    posix_spawn_file_actions_t actions;
    posix_spawn_file_actions_init(&actions);
    posix_spawn_file_actions_addopen(&actions, 0, "/dev/null", O_RDONLY, 0);
    posix_spawn_file_actions_addopen(&actions, 1, "/dev/null", O_WRONLY, 0);
    posix_spawn_file_actions_addopen(&actions, 2, "/dev/null", O_WRONLY, 0);
    pid_t pid = 0;
    const int status = posix_spawnp(&pid, argv[0].c_str(), &actions, nullptr, args.data(), environ);
    posix_spawn_file_actions_destroy(&actions);
    return status == 0 ? pid : -1;
}

}  // namespace

int runProgram(const std::vector<std::string>& argv) {
    const pid_t pid = spawnQuiet(argv);
    if (pid < 0) {
        return -1;
    }
    int status = 0;
    waitpid(pid, &status, 0);
    return WIFEXITED(status) ? WEXITSTATUS(status) : -1;
}

void runProgramDetached(const std::vector<std::string>& argv) {
    const pid_t pid = spawnQuiet(argv);
    if (pid > 0) {
        std::thread([pid] {
            int status = 0;
            waitpid(pid, &status, 0);
        }).detach();
    }
}

}  // namespace gwl
