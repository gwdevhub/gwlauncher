// Finds the tray on the panel from the widgets the desktop's accessibility tree shows along the panel. Platform-free: the
// platform reads the widgets, and this decides which of them are the tray. The panel can be on any edge of the screen.

#pragma once

#include <optional>
#include <string>
#include <vector>

#include "Geometry.h"

namespace gwl {

// A widget on the panel's band: its screen rectangle and its accessibility role.
struct TrayWidget {
    Rect rect;
    std::string role;

    // Whether the widget holds text rather than an icon, such as the clock.
    bool isText() const;
};

// Where a window of width x height goes beside the tray: a portrait tray (a column of icons, on a side panel) opens to its right,
// then its left, then above, then below. A landscape tray (a row, on a top or bottom panel) opens above, then to its right,
// below, then to its left. The first that fits in the work area is used. If none fits, it's above the tray, clamped into the
// work area. The orientation is passed in: a click gives only a point, so the panel decides it.
Rect placeBeside(const Rect& tray, int width, int height, const Rect& workArea, int gap, bool portrait);

// The panel's strip: the part of the screen the panels take, which is the largest part the work area leaves out. Missing
// when nothing is left out, so there's no panel.
std::optional<Rect> panelStrip(const Rect& screen, const Rect& workArea);

// The tray's rectangle: the group of icons along the panel with the most icons, where a group is icons that sit close together.
// Icons are icon-sized widgets that aren't text, and a widget holding another widget is a container, so it's left out.
// Nothing when the panel shows no icons.
std::optional<Rect> trayFromWidgets(const std::vector<TrayWidget>& widgets, bool vertical = false);

}  // namespace gwl
