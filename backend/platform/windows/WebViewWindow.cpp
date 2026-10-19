// Windows: a Win32 window with a WebView2 control. WebView2Loader.dll is loaded with LoadLibrary when the first window is
// made, not through the import table, so the program starts without it. The page's messages come from WebView2's
// message event; the page reads the host through window.chrome.webview.

#include <windows.h>
#include <objbase.h>

#include <WebView2.h>

#include <functional>
#include <stdexcept>
#include <string>
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
#include "include/WebView2.h"

namespace gwl {

namespace {

using CreateEnvironmentFn = HRESULT(STDMETHODCALLTYPE*)(PCWSTR, PCWSTR, ICoreWebView2EnvironmentOptions*,
                                                        ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler*);

const wchar_t* const kWindowClass = L"GwlWebViewWindow";
// Where the WebView2 runtime is downloaded from.
const char* const kWebView2Download = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

std::wstring toWide(const std::string& text) {
    if (text.empty()) {
        return {};
    }
    int length = MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0);
    std::wstring wide(length, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), wide.data(), length);
    return wide;
}

std::string toUtf8(const wchar_t* text) {
    if (!text) {
        return {};
    }
    int length = WideCharToMultiByte(CP_UTF8, 0, text, -1, nullptr, 0, nullptr, nullptr) - 1;
    std::string utf8(length > 0 ? length : 0, '\0');
    if (length > 0) {
        WideCharToMultiByte(CP_UTF8, 0, text, -1, utf8.data(), length, nullptr, nullptr);
    }
    return utf8;
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
    WebViewWindow* owner = nullptr;
    HWND hwnd = nullptr;
    ICoreWebView2Controller* controller = nullptr;
    ICoreWebView2* webview = nullptr;
    EventRegistrationToken messageToken{};
    // Scripts asked for before the web view exists. They run once it's ready.
    std::vector<std::wstring> pendingScripts;
    // Set while the window is being destroyed by its owner, so no close notification is sent for it.
    bool destroying = false;
};

namespace {

void resizeWebView(NativeWindow* native) {
    if (!native->controller || !native->hwnd) {
        return;
    }
    RECT bounds{};
    GetClientRect(native->hwnd, &bounds);
    native->controller->put_Bounds(bounds);
}

void syncWebViewVisibility(NativeWindow* native) {
    if (native->controller && native->hwnd) {
        native->controller->put_IsVisible(IsWindowVisible(native->hwnd) && !IsIconic(native->hwnd));
    }
}

void sendPendingScripts(NativeWindow* native) {
    for (const std::wstring& script : native->pendingScripts) {
        native->webview->ExecuteScript(script.c_str(), webview2::scriptDone());
    }
    native->pendingScripts.clear();
}

void attachWebView(NativeWindow* native, ICoreWebView2Controller* controller) {
    native->controller = controller;
    native->controller->AddRef();
    native->controller->get_CoreWebView2(&native->webview);
    resizeWebView(native);
    syncWebViewVisibility(native);

    native->webview->add_WebMessageReceived(
        webview2::messageReceived([native](ICoreWebView2WebMessageReceivedEventArgs* args) {
            LPWSTR message = nullptr;
            if (SUCCEEDED(args->TryGetWebMessageAsString(&message)) && message) {
                native->owner->receive(toUtf8(message));
                CoTaskMemFree(message);
            }
        }),
        &native->messageToken);
    sendPendingScripts(native);
}

LRESULT CALLBACK windowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
    if (message == WM_CREATE) {
        auto* created = reinterpret_cast<CREATESTRUCTW*>(lParam);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(created->lpCreateParams));
        return 0;
    }
    auto* native = reinterpret_cast<NativeWindow*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (!native) {
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }
    switch (message) {
        case WM_SIZE:
            resizeWebView(native);
            syncWebViewVisibility(native);
            return 0;
        case WM_ACTIVATE:
            if (LOWORD(wParam) == WA_INACTIVE) {
                native->owner->notifyFocusLost();
            }
            return 0;
        case WM_CLOSE:
            if (!native->owner->requestClose()) {
                return 0;
            }
            return DefWindowProcW(hwnd, message, wParam, lParam);
        case WM_DESTROY:
            native->hwnd = nullptr;
            if (!native->destroying) {
                native->owner->notifyClosed();
            }
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
    if (FAILED(initialized) && initialized != RPC_E_CHANGED_MODE) {
        throw std::runtime_error("COM couldn't be initialized.");
    }
}

}  // namespace

void NativeWindowDeleter::operator()(NativeWindow* native) const {
    if (!native) {
        return;
    }
    native->destroying = true;
    if (native->webview) {
        native->webview->Release();
    }
    if (native->controller) {
        native->controller->Close();
        native->controller->Release();
    }
    if (native->hwnd) {
        DestroyWindow(native->hwnd);
    }
    delete native;
}

namespace {

// The window style for the title bar: no frame at all, or the standard frame with the minimise and maximise buttons the
// options keep. Close is handled by applyCloseButton, since Windows draws it from the system menu.
DWORD titleBarStyle(const WindowOptions& options) {
    if (!options.decorations) {
        return WS_POPUP;
    }
    DWORD style = WS_OVERLAPPEDWINDOW;
    // An autosize window's page sets its size, so the user can't resize it.
    if (options.autosize) {
        style &= ~(WS_THICKFRAME | WS_MAXIMIZEBOX);
    }
    if (!options.hasButton(TitleButton::Minimize)) {
        style &= ~WS_MINIMIZEBOX;
    }
    if (!options.hasButton(TitleButton::Maximize)) {
        style &= ~WS_MAXIMIZEBOX;
    }
    return style;
}

// Greys out the close item, which greys the title bar's close button too. Windows can't remove that button from a standard
// frame, so it's disabled instead. A frameless window has no system menu, so there's nothing to change.
void applyCloseButton(HWND hwnd, const WindowOptions& options) {
    if (HMENU menu = GetSystemMenu(hwnd, FALSE)) {
        EnableMenuItem(menu, SC_CLOSE, MF_BYCOMMAND | (options.hasButton(TitleButton::Close) ? MF_ENABLED : MF_GRAYED));
    }
}

// The window rectangle that gives a client area of the given size, since the frame takes room outside it.
RECT outerRect(DWORD style, DWORD exStyle, int clientWidth, int clientHeight) {
    RECT rect{0, 0, clientWidth, clientHeight};
    AdjustWindowRectEx(&rect, style, FALSE, exStyle);
    return rect;
}

// Puts the title bar's style on an open window, and redraws its frame so the change shows.
void applyTitleBar(HWND hwnd, const WindowOptions& options) {
    const LONG_PTR current = GetWindowLongPtrW(hwnd, GWL_STYLE);
    const LONG_PTR style = (current & ~static_cast<LONG_PTR>(WS_OVERLAPPEDWINDOW | WS_POPUP)) | static_cast<LONG_PTR>(titleBarStyle(options));
    SetWindowLongPtrW(hwnd, GWL_STYLE, style);
    SetWindowPos(hwnd, nullptr, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    applyCloseButton(hwnd, options);
}

}  // namespace

void WebViewWindow::platformSetDecorated() {
    if (native_ && native_->hwnd) {
        applyTitleBar(native_->hwnd, options_);
    }
}

void WebViewWindow::platformSetTitleButtons() {
    if (native_ && native_->hwnd) {
        applyTitleBar(native_->hwnd, options_);
    }
}

void WebViewWindow::platformOpen() {
    ensureComInitialized();
    registerWindowClass();

    const WindowOptions& options = options_;
    DWORD style = titleBarStyle(options);
    DWORD exStyle = 0;
    if (options.skipTaskbar) {
        exStyle |= WS_EX_TOOLWINDOW;
    }
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
    const RECT outer = outerRect(style, exStyle, width, height);
    native->hwnd = CreateWindowExW(exStyle, kWindowClass, windowTitle.c_str(), style, x, y,
                                   outer.right - outer.left, outer.bottom - outer.top, nullptr, nullptr, GetModuleHandleW(nullptr), native);
    if (!native->hwnd) {
        throw std::runtime_error("The window couldn't be created.");
    }
    applyCloseButton(native->hwnd, options);

    const std::string url = devUrl();
    const std::string page = html();
    auto onEnvironment = [native, url, page](HRESULT result, ICoreWebView2Environment* environment) {
        if (FAILED(result) || !environment) {
            showError("The web view couldn't start, so the window is empty. Is the WebView2 runtime installed?");
            return;
        }
        environment->CreateCoreWebView2Controller(
            native->hwnd, webview2::controllerDone([native, url, page](HRESULT controllerResult, ICoreWebView2Controller* controller) {
                if (FAILED(controllerResult) || !controller) {
                    showError("The web view couldn't be attached to its window, so the window is empty.");
                    return;
                }
                attachWebView(native, controller);
                if (!url.empty()) {
                    native->webview->Navigate(toWide(url).c_str());
                } else {
                    native->webview->NavigateToString(toWide(page).c_str());
                }
            }));
    };
    auto createEnvironment = loaderFunction<CreateEnvironmentFn>("CreateCoreWebView2EnvironmentWithOptions");
    createEnvironment(nullptr, nullptr, nullptr, webview2::environmentDone(onEnvironment));

    ShowWindow(native->hwnd, options.visible ? SW_SHOW : SW_HIDE);
}

void WebViewWindow::platformPresent() {
    if (native_ && native_->hwnd) {
        // Restores the window if it's minimised, then asks for the foreground. Windows may keep the focus where it is.
        ShowWindow(native_->hwnd, IsIconic(native_->hwnd) ? SW_RESTORE : SW_SHOW);
        syncWebViewVisibility(native_.get());
        SetForegroundWindow(native_->hwnd);
    }
}

void WebViewWindow::platformShow(bool visible) {
    if (native_ && native_->hwnd) {
        ShowWindow(native_->hwnd, visible ? SW_SHOW : SW_HIDE);
        syncWebViewVisibility(native_.get());
    }
}

void WebViewWindow::platformResize(int width, int height) {
    if (native_ && native_->hwnd) {
        const LONG_PTR style = GetWindowLongPtrW(native_->hwnd, GWL_STYLE);
        const LONG_PTR exStyle = GetWindowLongPtrW(native_->hwnd, GWL_EXSTYLE);
        const UINT dpi = GetDpiForWindow(native_->hwnd);
        const RECT outer = outerRect(static_cast<DWORD>(style), static_cast<DWORD>(exStyle), MulDiv(width, dpi, 96), MulDiv(height, dpi, 96));
        SetWindowPos(native_->hwnd, nullptr, 0, 0, outer.right - outer.left, outer.bottom - outer.top,
                     SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
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
        return RuntimeProblem{"WebView2Loader.dll isn't next to the launcher, so its windows can't open.", ""};
    }
    using VersionFn = HRESULT(WINAPI*)(LPCWSTR, LPWSTR*);
    auto version = reinterpret_cast<VersionFn>(GetProcAddress(loader, "GetAvailableCoreWebView2BrowserVersionString"));
    LPWSTR text = nullptr;
    if (!version || FAILED(version(nullptr, &text)) || !text) {
        return RuntimeProblem{"The Microsoft Edge WebView2 runtime isn't installed, so the launcher's windows can't open. Install it, then start the launcher again.", kWebView2Download};
    }
    CoTaskMemFree(text);
    return std::nullopt;
}

void offerLink(const std::string& message, const std::string& link) {
    const std::wstring text = toWide(message + "\n\nOpen the WebView2 download page now?");
    if (MessageBoxW(nullptr, text.c_str(), L"GW Launcher", MB_YESNO | MB_ICONERROR) == IDYES) {
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
            throw std::runtime_error("The WebView2 loader couldn't be written to " + staged.string());
        }
    }
    std::filesystem::rename(staged, target, error);
    if (error) {
        std::filesystem::remove(staged, error);
        throw std::runtime_error("The WebView2 loader is in use, so it can't be replaced: " + target.string());
    }
}

void initializeWindowing() {
    installWebView2Loader();
    requireWindowingRuntime();
}

void setApplicationIcon(const std::string&) {}

void WebViewWindow::platformMove(int x, int y) {
    if (native_ && native_->hwnd) {
        SetWindowPos(native_->hwnd, nullptr, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }
}

void WebViewWindow::platformSetTitle(const std::string& title) {
    if (native_ && native_->hwnd) {
        std::wstring wide = toWide(title);
        SetWindowTextW(native_->hwnd, wide.c_str());
    }
}

void WebViewWindow::platformOpenDevTools() {
    if (native_ && native_->webview) {
        native_->webview->OpenDevToolsWindow();
    }
}

void WebViewWindow::platformRunScript(const std::string& script) {
    if (!native_) {
        return;
    }
    std::wstring wide = toWide(script);
    if (native_->webview) {
        native_->webview->ExecuteScript(wide.c_str(), webview2::scriptDone());
    } else {
        native_->pendingScripts.push_back(std::move(wide));
    }
}

namespace {

const UINT kRunTask = WM_APP + 1;
DWORD gUiThread = 0;

}  // namespace

void runEventLoop() {
    gUiThread = GetCurrentThreadId();
    MSG message;
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        if (message.message == kRunTask && !message.hwnd) {
            auto* task = reinterpret_cast<std::function<void()>*>(message.lParam);
            (*task)();
            delete task;
            continue;
        }
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
}

void runOnUiThread(std::function<void()> task) {
    PostThreadMessageW(gUiThread, kRunTask, 0, reinterpret_cast<LPARAM>(new std::function<void()>(std::move(task))));
}

void stopEventLoop() {
    PostQuitMessage(0);
}

}  // namespace gwl
