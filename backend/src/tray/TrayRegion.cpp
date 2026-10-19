#include "tray/TrayRegion.h"

#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <cstring>

namespace gwl {

namespace {

// Roles of widgets that hold text rather than an icon. The clock is one of these.
const char* const kTextRoles[] = {"label", "text", "static"};
// An icon-sized widget is between these sizes on both sides.
const int kMinIcon = 12;
const int kMaxIcon = 48;
// Icons whose centres are within this share of an icon's height of the icon row's centre are on the row.
const float kRowToleranceShare = 0.3f;
// Icons next to each other are grouped while the gap between them is no more than this share of an icon's height.
const float kGroupGapShare = 2.0f;

// Whether the widget at index is a wrapper: it holds another widget. A wrapper with the same rectangle as a text widget is
// the text's wrapper; two icons with the same rectangle are separate icons, so neither is a wrapper of the other.
bool isWrapper(std::size_t index, const std::vector<TrayWidget>& widgets) {
    const Rect& outer = widgets[index].rect;
    for (std::size_t j = 0; j < widgets.size(); ++j) {
        if (j == index) {
            continue;
        }
        const TrayWidget& inner = widgets[j];
        if (outer.contains(inner.rect) && (inner.rect != outer || inner.isText())) {
            return true;
        }
    }
    return false;
}

}  // namespace

bool TrayWidget::isText() const {
    return std::any_of(std::begin(kTextRoles), std::end(kTextRoles), [this](const char* text) { return role == text; });
}

Rect placeBeside(const Rect& tray, int width, int height, const Rect& workArea, int gap, bool portrait) {
    auto clampTo = [](int value, int low, int high) { return std::max(low, std::min(value, high)); };
    // The window is kept against the work area, so it never covers a panel. Above and below slide along the edge, and left and
    // right slide up and down, so a candidate beside the tray is pushed out to the work area's edge when the tray is inside a panel.
    const int centredLeft = clampTo(tray.centreX() - width / 2, workArea.x, workArea.right() - width);
    const int centredTop = clampTo(tray.centreY() - height / 2, workArea.y, workArea.bottom() - height);
    const int aboveTop = tray.y - height - gap;
    const int belowTop = std::max(tray.bottom() + gap, workArea.y);
    const int rightLeft = std::max(tray.right() + gap, workArea.x);
    const int leftLeft = std::min(tray.x - gap - width, workArea.right() - width);

    const bool fitsAbove = aboveTop >= workArea.y;
    const bool fitsBelow = belowTop + height <= workArea.bottom();
    const bool fitsRight = rightLeft + width <= workArea.right();
    const bool fitsLeft = leftLeft >= workArea.x;
    const Rect above{centredLeft, aboveTop, width, height};
    const Rect below{centredLeft, belowTop, width, height};
    const Rect right{rightLeft, centredTop, width, height};
    const Rect left{leftLeft, centredTop, width, height};

    // A portrait tray (a column of icons) opens beside it. A landscape one opens above or below it.
    if (portrait) {
        if (fitsRight) return right;
        if (fitsLeft) return left;
        if (fitsAbove) return above;
        if (fitsBelow) return below;
    } else {
        if (fitsAbove) return above;
        if (fitsRight) return right;
        if (fitsBelow) return below;
        if (fitsLeft) return left;
    }
    return Rect{centredLeft, clampTo(aboveTop, workArea.y, workArea.bottom() - height), width, height};
}

std::optional<Rect> panelStrip(const Rect& screen, const Rect& workArea) {
    const Rect strips[] = {
        {workArea.x, workArea.bottom(), workArea.width, screen.bottom() - workArea.bottom()},
        {workArea.x, screen.y, workArea.width, workArea.y - screen.y},
        {screen.x, workArea.y, workArea.x - screen.x, workArea.height},
        {workArea.right(), workArea.y, screen.right() - workArea.right(), workArea.height},
    };
    std::optional<Rect> largest;
    for (const Rect& strip : strips) {
        if (strip.width > 0 && strip.height > 0 && (!largest || strip.width * strip.height > largest->width * largest->height)) {
            largest = strip;
        }
    }
    return largest;
}

std::optional<Rect> trayFromWidgets(const std::vector<TrayWidget>& widgets, bool vertical) {
    // The icons run along the panel: left to right on a horizontal panel, top to bottom on a vertical one. Across the panel is
    // the other way, and the icon row is the icons' centres across it.
    auto along = [vertical](const Rect& r) { return vertical ? r.y : r.x; };
    auto span = [vertical](const Rect& r) { return vertical ? r.height : r.width; };
    auto across = [vertical](const Rect& r) { return vertical ? r.centreX() : r.centreY(); };
    auto thickness = [vertical](const Rect& r) { return vertical ? r.width : r.height; };

    std::vector<Rect> candidates;
    for (std::size_t i = 0; i < widgets.size(); ++i) {
        const Rect& rect = widgets[i].rect;
        if (rect.sizeBetween(kMinIcon, kMaxIcon) && !widgets[i].isText() && !isWrapper(i, widgets)) {
            candidates.push_back(rect);
        }
    }

    // The row is where most of the tall icons sit, so widgets off to the side of it (labels, containers) are left out.
    std::vector<Rect> tall;
    for (const Rect& rect : candidates) {
        if (thickness(rect) >= kMaxIcon / 2) {
            tall.push_back(rect);
        }
    }
    if (tall.empty()) {
        return std::nullopt;
    }
    std::sort(tall.begin(), tall.end(), [&](const Rect& a, const Rect& b) { return across(a) < across(b); });
    const Rect middle = tall[tall.size() / 2];
    const int row = across(middle);
    const int tolerance = static_cast<int>(static_cast<float>(thickness(middle)) * kRowToleranceShare);

    std::vector<Rect> icons;
    for (const Rect& rect : candidates) {
        if (std::abs(across(rect) - row) <= tolerance) {
            icons.push_back(rect);
        }
    }
    std::sort(icons.begin(), icons.end(), [&](const Rect& a, const Rect& b) { return along(a) < along(b); });

    // Groups are split where the gap is wider than the icon's own thickness, so separate neighbours don't merge.
    std::vector<std::vector<Rect>> groups;
    for (const Rect& icon : icons) {
        const int gapLimit = static_cast<int>(static_cast<float>(thickness(icon)) * kGroupGapShare);
        if (!groups.empty() && along(icon) - (along(groups.back().back()) + span(groups.back().back())) <= gapLimit) {
            groups.back().push_back(icon);
        } else {
            groups.push_back({icon});
        }
    }
    if (groups.empty()) {
        return std::nullopt;
    }

    // The tray is the group with the most icons. A lone icon further along the panel belongs to something else, such as a clock.
    const std::vector<Rect>* tray = &groups.back();
    for (const std::vector<Rect>& group : groups) {
        if (group.size() >= tray->size()) {
            tray = &group;
        }
    }
    Rect bounds = tray->front();
    for (const Rect& icon : *tray) {
        bounds = bounds.joined(icon);
    }
    return bounds;
}

}  // namespace gwl
