// Windows: the tray icon is a notification icon on the taskbar, fed by a hidden window. Its messages arrive on the UI thread's
// message loop, so the handler runs there, as on Linux.

#include <windows.h>
#include <shellapi.h>

#include <cstdint>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>

#include "tray/Tray.h"
#include "tray/TrayRegion.h"
#include "utils/Images.h"

namespace gwl {

namespace {

const wchar_t* const kWindowClass = L"GwlTrayWindow";
const wchar_t* const kTip = L"GW Launcher";
const UINT kTrayMessage = WM_APP + 0x40;

// Icon size for the taskbar, at the system's small-icon size.
Rgba trayImage(const std::string& iconPng) {
    const auto frame = decodePng(bytesOf(iconPng));
    if (!frame) {
        throw std::runtime_error("The tray icon couldn't be read.");
    }
    return scaleToFit(*frame, static_cast<std::uint32_t>(GetSystemMetrics(SM_CXSMICON)));
}

// A 32-bit icon from RGBA pixels. Colour is premultiplied by alpha, which is what the taskbar blends.
HICON iconFrom(const Rgba& image) {
    BITMAPV5HEADER header{};
    header.bV5Size = sizeof(header);
    header.bV5Width = static_cast<LONG>(image.width);
    header.bV5Height = -static_cast<LONG>(image.height);
    header.bV5Planes = 1;
    header.bV5BitCount = 32;
    header.bV5Compression = BI_BITFIELDS;
    header.bV5RedMask = 0x00FF0000;
    header.bV5GreenMask = 0x0000FF00;
    header.bV5BlueMask = 0x000000FF;
    header.bV5AlphaMask = 0xFF000000;

    HDC screen = GetDC(nullptr);
    void* bits = nullptr;
    HBITMAP color = CreateDIBSection(screen, reinterpret_cast<const BITMAPINFO*>(&header), DIB_RGB_COLORS, &bits, nullptr, 0);
    ReleaseDC(nullptr, screen);
    if (!color || !bits) {
        throw std::runtime_error("The tray icon's bitmap couldn't be made.");
    }
    auto* out = static_cast<std::uint8_t*>(bits);
    for (std::size_t i = 0; i + 3 < image.pixels.size(); i += 4) {
        const std::uint32_t alpha = image.pixels[i + 3];
        out[i + 0] = static_cast<std::uint8_t>(image.pixels[i + 2] * alpha / 255);
        out[i + 1] = static_cast<std::uint8_t>(image.pixels[i + 1] * alpha / 255);
        out[i + 2] = static_cast<std::uint8_t>(image.pixels[i + 0] * alpha / 255);
        out[i + 3] = static_cast<std::uint8_t>(alpha);
    }
    // The colour carries the alpha, so the mask is all clear.
    HBITMAP mask = CreateBitmap(static_cast<int>(image.width), static_cast<int>(image.height), 1, 1, nullptr);
    ICONINFO info{};
    info.fIcon = TRUE;
    info.hbmMask = mask;
    info.hbmColor = color;
    HICON icon = CreateIconIndirect(&info);
    DeleteObject(color);
    DeleteObject(mask);
    if (!icon) {
        throw std::runtime_error("The tray icon couldn't be made.");
    }
    return icon;
}

}  // namespace

struct TrayItem {
    TrayHandler onEvent;
    HWND window = nullptr;
    HICON icon = nullptr;
    UINT taskbarCreated = 0;
    bool added = false;

    explicit TrayItem(TrayHandler handler) : onEvent(std::move(handler)) {}
    ~TrayItem();

    NOTIFYICONDATAW data() const;
    bool add();
    void remove();
    LRESULT handle(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam);
};

namespace {

LRESULT CALLBACK trayProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) {
    if (message == WM_NCCREATE) {
        const auto* create = reinterpret_cast<const CREATESTRUCTW*>(lparam);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
    }
    if (auto* item = reinterpret_cast<TrayItem*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA))) {
        return item->handle(hwnd, message, wparam, lparam);
    }
    return DefWindowProcW(hwnd, message, wparam, lparam);
}

void registerWindowClass() {
    WNDCLASSEXW windowClass{};
    windowClass.cbSize = sizeof(windowClass);
    windowClass.lpfnWndProc = trayProc;
    windowClass.hInstance = GetModuleHandleW(nullptr);
    windowClass.lpszClassName = kWindowClass;
    if (!RegisterClassExW(&windowClass) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS) {
        throw std::runtime_error("The tray's window class couldn't be registered.");
    }
}

}  // namespace

NOTIFYICONDATAW TrayItem::data() const {
    NOTIFYICONDATAW data{};
    data.cbSize = sizeof(data);
    data.hWnd = window;
    data.uID = 1;
    data.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    data.uCallbackMessage = kTrayMessage;
    data.hIcon = icon;
    lstrcpynW(data.szTip, kTip, ARRAYSIZE(data.szTip));
    return data;
}

bool TrayItem::add() {
    NOTIFYICONDATAW notify = data();
    added = Shell_NotifyIconW(NIM_ADD, &notify) == TRUE;
    return added;
}

void TrayItem::remove() {
    if (!added) {
        return;
    }
    NOTIFYICONDATAW notify = data();
    Shell_NotifyIconW(NIM_DELETE, &notify);
    added = false;
}

LRESULT TrayItem::handle(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) {
    if (message == taskbarCreated) {
        // The taskbar was restarted, which drops the icon, so it's added again.
        added = false;
        add();
        return 0;
    }
    if (message != kTrayMessage) {
        return DefWindowProcW(hwnd, message, wparam, lparam);
    }
    switch (LOWORD(lparam)) {
    case WM_LBUTTONUP: {
        POINT point{};
        GetCursorPos(&point);
        onEvent(TrayAction::Activate, point.x, point.y);
        break;
    }
    case WM_RBUTTONUP: {
        POINT point{};
        GetCursorPos(&point);
        onEvent(TrayAction::RightClick, point.x, point.y);
        break;
    }
    default:
        break;
    }
    return 0;
}

TrayItem::~TrayItem() {
    remove();
    if (window) {
        DestroyWindow(window);
    }
    if (icon) {
        DestroyIcon(icon);
    }
}

TrayIcon::TrayIcon(const std::string& iconPng, TrayHandler onEvent) : item_(std::make_unique<TrayItem>(std::move(onEvent))) {
    registerWindowClass();
    item_->icon = iconFrom(trayImage(iconPng));
    item_->taskbarCreated = RegisterWindowMessageW(L"TaskbarCreated");

    // A top-level window, not a message-only one, since the taskbar's restart is broadcast to top-level windows.
    item_->window = CreateWindowExW(0, kWindowClass, kTip, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, GetModuleHandleW(nullptr), item_.get());
    if (!item_->window) {
        throw std::runtime_error("The tray's window couldn't be made.");
    }
    if (!item_->add()) {
        throw std::runtime_error("The taskbar has no notification area to show the icon in.");
    }
}

TrayIcon::~TrayIcon() = default;

std::optional<Rect> TrayIcon::rect() const {
    if (!item_ || !item_->added) {
        return std::nullopt;
    }
    NOTIFYICONIDENTIFIER identifier{};
    identifier.cbSize = sizeof(identifier);
    identifier.hWnd = item_->window;
    identifier.uID = 1;
    RECT rect{};
    if (FAILED(Shell_NotifyIconGetRect(&identifier, &rect))) {
        return std::nullopt;
    }
    return Rect{rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top};
}

bool hasTray() {
    return FindWindowW(L"Shell_TrayWnd", nullptr) != nullptr;
}

std::optional<Rect> locateTray(const Rect& screen, const Rect&) {
    // The notification area holds the tray icons, and the clock. It's a child of the taskbar.
    HWND taskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
    HWND notify = taskbar ? FindWindowExW(taskbar, nullptr, L"TrayNotifyWnd", nullptr) : nullptr;
    if (!notify) {
        return std::nullopt;
    }
    RECT rect{};
    if (!GetWindowRect(notify, &rect)) {
        return std::nullopt;
    }
    const Rect region{rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top};
    if (region.width <= 0 || region.height <= 0 || !screen.contains(region)) {
        return std::nullopt;
    }
    return region;
}

}  // namespace gwl
