// The Steam step of a Steam account's launch: Steam is started and logged in before the game is.

#include <atomic>
#include <chrono>
#include <cstdint>
#include <functional>
#include <optional>
#include <stdexcept>
#include <string>
#include <thread>

#include "Launch.h"
#include "Progress.h"

namespace gwl {

namespace {

// Waits up to 30 seconds for ready(). The window's line shows the seconds waited, and is only sent when that count changes.
void waitForSteam(const std::string& stage, const std::function<bool()>& ready, const ProgressReport& report,
                  const std::atomic<bool>& cancel, const std::string& timeoutMessage) {
    const auto start = std::chrono::steady_clock::now();
    std::optional<long long> shown;
    while (!ready()) {
        if (cancel.load()) {
            throw std::runtime_error(kCancelledMessage);
        }
        const long long waited = std::chrono::duration_cast<std::chrono::seconds>(std::chrono::steady_clock::now() - start).count();
        if (waited >= 30) {
            throw std::runtime_error(timeoutMessage);
        }
        if (waited != shown) {
            shown = waited;
            report(stage + ": " + std::to_string(waited) + " s", std::nullopt);
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(250));
    }
}

// Calls the method at index in an interface's table. Steamworks interfaces are C++ objects with a vtable at their start.
template <typename R, typename... A>
R callSteamAt(void* object, int index, A... args) {
    void** table = *reinterpret_cast<void***>(object);
    return reinterpret_cast<R (*)(void*, A...)>(table[index])(object, args...);
}

}  // namespace

bool steamLoggedInOn(void* client) {
    if (!client) {
        return false;
    }
    // ISteamClient: 0 CreateSteamPipe, 1 BReleaseSteamPipe, 2 ConnectToGlobalUser, 5 GetISteamUser.
    // ISteamUser: 1 BLoggedOn.
    const int32_t pipe = callSteamAt<int32_t>(client, 0);
    if (pipe <= 0) {
        return false;
    }
    bool loggedOn = false;
    const int32_t user = callSteamAt<int32_t>(client, 2, pipe);
    if (user > 0) {
        void* steamUser = callSteamAt<void*>(client, 5, user, pipe, "SteamUser023");
        if (steamUser) {
            loggedOn = callSteamAt<bool>(steamUser, 1);
        }
    }
    callSteamAt<bool>(client, 1, pipe);
    return loggedOn;
}

void startSteamForLaunch(const ProgressReport& report, const std::atomic<bool>& cancel) {
    if (!steamReady()) {
        report("Starting Steam...", std::nullopt);
        startSteamClient();
        waitForSteam("Waiting for Steam to open", steamReady, report, cancel, "Steam didn't open within 30 seconds.");
    }
    waitForSteam("Waiting for Steam to log in", steamLoggedIn, report, cancel, "Steam didn't log in within 30 seconds.");
    report("Steam is logged in.", std::nullopt);
}

}  // namespace gwl
