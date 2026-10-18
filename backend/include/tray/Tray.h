// The system tray icon. On Linux it's a StatusNotifierItem; the Windows tray isn't ported yet, so the desktop has no tray.

#pragma once

#include <functional>
#include <memory>
#include <optional>
#include <string>

#include "Geometry.h"

namespace gwl {

enum class TrayAction { Activate, Open, Quit };

// Called on the UI thread. For Activate, x and y are where on the screen the icon was clicked.
using TrayHandler = std::function<void(TrayAction action, int x, int y)>;

// Whether the desktop has a tray to keep the launcher in.
bool hasTray();

struct TrayItem;

// Where the desktop's tray is on the screen: the rectangle of its icons, on whichever edge the panel is. The screen and its
// work area are what the desktop reports for the monitor. It asks the desktop, so it blocks, and runs on a worker. Nothing
// when the desktop doesn't say.
std::optional<Rect> locateTray(const Rect& screen, const Rect& workArea);

// The icon in the tray, shown while the object lives. iconPng is the PNG the icon is drawn from. Throws std::runtime_error
// with the reason when the desktop has no tray to take it.
class TrayIcon {
public:
    TrayIcon(const std::string& iconPng, TrayHandler onEvent);
    ~TrayIcon();

    TrayIcon(const TrayIcon&) = delete;
    TrayIcon& operator=(const TrayIcon&) = delete;

    // The icon's own rectangle on the screen, when the desktop reports it. Nothing when it doesn't.
    std::optional<Rect> rect() const;

private:
    std::unique_ptr<TrayItem> item_;
};

}  // namespace gwl
