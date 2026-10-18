// A rectangle on the screen, in screen pixels, and the operations the launcher needs on one.

#pragma once

#include <algorithm>
#include <cstring>
#include <type_traits>

namespace gwl {

struct Rect {
    int x = 0;
    int y = 0;
    int width = 0;
    int height = 0;

    int right() const { return x + width; }
    int bottom() const { return y + height; }
    int centreX() const { return x + width / 2; }
    int centreY() const { return y + height / 2; }
    // Whether the rectangle is taller than it is wide: a column, as on a left or right panel.
    bool isVertical() const { return height > width; }

    // Whether the point is inside: left and top edges included, right and bottom excluded.
    bool contains(int px, int py) const { return px >= x && px < right() && py >= y && py < bottom(); }

    // Whether the two rectangles share any area.
    bool overlaps(const Rect& other) const {
        return x < other.right() && other.x < right() && y < other.bottom() && other.y < bottom();
    }

    // Whether inner lies entirely inside this rectangle.
    bool contains(const Rect& inner) const {
        return inner.x >= x && inner.y >= y && inner.right() <= right() && inner.bottom() <= bottom();
    }

    // Whether both width and height are between min and max, both included.
    bool sizeBetween(int min, int max) const {
        return width >= min && width <= max && height >= min && height <= max;
    }

    // The smallest rectangle holding both this one and other.
    Rect joined(const Rect& other) const {
        const int left = std::min(x, other.x);
        const int top = std::min(y, other.y);
        return Rect{left, top, std::max(right(), other.right()) - left, std::max(bottom(), other.bottom()) - top};
    }

    // Two rectangles are equal when their fields are: the struct is four ints with no padding, so its bytes are its fields.
    bool operator==(const Rect& other) const { return std::memcmp(this, &other, sizeof(Rect)) == 0; }
    bool operator!=(const Rect& other) const { return !(*this == other); }
};

static_assert(std::is_trivially_copyable_v<Rect> && sizeof(Rect) == 4 * sizeof(int), "Rect must compare by its bytes");

}  // namespace gwl
