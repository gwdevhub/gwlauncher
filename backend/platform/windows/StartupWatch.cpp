// Windows: the startup watchdog. The watchdog runs on its own thread, since a message pump that startup might block can't be relied on.

#include <windows.h>

#include <atomic>
#include <chrono>
#include <string>
#include <thread>

#include "StartupWatch.h"
#include "WebViewWindow.h"

namespace gwl {
namespace {

std::atomic<const wchar_t*> startupStage{L"initializing Windows windowing and the WebView2 loader"};
std::atomic<bool> startupFinished{false};
std::atomic<unsigned> startupDialogs{0};

}  // namespace

void startStartupMonitoring() {
    setDialogObserver([](bool shown) { pauseStartupMonitoring(shown); });
    std::thread([] {
        unsigned seconds = 0;
        while (seconds < 30) {
            std::this_thread::sleep_for(std::chrono::seconds(1));
            if (startupFinished.load()) return;
            seconds = startupDialogs.load() ? 0 : seconds + 1;
        }
        const std::wstring message = std::wstring(L"GW Launcher startup has not completed within 30 seconds. It is currently ") +
                                     startupStage.load() +
                                     L". Startup may be blocked by inaccessible files or security software.\n\nPress OK to exit, check the launcher folder, and restart. Press Ctrl+C to copy this error.";
        MessageBoxW(nullptr, message.c_str(), L"GW Launcher startup timed out", MB_OK | MB_ICONERROR);
        ExitProcess(1);
    }).detach();
}

void monitorConfigurationStartup() {
    startupStage = L"reading launcher settings and Accounts.json";
}

void finishStartupMonitoring() {
    startupFinished = true;
}

void pauseStartupMonitoring(bool paused) {
    if (paused) {
        ++startupDialogs;
    } else {
        --startupDialogs;
    }
}

}  // namespace gwl
