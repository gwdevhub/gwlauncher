namespace GwLauncher.Ui;

// The platform's window around the page: a WebView2 form on Windows, a WebKitGTK window on Linux.
public interface IShell
{
    bool HasTray { get; }

    // While set, losing focus doesn't hide the window, because the page has a form or prompt open.
    bool Pinned { set; }

    // Creates the page in this window and hands each message the page posts to onMessage. Called once, before Post.
    void Attach(Action<string> onMessage);

    // Delivers a message to the page. Safe to call from any thread.
    void Post(string json);

    // Runs work on the window's UI thread, so a window can be made from background work.
    void RunOnUi(Action work);

    // Shows the window beside the tray icon, or centred without a tray.
    void Show();

    // Shows a borderless window at the pointer once the page has sized it. It closes when the user clicks away.
    void ShowMenu();

    // Applies the tray setting to the main window: tray icon, frame and placement. Called when the setting changes.
    void ApplyWindowMode();
    void Hide();

    // Hides a tray flyout that lost focus, unless the pointer is over it.
    void HideUnlessPointerOver();
    void Quit();
    void Resize(int width, int height);

    // Raised when the user resizes the account list, so its size can be remembered.
    event Action<int, int>? PageSizeChanged;

    // Raised when the user moves the account list outside tray mode, so its place can be remembered.
    event Action<int, int>? PositionChanged;
    void CopyText(string text);
    Task<string?> PickFile(string title, bool modsOnly);
}
