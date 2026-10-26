// Windows: a Win32 window with a WebView2 control. WebView2Loader.dll is loaded with LoadLibrary when the first window is
// made, not through the import table, so the program starts without it. The page's messages come from WebView2's
// message event; the page reads the host through window.chrome.webview.

#include <windows.h>
#include <objbase.h>

#include <WebView2.h>

#include <atomic>
#include <chrono>
#include <cstdio>
#include <cwchar>
#include <functional>
#include <memory>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>
#include <vector>

#include "WebViewWindow.h"
#include "WebView2.h"

#include <cstring>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <system_error>
#include "Launch.h"
#include "utils/Errors.h"
#include "utils/Files.h"
#include "utils/Strings.h"

namespace gwl {

namespace {

using CreateEnvironmentFn = HRESULT(STDMETHODCALLTYPE*)(PCWSTR, PCWSTR, ICoreWebView2EnvironmentOptions*,
                                                        ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler*);

const wchar_t* const kWindowClass = L"GwlWebViewWindow";
// Where the WebView2 runtime is downloaded from.
const char* const kWebView2Download = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
void initializeTaskDispatch();
std::atomic<const wchar_t*> startupStage{L"initializing Windows windowing and the WebView2 loader"};
std::atomic<bool> startupFinished{false};
std::atomic<unsigned> startupDialogs{0};

std::wstring toWide(const std::string& text) {
    if (text.empty()) return {};
    const int length = MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0);
    std::wstring result(length, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), result.data(), length);
    return result;
}

std::string toUtf8(const wchar_t* text) {
    if (!text) return {};
    return utf16ToUtf8(std::vector<std::uint16_t>(text, text + std::wcslen(text)));
}

template <typename F>
F loaderFunction(const char* name) {
    static HMODULE loader = LoadLibraryW((launcherDataDir() / "WebView2Loader.dll").c_str());
    if (!loader) {
        throw std::runtime_error("WebView2Loader.dll couldn't be loaded. Install the WebView2 runtime.");
    }
    FARPROC address = GetProcAddress(loader, name);
    if (!address) {
        throw std::runtime_error(std::string("WebView2Loader.dll is missing ") + name + ".");
    }
    return reinterpret_cast<F>(address);
}

}  // namespace

struct NativeWindow {
    std::shared_ptr<bool> alive = std::make_shared<bool>(true);
    WebViewWindow* owner = nullptr;
    HWND hwnd = nullptr;
    ICoreWebView2Controller* controller = nullptr;
    ICoreWebView2* webview = nullptr;
    EventRegistrationToken messageToken{};
    // Host events must wait for frontend subscriptions, but startup replies must not wait for readiness.
    std::vector<std::wstring> pendingScripts;
    // The window stays hidden until its page has loaded, so it never shows as an empty frame.
    bool pageLoaded = false;
    bool failed = false;
    std::string stage = "Creating the WebView2 environment";
    bool wantsVisible = false;
    bool wantsFocus = false;
    EventRegistrationToken navigationToken{};
    EventRegistrationToken processToken{};
};

namespace {

void failWebView(NativeWindow* native, const std::string& message) {
    if (native->failed) return;
    native->failed = true;
    native->owner->keepAliveForRecovery();
    KillTimer(native->hwnd, 1);
    const auto alive = std::weak_ptr<bool>(native->alive);
    runOnUiThread([native, alive, message] {
        if (alive.expired()) return;
        const std::wstring text = toWide(message + "\n\nRetry recreates the interface; running jobs are not restarted. Cancel exits GW Launcher.\nPress Ctrl+C to copy this error.");
        const int answer = MessageBoxW(native->hwnd, text.c_str(), L"GW Launcher interface error", MB_RETRYCANCEL | MB_ICONERROR);
        if (alive.expired()) return;
        if (answer == IDRETRY) {
            const auto error = captureError([&] { native->owner->reload(); }, "Recreating the launcher interface failed: ");
            if (!error.empty()) reportFatalError(error);
        } else {
            stopEventLoop();
        }
    });
}

bool checkWebView(NativeWindow* native, HRESULT result, const char* operation) {
    if (SUCCEEDED(result)) return true;
    char code[32];
    std::snprintf(code, sizeof(code), " (HRESULT 0x%08lX)", static_cast<unsigned long>(result));
    failWebView(native, std::string(operation) + code);
    return false;
}

void executeScript(NativeWindow* native, const std::wstring& script) {
    if (native->failed) return;
    const auto alive = std::weak_ptr<bool>(native->alive);
    auto done = webview2::scriptDone([native, alive](HRESULT result, LPCWSTR json) {
        if (alive.expired() || native->failed) return;
        if (!checkWebView(native, result, "Delivering a message to the launcher interface failed")) return;
        if (!json || std::wstring(json) != L"null") {
            failWebView(native, "The launcher interface could not process a host message: " + toUtf8(json ? json : L""));
        }
    });
    const std::wstring wrapped = L"(function(){try{" + script + L";return null;}catch(error){return String(error);}})()";
    checkWebView(native, native->webview->ExecuteScript(wrapped.c_str(), done.get()),
                 "Sending a message to the launcher interface failed");
}

void resizeWebView(NativeWindow* native) {
    if (!native->controller || !native->hwnd) {
        return;
    }
    RECT bounds{};
    GetClientRect(native->hwnd, &bounds);
    checkWebView(native, native->controller->put_Bounds(bounds), "Resizing the WebView2 interface failed");
}

void syncWebViewVisibility(NativeWindow* native) {
    if (native->controller && native->hwnd) {
        checkWebView(native, native->controller->put_IsVisible(IsWindowVisible(native->hwnd) && !IsIconic(native->hwnd)), "Changing WebView2 visibility failed");
    }
}

void sendPendingScripts(NativeWindow* native) {
    for (const std::wstring& script : native->pendingScripts) {
        executeScript(native, script);
    }
    native->pendingScripts.clear();
}

// Asks WebView2 to keep its memory use low. Runtimes without the interface are skipped.
void useLowMemoryTarget(ICoreWebView2* webview) {
    ICoreWebView2_19* webview19 = nullptr;
    if (SUCCEEDED(webview->QueryInterface(IID_ICoreWebView2_19, reinterpret_cast<void**>(&webview19)))) {
        webview19->put_MemoryUsageTargetLevel(COREWEBVIEW2_MEMORY_USAGE_TARGET_LEVEL_LOW);
        webview19->Release();
    }
}

// Shows the window once its page has loaded, if it's wanted yet. Focus goes to it only if it was asked for.
void showLoadedPage(NativeWindow* native) {
    native->pageLoaded = true;
    if (!native->hwnd || !native->wantsVisible) {
        return;
    }
    ShowWindow(native->hwnd, SW_SHOW);
    syncWebViewVisibility(native);
    if (native->failed) return;
    if (native->wantsFocus) {
        native->wantsFocus = false;
        SetForegroundWindow(native->hwnd);
    }
}

void attachWebView(NativeWindow* native, ICoreWebView2Controller* controller) {
    native->controller = controller;
    native->controller->AddRef();
    if (!checkWebView(native, native->controller->get_CoreWebView2(&native->webview), "Obtaining the WebView2 interface failed")) return;
    if (!native->webview) {
        failWebView(native, "WebView2 returned no browser interface.");
        return;
    }
    useLowMemoryTarget(native->webview);
    resizeWebView(native);
    syncWebViewVisibility(native);
    if (native->failed) return;

    const auto alive = std::weak_ptr<bool>(native->alive);
    auto navigation = webview2::navigationDone([native, alive](ICoreWebView2NavigationCompletedEventArgs* args) {
        if (alive.expired() || native->failed) return;
        BOOL success = FALSE;
        if (!checkWebView(native, args->get_IsSuccess(&success), "Reading the navigation result failed")) return;
        if (!success) {
            COREWEBVIEW2_WEB_ERROR_STATUS status{};
            args->get_WebErrorStatus(&status);
            failWebView(native, "Loading the launcher page failed (WebView2 web error " + std::to_string(static_cast<int>(status)) + ").");
        } else {
            native->stage = "Waiting for the launcher interface to initialize";
        }
    });
    if (!checkWebView(native, native->webview->add_NavigationCompleted(navigation.get(), &native->navigationToken),
                      "Registering navigation notifications failed")) return;

    auto process = webview2::processFailed([native, alive](ICoreWebView2ProcessFailedEventArgs* args) {
        if (alive.expired() || native->failed) return;
        COREWEBVIEW2_PROCESS_FAILED_KIND kind{};
        if (!checkWebView(native, args->get_ProcessFailedKind(&kind), "Reading the WebView2 process failure failed")) return;
        failWebView(native, "A WebView2 process failed or became unresponsive (failure kind " + std::to_string(static_cast<int>(kind)) + "). The launcher interface must be recreated.");
    });
    if (!checkWebView(native, native->webview->add_ProcessFailed(process.get(), &native->processToken),
                      "Registering WebView2 process notifications failed")) return;

    auto messages = webview2::messageReceived([native, alive](ICoreWebView2WebMessageReceivedEventArgs* args) {
        if (alive.expired() || native->failed) return;
        LPWSTR message = nullptr;
        const HRESULT result = args->TryGetWebMessageAsString(&message);
        if (checkWebView(native, result, "Reading a launcher message failed") && message) {
            const auto text = toUtf8(message);
            CoTaskMemFree(message);
            const auto error = captureError([&] { native->owner->receive(text); }, "Processing a launcher message failed: ");
            if (!error.empty()) failWebView(native, error);
        }
    });
    checkWebView(native, native->webview->add_WebMessageReceived(messages.get(), &native->messageToken),
                 "Registering launcher messages failed");
}

LRESULT CALLBACK windowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
    if (message == WM_CREATE) {
        auto* created = reinterpret_cast<CREATESTRUCTW*>(lParam);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(created->lpCreateParams));
        return 0;
    }
    // Every window is frameless. These come before WM_CREATE too, so they don't wait for the window's NativeWindow.
    switch (message) {
        case WM_NCCALCSIZE:
            // The client area is the whole window: a resizable window keeps its sizing frame for the size loop, but it takes no room.
            if (wParam) {
                return 0;
            }
            break;
        case WM_NCACTIVATE:
            // Keeps Windows from drawing the hidden frame over the page when the window gains or loses the focus.
            return DefWindowProcW(hwnd, message, wParam, -1);
        case WM_GETMINMAXINFO: {
            // A frameless window would maximise over the whole monitor, taskbar included, so it's kept to the work area.
            MONITORINFO monitor{};
            monitor.cbSize = sizeof(monitor);
            if (GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), &monitor)) {
                auto* info = reinterpret_cast<MINMAXINFO*>(lParam);
                info->ptMaxPosition = {monitor.rcWork.left - monitor.rcMonitor.left, monitor.rcWork.top - monitor.rcMonitor.top};
                info->ptMaxSize = {monitor.rcWork.right - monitor.rcWork.left, monitor.rcWork.bottom - monitor.rcWork.top};
            }
            return 0;
        }
        default:
            break;
    }
    auto* native = reinterpret_cast<NativeWindow*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (!native) {
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }
    switch (message) {
        case WM_TIMER:
            if (wParam == 1 && !native->pageLoaded) {
                failWebView(native, native->stage + " timed out after 30 seconds.");
            }
            return 0;
        case WM_SIZE:
            resizeWebView(native);
            syncWebViewVisibility(native);
            return 0;
        case WM_ACTIVATE:
            if (LOWORD(wParam) == WA_INACTIVE && !native->failed) {
                native->owner->notifyFocusLost();
            }
            return 0;
        case WM_CLOSE:
            // A close hides the window, so it's never destroyed by one.
            native->owner->requestClose();
            return 0;
        case WM_DESTROY:
            native->hwnd = nullptr;
            return 0;
        default:
            return DefWindowProcW(hwnd, message, wParam, lParam);
    }
}

void registerWindowClass() {
    static const bool registered = [] {
        WNDCLASSEXW windowClass{};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = windowProc;
        windowClass.hInstance = GetModuleHandleW(nullptr);
        windowClass.hCursor = LoadCursorW(nullptr, MAKEINTRESOURCEW(32512));  // IDC_ARROW, as a wide-string resource id
        // The executable's icon, which the build puts in as resource 1.
        windowClass.hIcon = LoadIconW(windowClass.hInstance, MAKEINTRESOURCEW(1));
        windowClass.hIconSm = windowClass.hIcon;
        windowClass.lpszClassName = kWindowClass;
        if (!RegisterClassExW(&windowClass)) {
            throw std::runtime_error("The window class couldn't be registered.");
        }
        return true;
    }();
    (void)registered;
}

void ensureComInitialized() {
    static const HRESULT initialized = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(initialized)) {
        throw std::runtime_error("COM couldn't be initialized.");
    }
}

}  // namespace

void NativeWindowDeleter::operator()(NativeWindow* native) const {
    if (!native) {
        return;
    }
    native->alive.reset();
    if (native->hwnd) KillTimer(native->hwnd, 1);
    if (native->webview) {
        native->webview->remove_WebMessageReceived(native->messageToken);
        native->webview->remove_NavigationCompleted(native->navigationToken);
        native->webview->remove_ProcessFailed(native->processToken);
        native->webview->Release();
    }
    if (native->controller) {
        native->controller->Close();
        native->controller->Release();
    }
    if (native->hwnd) {
        SetWindowLongPtrW(native->hwnd, GWLP_USERDATA, 0);
        DestroyWindow(native->hwnd);
    }
    delete native;
}

namespace {

// A frameless popup. A resizable one keeps the sizing frame, since Windows only runs the size loop for a window that has it;
// WM_NCCALCSIZE gives the frame no room. The system menu and minimise box let the taskbar minimise and restore the window.
DWORD windowStyle(const WindowOptions& options) {
    DWORD style = WS_POPUP | WS_SYSMENU | WS_MINIMIZEBOX;
    if (options.resizable && !options.autosize) {
        style |= WS_THICKFRAME | WS_MAXIMIZEBOX;
    }
    return style;
}

// Starts the system's move or size loop, as a press on the frame would. The page's press leaves the mouse captured, so the
// capture is released first.
void beginSystemDrag(HWND hwnd, WPARAM hitTest) {
    POINT point{};
    GetCursorPos(&point);
    ReleaseCapture();
    PostMessageW(hwnd, WM_NCLBUTTONDOWN, hitTest, MAKELPARAM(point.x, point.y));
}

// The frame's hit-test code for each ResizeEdge, in its order.
const WPARAM kEdgeHitTests[] = {HTTOPLEFT, HTTOP, HTTOPRIGHT, HTLEFT, HTRIGHT, HTBOTTOMLEFT, HTBOTTOM, HTBOTTOMRIGHT};

// A hidden window that owns the windows kept off the taskbar. A window with an owner gets no taskbar button, and unlike the
// tool-window style it still minimises normally.
HWND taskbarOwner() {
    static HWND owner = CreateWindowExW(0, L"STATIC", L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
    return owner;
}

// Gives an open window its owner, or none. The taskbar reads the owner when a window is shown, so a visible window is hidden and shown again.
void applySkipTaskbar(HWND hwnd, bool skip) {
    const bool visible = IsWindowVisible(hwnd);
    if (visible) {
        ShowWindow(hwnd, SW_HIDE);
    }
    SetWindowLongPtrW(hwnd, GWLP_HWNDPARENT, reinterpret_cast<LONG_PTR>(skip ? taskbarOwner() : nullptr));
    if (visible) {
        ShowWindow(hwnd, SW_SHOWNOACTIVATE);
    }
}

}  // namespace

void WebViewWindow::platformSetSkipTaskbar() {
    if (native_ && native_->hwnd) {
        applySkipTaskbar(native_->hwnd, options_.skipTaskbar);
    }
}

void WebViewWindow::platformBeginMove() {
    if (native_ && native_->hwnd) {
        beginSystemDrag(native_->hwnd, HTCAPTION);
    }
}

void WebViewWindow::platformBeginResize(ResizeEdge edge) {
    if (native_ && native_->hwnd) {
        beginSystemDrag(native_->hwnd, kEdgeHitTests[static_cast<int>(edge)]);
    }
}

void WebViewWindow::platformMinimize() {
    if (native_ && native_->hwnd) {
        ShowWindow(native_->hwnd, SW_MINIMIZE);
    }
}

void WebViewWindow::platformToggleMaximize() {
    if (native_ && native_->hwnd) {
        ShowWindow(native_->hwnd, IsZoomed(native_->hwnd) ? SW_RESTORE : SW_MAXIMIZE);
    }
}

// Posts the close the title bar's button would, so WM_CLOSE asks requestClose.
void WebViewWindow::platformClose() {
    if (native_ && native_->hwnd) {
        PostMessageW(native_->hwnd, WM_CLOSE, 0, 0);
    }
}

void WebViewWindow::platformOpen() {
    ensureComInitialized();
    registerWindowClass();

    const WindowOptions& options = options_;
    DWORD style = windowStyle(options);
    DWORD exStyle = 0;
    if (options.alwaysOnTop) {
        exStyle |= WS_EX_TOPMOST;
    }

    native_.reset(new NativeWindow);
    NativeWindow* native = native_.get();
    native->owner = this;
    std::wstring windowTitle = toWide(title());
    // Opened in the middle of the work area. A caller that places the window moves it after it's opened.
    const auto area = workArea();
    // Sizes are in device-independent pixels, so they grow with the display's DPI.
    const UINT dpi = GetDpiForSystem();
    const int width = MulDiv(scaledSize(options.width), dpi, 96);
    const int height = MulDiv(scaledSize(options.height), dpi, 96);
    const int x = area ? area->x + (area->width - width) / 2 : CW_USEDEFAULT;
    const int y = area ? area->y + (area->height - height) / 2 : CW_USEDEFAULT;
    // The window has no frame that takes room, so its size is the page's size.
    native->hwnd = CreateWindowExW(exStyle, kWindowClass, windowTitle.c_str(), style, x, y, width, height,
                                   options.skipTaskbar ? taskbarOwner() : nullptr, nullptr, GetModuleHandleW(nullptr), native);
    if (!native->hwnd) {
        throw std::runtime_error("The window couldn't be created.");
    }

    const std::string url = devUrl();
    const std::string page = html();
    native->wantsVisible = options.visible;
    if (!SetTimer(native->hwnd, 1, 30000, nullptr)) {
        throw std::runtime_error("The WebView2 startup watchdog could not be started.");
    }
    const auto alive = std::weak_ptr<bool>(native->alive);
    auto onEnvironment = [native, alive, url, page](HRESULT result, ICoreWebView2Environment* environment) {
        if (alive.expired() || native->failed) return;
        if (!checkWebView(native, result, "Creating the WebView2 environment failed")) return;
        if (!environment) {
            failWebView(native, "WebView2 returned no environment. Check that the WebView2 runtime is installed.");
            return;
        }
        native->stage = "Creating the WebView2 controller";
        auto done = webview2::controllerDone([native, alive, url, page](HRESULT controllerResult, ICoreWebView2Controller* controller) {
            if (alive.expired() || native->failed) {
                if (controller) controller->Close();
                return;
            }
            if (!checkWebView(native, controllerResult, "Creating the WebView2 controller failed")) return;
            if (!controller) {
                failWebView(native, "WebView2 returned no controller.");
                return;
            }
            attachWebView(native, controller);
            if (native->failed) return;
            native->stage = "Loading the launcher page";
            if (!url.empty()) {
                checkWebView(native, native->webview->Navigate(toWide(url).c_str()), "Navigating to the launcher page failed");
            } else {
                checkWebView(native, native->webview->NavigateToString(toWide(page).c_str()), "Loading the embedded launcher page failed");
            }
        });
        checkWebView(native, environment->CreateCoreWebView2Controller(native->hwnd, done.get()),
                     "Starting WebView2 controller creation failed");
    };
    auto createEnvironment = loaderFunction<CreateEnvironmentFn>("CreateCoreWebView2EnvironmentWithOptions");
    auto done = webview2::environmentDone(onEnvironment);
    checkWebView(native, createEnvironment(nullptr, nullptr, nullptr, done.get()), "Starting WebView2 environment creation failed");
}

void WebViewWindow::platformUnload() {
    native_.reset();
}

void WebViewWindow::platformPresent() {
    if (native_ && native_->hwnd) {
        native_->wantsVisible = true;
        if (!native_->pageLoaded) {
            native_->wantsFocus = true;
            return;
        }
        // Restores the window if it's minimised, then asks for the foreground. Windows may keep the focus where it is.
        ShowWindow(native_->hwnd, IsIconic(native_->hwnd) ? SW_RESTORE : SW_SHOW);
        syncWebViewVisibility(native_.get());
        SetForegroundWindow(native_->hwnd);
    }
}

void WebViewWindow::platformShow(bool visible) {
    if (native_ && native_->hwnd) {
        native_->wantsVisible = visible;
        if (native_->pageLoaded) {
            ShowWindow(native_->hwnd, visible ? SW_SHOW : SW_HIDE);
            syncWebViewVisibility(native_.get());
        }
    }
}

void WebViewWindow::platformResize(int width, int height) {
    if (native_ && native_->hwnd) {
        const UINT dpi = GetDpiForWindow(native_->hwnd);
        SetWindowPos(native_->hwnd, nullptr, 0, 0, MulDiv(width, dpi, 96), MulDiv(height, dpi, 96), SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    }
}

int WebViewWindow::platformToIndependent(int pixels) const {
    if (!native_ || !native_->hwnd) {
        return pixels;
    }
    return MulDiv(pixels, 96, GetDpiForWindow(native_->hwnd));
}

std::optional<Rect> WebViewWindow::platformFrame() const {
    // A minimised window reports an off-screen place, which isn't where it is.
    if (!native_ || !native_->hwnd || IsIconic(native_->hwnd)) {
        return std::nullopt;
    }
    RECT rect{};
    if (!GetWindowRect(native_->hwnd, &rect)) {
        return std::nullopt;
    }
    return Rect{rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top};
}

std::optional<RuntimeProblem> missingWindowingRuntime() {
    // The loader is found beside the executable, and it reports the runtime's version when the runtime is installed.
    HMODULE loader = LoadLibraryW((launcherDataDir() / "WebView2Loader.dll").c_str());
    if (!loader) {
        return RuntimeProblem{"WebView2Loader.dll is missing from the launcher folder.", ""};
    }
    using VersionFn = HRESULT(WINAPI*)(LPCWSTR, LPWSTR*);
    auto version = reinterpret_cast<VersionFn>(GetProcAddress(loader, "GetAvailableCoreWebView2BrowserVersionString"));
    LPWSTR text = nullptr;
    if (!version || FAILED(version(nullptr, &text)) || !text) {
        return RuntimeProblem{"Install the Microsoft Edge WebView2 runtime, then restart the launcher.", kWebView2Download};
    }
    CoTaskMemFree(text);
    return std::nullopt;
}

void offerLink(const std::string& message, const std::string& link) {
    const std::wstring text = toWide(message + "\n\nOpen the WebView2 download page now?");
    pauseStartupMonitoring(true);
    const int answer = MessageBoxW(nullptr, text.c_str(), L"GW Launcher", MB_YESNO | MB_ICONERROR);
    pauseStartupMonitoring(false);
    if (answer == IDYES) {
        const std::wstring url = toWide(link);
        ShellExecuteW(nullptr, L"open", url.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
    }
}

// Writes the loader, which the launcher carries as resource 2, beside the launcher, unless the copy there already matches. It's staged
// and then moved over the old copy, so a copy that's cut short is never loaded.
void installWebView2Loader() {
    const HRSRC resource = FindResourceW(nullptr, MAKEINTRESOURCEW(2), MAKEINTRESOURCEW(10));
    if (!resource) {
        return;
    }
    const std::pair<const unsigned char*, std::size_t> bytes{
        static_cast<const unsigned char*>(LockResource(LoadResource(nullptr, resource))), SizeofResource(nullptr, resource)};
    if (!bytes.first || bytes.second == 0) {
        return;
    }
    const std::filesystem::path folder = launcherDataDir();
    const std::filesystem::path target = folder / "WebView2Loader.dll";
    std::error_code error;
    if (std::filesystem::is_regular_file(target, error) && std::filesystem::file_size(target, error) == bytes.second) {
        std::ifstream existing(target, std::ios::binary);
        const std::string text((std::istreambuf_iterator<char>(existing)), std::istreambuf_iterator<char>());
        if (text.size() == bytes.second && std::memcmp(text.data(), bytes.first, bytes.second) == 0) {
            return;
        }
    }
    const std::filesystem::path staged = folder / "WebView2Loader.dll.new";
    {
        std::ofstream file(staged, std::ios::binary | std::ios::trunc);
        file.write(reinterpret_cast<const char*>(bytes.first), static_cast<std::streamsize>(bytes.second));
        if (!file) {
            throw std::runtime_error("The WebView2 loader couldn't be written to " + pathToUtf8(staged));
        }
    }
    std::filesystem::rename(staged, target, error);
    if (error) {
        std::filesystem::remove(staged, error);
        throw std::runtime_error("Can't replace the WebView2 loader while it's in use: " + pathToUtf8(target));
    }
}

void initializeWindowing() {
    // This watchdog cannot depend on a message pump that configuration or loader I/O might block.
    std::thread([] {
        unsigned seconds = 0;
        while (seconds < 30) {
            std::this_thread::sleep_for(std::chrono::seconds(1));
            if (startupFinished.load()) return;
            seconds = startupDialogs.load() ? 0 : seconds + 1;
        }
        const std::wstring message = std::wstring(L"GW Launcher startup has not completed within 30 seconds. It is currently ") + startupStage.load() +
            L". Startup may be blocked by inaccessible files or security software.\n\nPress OK to exit, check the launcher folder, and restart. Press Ctrl+C to copy this error.";
        MessageBoxW(nullptr, message.c_str(), L"GW Launcher startup timed out", MB_OK | MB_ICONERROR);
        ExitProcess(1);
    }).detach();
    initializeTaskDispatch();
    installWebView2Loader();
    requireWindowingRuntime();
}

void monitorConfigurationStartup() {
    startupStage = L"reading launcher settings and Accounts.json";
}

void finishStartupMonitoring() {
    startupFinished = true;
}

void pauseStartupMonitoring(bool paused) {
    if (paused) ++startupDialogs;
    else --startupDialogs;
}

void setApplicationIcon(const std::string&) {}

void WebViewWindow::platformMove(int x, int y) {
    if (native_ && native_->hwnd) {
        SetWindowPos(native_->hwnd, nullptr, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }
}

void WebViewWindow::platformSetTitle(const std::string& title) {
    if (native_ && native_->hwnd) {
        SetWindowTextW(native_->hwnd, toWide(title).c_str());
    }
}

void WebViewWindow::platformOpenDevTools() {
    if (native_ && native_->webview) {
        native_->webview->OpenDevToolsWindow();
    }
}

void WebViewWindow::platformPageReady() {
    if (!native_ || native_->failed) return;
    KillTimer(native_->hwnd, 1);
    showLoadedPage(native_.get());
    sendPendingScripts(native_.get());
}

void WebViewWindow::platformFailure(const std::string& message) {
    if (native_) failWebView(native_.get(), message);
}

void WebViewWindow::platformRunScript(const std::string& script, bool requiresReady) {
    if (!native_) {
        return;
    }
    std::wstring source = toWide(script);
    if (native_->failed) return;
    if (native_->webview && (!requiresReady || native_->pageLoaded)) {
        executeScript(native_.get(), source);
    } else {
        native_->pendingScripts.push_back(std::move(source));
    }
}

namespace {

const UINT kRunTask = WM_APP + 1;
HWND taskWindow = nullptr;

LRESULT CALLBACK taskWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
    if (message != kRunTask) return DefWindowProcW(hwnd, message, wParam, lParam);
    std::unique_ptr<std::function<void()>> task(reinterpret_cast<std::function<void()>*>(lParam));
    const auto error = captureError(*task, "A launcher UI task failed: ");
    if (!error.empty()) reportFatalError(error);
    return 0;
}

void initializeTaskDispatch() {
    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = taskWindowProc;
    windowClass.hInstance = GetModuleHandleW(nullptr);
    windowClass.lpszClassName = L"GwlUiTaskWindow";
    if (!RegisterClassW(&windowClass)) throw std::runtime_error("Could not register the launcher UI task dispatcher.");
    taskWindow = CreateWindowExW(0, windowClass.lpszClassName, L"", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, windowClass.hInstance, nullptr);
    if (!taskWindow) throw std::runtime_error("Could not create the launcher UI task dispatcher.");
}

}  // namespace

void runEventLoop() {
    MSG message;
    BOOL result;
    while ((result = GetMessageW(&message, nullptr, 0, 0)) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    if (result == -1) showError("The Windows message loop failed (error " + std::to_string(GetLastError()) + ").");
}

void runOnUiThread(std::function<void()> task) {
    auto pending = std::make_unique<std::function<void()>>(std::move(task));
    if (!taskWindow || !PostMessageW(taskWindow, kRunTask, 0, reinterpret_cast<LPARAM>(pending.get()))) {
        showError("GW Launcher could not deliver a UI task (Windows error " + std::to_string(GetLastError()) + "). Restart the launcher.");
        ExitProcess(1);
    }
    pending.release();
}

void stopEventLoop() {
    PostQuitMessage(0);
}

}  // namespace gwl
