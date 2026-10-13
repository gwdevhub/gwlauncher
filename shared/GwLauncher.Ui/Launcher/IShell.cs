namespace GwLauncher.Ui;

// The platform's window around the page: a WebView2 form on Windows, a WebKitGTK window on Linux.
public interface IShell
{
    bool HasTray { get; }

    // While set, losing focus doesn't hide the window, because the page has a form or prompt open.
    bool Pinned { set; }

    // Delivers a message to the page. Safe to call from any thread.
    void Post(string json);

    // Shows the window beside the tray icon, or centred without a tray.
    void Show();
    void Hide();
    void Quit();
    void Resize(int width, int height);
    void CopyText(string text);
    Task<string?> PickFile(string title, bool modsOnly);
}
