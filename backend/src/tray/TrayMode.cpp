// The tray's behaviour in the launcher: the tray icon's presence and its clicks, and where the list opens beside the icon.
// The tray's own parts are in tray/Tray.h and tray/TrayRegion.h, and their platform code is in platform/<os>.

#include <algorithm>
#include <chrono>
#include <cstdio>
#include <exception>
#include <optional>

#include "AccountListWindow.h"
#include "AppState.h"
#include "EmbeddedAssets.h"
#include "tray/Tray.h"
#include "tray/TrayRegion.h"

namespace launcher {

namespace {

// The gap between the list and the tray rect it sits beside.
const int kTrayGap = 4;
// A tray click this soon after focus hid the list is the click that hid it, so it doesn't show the list again.
const auto kTrayClickAfterFocusLoss = std::chrono::milliseconds(500);

}  // namespace

void AppState::applyTrayMode() {
    setTrayIcon();
    // With the tray holding it the list is a flyout, with no title bar; without the tray it's an ordinary window again.
    if (list) {
        list->setDecorated(tray == nullptr);
    }
}

void AppState::setTrayIcon() {
    if (!settings.effective(gwl::hasTray()).keepInSystemTray) {
        tray.reset();
        if (list && !list->isVisible()) {
            list->show();
        }
        return;
    }
    if (tray) {
        return;
    }
    try {
        tray = std::make_unique<gwl::TrayIcon>(gwl::embeddedHelmetPng(), [this](gwl::TrayAction action, int x, int y) {
            trayEvent(action, x, y);
        });
    } catch (const std::exception& error) {
        // Without the icon the list stays an ordinary window, so it can't be lost by closing it.
        std::fprintf(stderr, "could not add the tray icon: %s\n", error.what());
        if (list && !list->isVisible()) {
            list->show();
        }
    }
}

void AppState::trayEvent(gwl::TrayAction action, int x, int y) {
    switch (action) {
    case gwl::TrayAction::Activate:
        trayAnchor = gwl::Rect{x, y, 0, 0};
        if (list && list->isVisible()) {
            hideList();
        } else if (std::chrono::steady_clock::now() - listHiddenByFocusAt > kTrayClickAfterFocusLoss) {
            showList(trayAnchor);
        }
        break;
    case gwl::TrayAction::Open: {
        // Open from the tray menu uses the last click's place, or the pointer's.
        std::optional<gwl::Rect> anchor = trayAnchor;
        if (!anchor) {
            if (const auto pointer = gwl::cursorPosition()) {
                anchor = gwl::Rect{pointer->first, pointer->second, 0, 0};
            }
        }
        showList(anchor);
        break;
    }
    case gwl::TrayAction::Quit:
        rememberListPlace();
        gwl::stopEventLoop();
        break;
    }
}

void AppState::rememberListPlace() {
    if (!list || !list->isVisible()) {
        return;
    }
    const auto frame = list->frame();
    if (!frame) {
        return;
    }
    settings.listX = frame->x;
    settings.listY = frame->y;
    const auto size = list->frameUnscaled();
    if (!size) {
        return;
    }
    settings.listWidth = size->first;
    settings.listHeight = size->second;
    saveSettings();
}

void AppState::hideList() {
    rememberListPlace();
    list->hide();
}

void AppState::showList(std::optional<gwl::Rect> click, std::optional<gwl::Rect> trayRect) {
    if (!list) {
        return;
    }
    listShownAt = std::chrono::steady_clock::now();
    const auto area = gwl::workArea();
    if (!area) {
        list->show();
        listShownAt = std::chrono::steady_clock::now();
        return;
    }
    const int baseWidth = static_cast<int>(settings.listWidth);
    const int baseHeight = static_cast<int>(settings.listHeight);
    list->resize(baseWidth, baseHeight);
    // The window's real size, which the display's scale can make larger than the UI scale alone gives.
    const auto actual = list->frame();
    const int width = actual ? actual->width : gwl::scaledSize(baseWidth);
    const int height = actual ? actual->height : gwl::scaledSize(baseHeight);
    // Beside a rect: see gwl::placeBeside. The list stays inside the work area.
    auto besideRect = [&](const gwl::Rect& rect, bool portrait) {
        const gwl::Rect beside = gwl::placeBeside(rect, width, height, *area, kTrayGap, portrait);
        return std::make_pair(beside.x, beside.y);
    };
    std::pair<int, int> place{area->centreX() - width / 2, area->centreY() - height / 2};
    const auto screen = gwl::screenSize();
    const auto strip = screen ? gwl::panelStrip(gwl::Rect{0, 0, screen->first, screen->second}, *area) : std::nullopt;
    // In tray mode the list goes beside the icon whenever the desktop gives its rectangle, before the click's point. The panel's
    // orientation decides the side: an icon's rectangle is as tall as the taskbar, so its own shape doesn't say.
    if (!trayRect && tray) {
        trayRect = tray->rect();
    }
    if (trayRect) {
        place = besideRect(*trayRect, strip && strip->isVertical());
    } else if (click) {
        // A click gives only a point, so the panel's orientation says whether the tray is a column or a row.
        place = besideRect(*click, strip && strip->isVertical());
    } else if (settings.listX && settings.listY) {
        place = {static_cast<int>(*settings.listX), static_cast<int>(*settings.listY)};
    } else if (strip) {
        place = besideRect(*strip, strip->isVertical());
    }
    const int left = std::clamp(place.first, area->x, std::max(area->x, area->right() - width));
    const int top = std::clamp(place.second, area->y, std::max(area->y, area->bottom() - height));
    list->move(left, top);
    list->present();
    // The window manager may place a window again when it's mapped, so the place is set after it's shown too.
    list->move(left, top);
    listShownAt = std::chrono::steady_clock::now();
}

void AppState::showListAtStart() {
    // The icon's own rectangle is where the list goes, when the desktop reports it.
    if (tray) {
        if (const auto icon = tray->rect()) {
            showList(std::nullopt, icon);
            return;
        }
    }
    const auto screen = gwl::screenSize();
    const auto area = gwl::workArea();
    if (!screen || !area) {
        showList(std::nullopt);
        return;
    }
    const gwl::Rect screenRect{0, 0, screen->first, screen->second};
    // Finding the tray asks the desktop, so it's asked on a worker, and the list follows on the window's thread.
    workers.submit([screenRect, workRect = *area] { return gwl::locateTray(screenRect, workRect); },
                   [this](std::optional<gwl::Rect> region) {
                       gwl::runOnUiThread([this, region] {
                           if (list) {
                               showList(std::nullopt, region);
                           }
                       });
                   });
}

}  // namespace launcher
