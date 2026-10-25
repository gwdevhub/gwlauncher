#pragma once

#include <algorithm>
#include <functional>
#include <stdexcept>
#include <memory>
#include <optional>
#include <utility>
#include <string>
#include <vector>

#include "Geometry.h"
#include "Messages.h"

namespace gwl {

// The edges and corners a window can be resized from, in the order GDK's edges use.
enum class ResizeEdge { NorthWest, North, NorthEast, West, East, SouthWest, South, SouthEast };

struct WindowOptions {
    std::string title;
    int width = 300;
    int height = 340;
    // Every window is frameless: the page draws the title bar, and moves, resizes and closes the window through the calls below.
    bool titleBar = true;
    // Whether the page offers edges to resize the window from. An autosize window never has them.
    bool resizable = true;
    bool skipTaskbar = false;
    bool alwaysOnTop = false;
    bool visible = true;
    // The page sizes the window: its reports through fitToPage set the window's size, in device-independent pixels.
    bool autosize = false;
};

// The platform's window and web view. Each platform defines it in its own source file.
struct NativeWindow;

struct NativeWindowDeleter {
    void operator()(NativeWindow* native) const;
};

// A native window with a web view that shows a page and talks to it. Subclasses choose the page and the title.
class WebViewWindow {
public:
    explicit WebViewWindow(WindowOptions options);
    virtual ~WebViewWindow();
    WebViewWindow(const WebViewWindow&) = delete;
    WebViewWindow& operator=(const WebViewWindow&) = delete;

    // Creates the native window and loads the page. Call once, before runEventLoop.
    void open();

    void show();
    // Hides the window. Its web view is freed after a cooldown, unless the window is shown again first.
    void hide();
    // Brings the window to the front and gives it the focus. A hidden or minimised window is shown and restored first.
    void present();
    // The window's rectangle on the screen, when the platform can give it.
    std::optional<Rect> frame() const;
    // The window's size in the units resize takes, read back from the screen. Missing when the window isn't open.
    std::optional<std::pair<int, int>> frameUnscaled() const;

    // Whether the window is shown, as far as this layer knows: it's kept in step by open, show and hide.
    bool isVisible() const { return visible_; }
    void resize(int width, int height);
    // Sizes the window to the page's reported size, in device-independent pixels. Only for windows with autosize set.
    void fitToPage(int width, int height);
    // Records the window's unscaled size from the screen, before the scale changes.
    void rememberSize();
    // Sizes the window to its unscaled size at the current scale.
    void applyScale();
    void move(int x, int y);
    void setTitle(const std::string& title);

    // What the page draws around its view: the title, whether there's a title bar, and whether the edges resize the window.
    Json chrome() const;
    // Shows or hides the page's title bar. The page is told straight away.
    void setTitleBar(bool shown);
    // Keeps the window off the taskbar, or puts it back. Applies straight away when the window is open.
    void setSkipTaskbar(bool skip);
    // Start moving or resizing the window, following the pointer's button press on the page.
    void beginMove();
    void beginResize(ResizeEdge edge);
    void minimize();
    void toggleMaximize();
    // Asks the window to close, as the window manager's close would: allowClose decides, and a close hides the window.
    void close();

    // Sends an event to the page: window.gwlReceive({event, data}).
    void emit(const std::string& event, const Json& data);
    void runScript(const std::string& script);

    // Opens the page's developer tools.
    void openDevTools();

    MessageHandler& handler() { return handler_; }

    // The page to show: the inline HTML, or the URL when devUrl is set.
    virtual std::string html() const { return {}; }
    virtual std::string devUrl() const { return {}; }
    virtual std::string title() const { return options_.title; }

    // Called by the platform when the page posts a message, and when the user asks to close the window.
    void receive(const std::string& text);
    void reload();
    void keepAliveForRecovery() { ++cooldownGeneration_; }
    void requestClose();
    void notifyFocusLost();

protected:
    // Return false to keep the window open. A window that allows the close is hidden.
    virtual bool allowClose() { return true; }
    // Called when another window takes the focus from this one.
    virtual void onFocusLost() {}

private:
    // Each platform implements these in platform/<os>/WebViewWindow.cpp.
    void platformOpen();
    void platformUnload();
    // Starts the cooldown after which a hidden window frees its web view. A show or present cancels it.
    void startCooldown();
    void freeWebView();
    void platformShow(bool visible);
    void platformPresent();
    void platformResize(int width, int height);
    void platformMove(int x, int y);
    std::optional<Rect> platformFrame() const;
    void platformSetTitle(const std::string& title);
    // Applies options_.skipTaskbar, which is already updated when it runs.
    void platformSetSkipTaskbar();
    void platformBeginMove();
    void platformBeginResize(ResizeEdge edge);
    void platformMinimize();
    void platformToggleMaximize();
    void platformClose();
    void platformRunScript(const std::string& script, bool requiresReady = false);
    void platformPageReady();
    void platformFailure(const std::string& message);
    void platformOpenDevTools();
    // Converts a size on the screen to the device-independent pixels resize takes, before the UI scale is removed.
    int platformToIndependent(int pixels) const;
    // Moves the window back inside its monitor's work area when it has gone past an edge.
    void keepOnScreen();

    WindowOptions options_;
    // The window's size without the UI scale, which is what the page and the settings work in.
    int baseWidth_ = 0;
    int baseHeight_ = 0;
    bool visible_ = false;
    bool opened_ = false;
    // A long-running operation may finish after this window has opened a different page.
    std::uint64_t pageGeneration_ = 0;
    Json progressSnapshot_;
    // Counts the hides and shows, so a cooldown started by an earlier hide does nothing once the window is shown again.
    std::uint64_t cooldownGeneration_ = 0;
    // Expires with the window, so a cooldown still waiting when the window is destroyed does nothing.
    std::shared_ptr<bool> alive_ = std::make_shared<bool>(true);
    MessageHandler handler_;
    std::unique_ptr<NativeWindow, NativeWindowDeleter> native_;
};

// Thrown when a runtime the windows need is missing. The user has already been shown why in a dialog, so the caller only stops.
class RuntimeMissingError : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};

// What's missing for the windows to open: a message for the user, and a web page that gets it (empty when there's none).
// Nothing when it's all there. GTK 3, WebKitGTK 4.1 and a display on Linux; the WebView2 runtime on Windows.
struct RuntimeProblem {
    std::string message;
    std::string link;
};
std::optional<RuntimeProblem> missingWindowingRuntime();

// Asks the user in a dialog whether to open the page at link, and opens it when they say yes. Without a link, shows the message.
void offerLink(const std::string& message, const std::string& link);

// Shows what's missing in a dialog and throws RuntimeMissingError, when something is missing. Each window checks before it opens.
void requireWindowingRuntime();

// Starts the platform's windowing, so that windows and menus can be made. Throws RuntimeMissingError when a runtime is missing.
void initializeWindowing();

// Sets the icon every window of the launcher shows, from the PNG the launcher embeds. Windows takes its icon from the
// executable's resource instead, so there it does nothing.
void setApplicationIcon(const std::string& png);

// The platform's event loop. runEventLoop returns once stopEventLoop has been called.
void runEventLoop();
void stopEventLoop();

// Shows an error in a dialog, and writes it to stderr. Used where nothing else would show the failure.
void showError(const std::string& message);

// The file the user picks in an open dialog: a Gw.exe, or a mod file when modsOnly. Empty when they cancel.
// The file the user picks, or nothing. startFolder, when it's given, is the folder the dialog opens in.
std::optional<std::string> pickFile(const std::string& title, bool modsOnly, const std::string& startFolder = {});

// The Windows or Linux account folder the user installed Guild Wars into, when one is found.
std::optional<std::string> defaultInstall();

// Runs a task on the event loop's thread, from any thread.
void runOnUiThread(std::function<void()> task);

// The UI scale in percent. Window sizes are given unscaled: resize and the opened size multiply them by it.
void setUiScale(int percent);
int scaledSize(int size);
int unscaledSize(int size);

// The screen's size in pixels, when the platform can give it.
std::optional<std::pair<int, int>> screenSize();

// The part of the screen the panels leave free, when the platform can give it.
std::optional<Rect> workArea();

// The work area of the monitor the rectangle is on, when the platform can tell which monitor that is. Missing otherwise, and
// windows are then left where they are.
std::optional<Rect> workAreaAt(const Rect& rect);

// The mouse position on screen, when the platform can give it.
std::optional<std::pair<int, int>> cursorPosition();

}  // namespace gwl
