#include "WebViewWindow.h"

#include <algorithm>
#include <cstdio>
#include <set>
#include <utility>

namespace gwl {

namespace {

int uiScale = 100;

// The rectangle moved the least it takes to lie inside area. One larger than the area sits at the area's top left.
Rect keptWithin(Rect rect, const Rect& area) {
    rect.x = std::max(area.x, std::min(rect.x, area.right() - rect.width));
    rect.y = std::max(area.y, std::min(rect.y, area.bottom() - rect.height));
    return rect;
}

// The windows that are open, so a change of scale reaches each of them.
std::set<WebViewWindow*>& liveWindows() {
    static std::set<WebViewWindow*> windows;
    return windows;
}

}  // namespace

WebViewWindow::WebViewWindow(WindowOptions options)
    : options_(std::move(options)), baseWidth_(options_.width), baseHeight_(options_.height) {
    liveWindows().insert(this);
}

WebViewWindow::~WebViewWindow() {
    liveWindows().erase(this);
}

void requireWindowingRuntime() {
    if (const auto problem = missingWindowingRuntime()) {
        if (problem->link.empty()) {
            showError(problem->message);
        } else {
            offerLink(problem->message, problem->link);
        }
        throw RuntimeMissingError(problem->message);
    }
}

void WebViewWindow::open() {
    // Opening a window that already exists brings it to the front, so every opener restores a minimised window the same way.
    if (opened_) {
        present();
        return;
    }
    requireWindowingRuntime();
    visible_ = options_.visible;
    platformOpen();
    opened_ = true;
    keepOnScreen();
}

void WebViewWindow::show() {
    visible_ = true;
    platformShow(true);
}

void WebViewWindow::present() {
    visible_ = true;
    platformPresent();
}

void WebViewWindow::hide() {
    visible_ = false;
    platformShow(false);
}

void WebViewWindow::resize(int width, int height) {
    baseWidth_ = width;
    baseHeight_ = height;
    platformResize(scaledSize(width), scaledSize(height));
    keepOnScreen();
}

std::optional<std::pair<int, int>> WebViewWindow::frameUnscaled() const {
    const auto frame = platformFrame();
    if (!frame) {
        return std::nullopt;
    }
    return std::make_pair(unscaledSize(platformToIndependent(frame->width)), unscaledSize(platformToIndependent(frame->height)));
}

void WebViewWindow::rememberSize() {
    if (!opened_ || options_.autosize) {
        return;
    }
    if (const auto size = frameUnscaled()) {
        baseWidth_ = size->first;
        baseHeight_ = size->second;
    }
}

void WebViewWindow::applyScale() {
    if (!opened_ || options_.autosize) {
        return;
    }
    platformResize(scaledSize(baseWidth_), scaledSize(baseHeight_));
    keepOnScreen();
}

void WebViewWindow::fitToPage(int width, int height) {
    if (options_.autosize) {
        platformResize(width, height);
        keepOnScreen();
    }
}

std::optional<Rect> WebViewWindow::frame() const {
    return platformFrame();
}

void WebViewWindow::move(int x, int y) {
    const auto current = platformFrame();
    if (!current) {
        platformMove(x, y);
        return;
    }
    const Rect target{x, y, current->width, current->height};
    const auto area = workAreaAt(target);
    const Rect kept = area ? keptWithin(target, *area) : target;
    platformMove(kept.x, kept.y);
}

void WebViewWindow::keepOnScreen() {
    const auto current = platformFrame();
    if (!current) {
        return;
    }
    const auto area = workAreaAt(*current);
    if (!area) {
        return;
    }
    const Rect kept = keptWithin(*current, *area);
    if (kept.x != current->x || kept.y != current->y) {
        platformMove(kept.x, kept.y);
    }
}

Json WebViewWindow::chrome() const {
    return {
        {"title", title()},
        {"titleBar", options_.titleBar},
        {"resizable", options_.resizable && !options_.autosize},
    };
}

void WebViewWindow::setTitleBar(bool shown) {
    options_.titleBar = shown;
    emit("chrome", chrome());
}

void WebViewWindow::setSkipTaskbar(bool skip) {
    options_.skipTaskbar = skip;
    platformSetSkipTaskbar();
}

void WebViewWindow::beginMove() {
    platformBeginMove();
}

void WebViewWindow::beginResize(ResizeEdge edge) {
    platformBeginResize(edge);
}

void WebViewWindow::minimize() {
    platformMinimize();
}

void WebViewWindow::toggleMaximize() {
    platformToggleMaximize();
}

void WebViewWindow::close() {
    platformClose();
}

void WebViewWindow::setTitle(const std::string& title) {
    options_.title = title;
    platformSetTitle(title);
    emit("chrome", chrome());
}

void WebViewWindow::emit(const std::string& event, const Json& data) {
    runScript("window.gwlReceive(" + Json{{"event", event}, {"data", data}}.dump() + ")");
}

void WebViewWindow::openDevTools() {
    platformOpenDevTools();
}

void WebViewWindow::runScript(const std::string& script) {
    platformRunScript(script);
}

void WebViewWindow::receive(const std::string& text) {
    Request request;
    std::string error;
    if (!Request::parse(text, request, error)) {
        // A message with no id has nothing to answer, so the reason goes to stderr.
        std::fprintf(stderr, "Ignored a page message: %s\n", error.c_str());
        return;
    }
    const std::uint64_t id = request.id;
    Reply reply{
        [this, id](const Json& result) { runScript(replyScript(id, result)); },
        [this, id](const std::string& message) { runScript(errorScript(id, message)); },
    };
    handler_.dispatch(*this, request, reply);
}

bool WebViewWindow::requestClose() {
    return allowClose();
}

void WebViewWindow::notifyClosed() {
    onClosed();
}

void WebViewWindow::notifyFocusLost() {
    onFocusLost();
}

void setUiScale(int percent) {
    if (percent < 50 || percent > 200 || percent == uiScale) {
        return;
    }
    for (WebViewWindow* window : liveWindows()) {
        window->rememberSize();
    }
    uiScale = percent;
    for (WebViewWindow* window : liveWindows()) {
        window->applyScale();
        window->emit("uiScale", percent);
    }
}

int scaledSize(int size) {
    return (size * uiScale + 50) / 100;
}

int unscaledSize(int size) {
    return (size * 100 + uiScale / 2) / uiScale;
}

}  // namespace gwl
