using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GwLauncher.Ui;

// Connects the page to the platform. The page calls methods by name with JSON arguments and gets the result back under
// the same id. The bridge also pushes events to the page, for the progress, prompts and messages the C# side starts.
public sealed class LauncherBridge : IPrompts
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ILauncherHost _host;
    private readonly IShell _main;
    private readonly Func<Action, IShell> _createPopup;
    private readonly Dictionary<string, IShell> _popups = new();
    private readonly Dictionary<IShell, PopupRole> _roles = new();
    private IShell? _openMenu;
    private bool _menuPicked;
    private int _nextDialog;
    private System.Threading.Timer? _saveTimer;

    // Views whose windows, when closed, leave the account list alone: the prompts and the right-click menu.
    private static readonly HashSet<string> DialogViews = new() { "message", "choose", "progress", "menu", "update" };
    private readonly Dictionary<int, TaskCompletionSource<int?>> _prompts = new();
    private CancellationTokenSource? _progress;
    private int _nextPrompt;

    // createPopup makes an empty window for a view the user opens; it calls onClosed when that window goes away.
    public LauncherBridge(ILauncherHost host, IShell main, Func<Action, IShell> createPopup)
    {
        _host = host;
        _main = main;
        _createPopup = createPopup;
        main.Attach(json => Receive(json, main));
        main.PageSizeChanged += RememberListSize;
        main.PositionChanged += RememberListPosition;
        host.Prompts = this;
    }

    // The view the page opens on, when a desktop shortcut couldn't start its account.
    public object? Startup { get; set; }

    // Opens a view on the page from outside it, such as the tray menu's Settings item.
    public void Navigate(object view)
    {
        _main.Show();
        Send(new { @event = "navigate", data = view }, _main);
    }

    // Called by the shell with each message the page posts.
    public async void Receive(string json, IShell origin)
    {
        var id = 0;
        try
        {
            var request = JsonNode.Parse(json)!;
            id = (int)request["id"]!;
            var result = await Call((string)request["method"]!, request["args"] as JsonArray ?? new JsonArray(), origin);
            Send(new { id, result }, origin);
        }
        catch (Exception ex)
        {
            Send(new { id, error = ex.Message }, origin);
        }
    }

    // Each prompt opens in a window of its own. Prompts can come from background work, so windows are made on the UI thread.
    public void Message(string title, string text) =>
        _main.RunOnUi(() => OpenDialog("message", new { title, text }, null));

    public Task<int?> Choose(string title, string message, params string[] options)
    {
        var answer = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int id;
        lock (_prompts)
        {
            id = ++_nextPrompt;
            _prompts[id] = answer;
        }

        _main.RunOnUi(() => OpenDialog("choose", new { id, title, message, options }, () => Answer(id, null)));
        return answer.Task;
    }

    // The update prompt shows the release notes and a link, so the user can read the changes before deciding.
    public async Task<bool> ConfirmUpdate(string title, string message, string notes, string releaseUrl)
    {
        var answer = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int id;
        lock (_prompts)
        {
            id = ++_nextPrompt;
            _prompts[id] = answer;
        }

        _main.RunOnUi(() => OpenDialog("update", new { id, title, message, notes, releaseUrl }, () => Answer(id, null)));
        return (await answer.Task) == 1;
    }

    // Updates go to the progress window until the work ends. Closing that window cancels the work if it can be cancelled.
    public async Task<T?> Progress<T>(ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T>> work) where T : class
    {
        using var cancellation = new CancellationTokenSource();
        var finished = false;
        IShell? window = null;
        _main.RunOnUi(() => window = OpenDialog("progress", new
        {
            options.Title,
            options.Heading,
            options.Note,
            options.Cancellable,
            options.FailureText
        }, () =>
        {
            if (!Volatile.Read(ref finished) && options.Cancellable)
            {
                cancellation.Cancel();
            }
        }));

        void Update(object data) => _main.RunOnUi(() =>
        {
            if (window != null)
            {
                Send(new { @event = "progress", data }, window);
            }
        });

        _progress = cancellation;
        try
        {
            var result = await work(cancellation.Token, new ThrottledProgress(update =>
                Update(new { phase = "update", update.Fraction, update.Detail })));
            Volatile.Write(ref finished, true);
            Update(new { phase = "end" });
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Volatile.Write(ref finished, true);
            Update(new { phase = "end" });
            return null;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref finished, true);
            Update(new { phase = "failed", text = $"{options.FailureText}: {ex.Message}" });
            return null;
        }
        finally
        {
            _progress = null;
        }
    }

    private async Task<object?> Call(string method, JsonArray args, IShell origin)
    {
        switch (method)
        {
            case "state":
                return State();
            case "reloadAccounts":
                return _host.ReloadAccounts();
            case "launchDefault":
                return await _host.LaunchDefaultClient();
            case "openUrl":
                OpenUrl(Arg<string>(args, 0)!);
                return null;
            case "accounts":
                var entries = _host.Accounts(out var error).Select((entry, i) => entry with { State = StateOf(entry.Name, i) }).ToList();
                return new { entries, error };
            case "draft":
                return _host.Draft(Arg<int?>(args, 0));
            case "save":
                return _host.Save(Arg<int?>(args, 0), Arg<AccountDraft>(args, 1)!);
            case "remove":
                return _host.Remove(Arg<int>(args, 0));
            case "move":
                return _host.Move(Arg<int>(args, 0), Arg<int>(args, 1));
            case "launch":
                return await LaunchFlow.StartAsync(_host, Arg<int>(args, 0), Arg<bool>(args, 1));
            case "launchNow":
                return await LaunchFlow.Launch(_host, Arg<int>(args, 0));
            case "issues":
                var index = Arg<int>(args, 0);
                return _host.Accounts(out _)[index].Issues.Concat(await _host.VersionIssues(index)).ToList();
            case "fix":
                return await _host.Fix(Arg<int>(args, 0), Arg<AccountIssue>(args, 1)!);
            case "listSize":
                return new[] { _host.Settings.ListWidth, _host.Settings.ListHeight };
            case "icon":
                return WebAssets.IconDataUrl();
            case "encrypted":
                return _host.Encrypted;
            case "setMasterPassword":
                return _host.SetMasterPassword(Arg<string>(args, 0) ?? "");
            case "updateAll":
                return await _host.UpdateAllClients();
            case "message":
                Message(Arg<string>(args, 0)!, Arg<string>(args, 1)!);
                return null;
            case "choose":
                return await Choose(Arg<string>(args, 0)!, Arg<string>(args, 1)!, Arg<string[]>(args, 2) ?? Array.Empty<string>());
            case "window":
                return _roles.TryGetValue(origin, out var role) ? new { view = role.View, args = role.Args } : null;
            case "openWindow":
                OpenPopup(Arg<string>(args, 0)!, Arg<string>(args, 1)!, args.Count > 2 ? args[2]?.DeepClone() as JsonArray ?? new JsonArray() : new JsonArray());
                return null;
            case "openMenu":
                OpenMenu(args.Count > 0 && args[0]?.DeepClone() is JsonArray items ? items : new JsonArray());
                return null;
            case "menuPick":
                _menuPicked = true;
                Send(new { @event = "menuPick", data = Arg<int>(args, 0) }, _main);
                origin.Hide();
                return null;
            case "shortcut":
                return _host.CreateShortcut(Arg<int>(args, 0));
            case "pluginFiles":
                return _host.PluginFiles(Arg<AccountDraft>(args, 0)!);
            case "refreshMod":
                var mod = Arg<ModDraft>(args, 0)!;
                _host.RefreshMod(mod);
                return mod;
            case "resolveMod":
                return await _host.Resolve(Arg<ModDraft>(args, 0)!);
            case "pickFile":
                return await origin.PickFile(Arg<string>(args, 0)!, Arg<bool>(args, 1));
            case "saveSettings":
                SaveSettings(Arg<GlobalSettings>(args, 0)!);
                return null;
            case "unlock":
                return _host.Unlock(Arg<string>(args, 0)!, Arg<bool>(args, 1));
            case "setup":
                return _host.SetupItems();
            case "startSetup":
                return _host.StartSetup(Arg<string>(args, 0));
            case "setupStatus":
                return _host.SetupStatus();
            case "answer":
                Answer(Arg<int>(args, 0), Arg<int?>(args, 1));
                return null;
            case "cancel":
                _progress?.Cancel();
                return null;
            case "copy":
                origin.CopyText(Arg<string>(args, 0)!);
                return null;
            case "pin":
                origin.Pinned = Arg<bool>(args, 0);
                return null;
            case "resize":
                origin.Resize(Arg<int>(args, 0), Arg<int>(args, 1));
                return null;
            case "hide":
                origin.Hide();
                return null;
            case "quit":
                _main.Quit();
                return null;
            default:
                throw new InvalidOperationException($"Unknown method '{method}'.");
        }
    }

    private string StateOf(string name, int index) =>
        LaunchFlow.IsLaunching(name) ? "Launching" : _host.IsRunning(index) ? "Active" : "Inactive";

    // The build id, from the build component of the launcher's version. The entry assembly is the launcher's own.
    private static string LauncherVersion() =>
        $"build {(System.Reflection.Assembly.GetEntryAssembly() ?? typeof(LauncherBridge).Assembly).GetName().Version?.Build}";

    // The build and OS line, shown in the window footer and the tray tooltip.
    public static string VersionLine() => $"GW Launcher {LauncherVersion()} · {RuntimeInformation.OSDescription}";

    private object State()
    {
        var setup = _host.SetupItems();
        return new
        {
            _main.HasTray,
            _host.HasLinuxPaths,
            _host.CanCreateShortcuts,
            _host.HasWindowsOptions,
            _host.CanLaunchDefaultClient,
            _host.Locked,
            HasSetup = setup.Count > 0,
            SetupNeeded = setup.Any(item => !item.Done),
            _host.Settings,
            Version = VersionLine(),
            Startup
        };
    }

    // Copies the page's edits into the host's settings object, which is the one the platform reads.
    private void SaveSettings(GlobalSettings edited)
    {
        var settings = _host.Settings;
        settings.CheckForUpdates = edited.CheckForUpdates;
        settings.AutoUpdate = edited.AutoUpdate;
        settings.KeepInSystemTray = edited.KeepInSystemTray;
        settings.LaunchMinimized = edited.LaunchMinimized;
        settings.TimeoutOnModlaunch = edited.TimeoutOnModlaunch;
        _host.SaveSettings();
        _main.ApplyWindowMode();
    }

    // Opens a web link in the browser. Anything that isn't an http or https address is ignored.
    private static void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void Answer(int id, int? choice)
    {
        TaskCompletionSource<int?>? answer;
        lock (_prompts)
        {
            _prompts.Remove(id, out answer);
        }

        answer?.TrySetResult(choice);
    }

    private static T? Arg<T>(JsonArray args, int index) =>
        index < args.Count && args[index] is { } node ? node.Deserialize<T>(Json) : default;

    // Each view the user opens gets a window of its own. Asking for one that's already open brings it forward.
    private IShell OpenPopup(string key, string view, JsonArray args, Action? onClosed = null, bool menu = false)
    {
        if (_popups.TryGetValue(key, out var open))
        {
            open.Show();
            return open;
        }

        IShell? window = null;
        window = _createPopup(() => ClosePopup(key, window!));
        _popups[key] = window;
        _roles[window] = new PopupRole(view, args, onClosed);
        window.Attach(json => Receive(json, window));
        if (menu)
        {
            window.ShowMenu();
        }
        else
        {
            window.Show();
        }

        return window;
    }

    private IShell OpenDialog(string view, object data, Action? onClosed) =>
        OpenPopup($"{view}:{Interlocked.Increment(ref _nextDialog)}", view, new JsonArray(JsonSerializer.SerializeToNode(data, Json)), onClosed);

    // The right-click menu is its own borderless window, so it isn't clipped to the page. A new one replaces any open.
    private void OpenMenu(JsonArray items)
    {
        _openMenu?.Hide();
        _openMenu = OpenPopup($"menu:{Interlocked.Increment(ref _nextDialog)}", "menu", new JsonArray(items), menu: true);
    }

    // The account list may have changed while a view was open, so the main window reloads it. Prompts don't change anything.
    private void ClosePopup(string key, IShell window)
    {
        _popups.Remove(key);
        if (_openMenu == window)
        {
            _openMenu = null;
        }

        var isDialog = false;
        if (_roles.Remove(window, out var role))
        {
            isDialog = DialogViews.Contains(role.View);
            if (role.View == "menu")
            {
                // A menu dismissed by a click elsewhere takes the account list with it, as any other click away does.
                if (!_menuPicked)
                {
                    _main.HideUnlessPointerOver();
                }

                _menuPicked = false;
            }

            role.OnClosed?.Invoke();
        }

        if (!isDialog)
        {
            Send(new { @event = "refresh" }, _main);
        }
    }

    // The list's size is kept in memory at once and written after a short pause, so a drag saves once.
    private void RememberListSize(int width, int height)
    {
        _host.Settings.ListWidth = width;
        _host.Settings.ListHeight = height;
        ScheduleSave();
    }

    private void RememberListPosition(int x, int y)
    {
        _host.Settings.ListX = x;
        _host.Settings.ListY = y;
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _saveTimer?.Dispose();
        _saveTimer = new System.Threading.Timer(_ => _host.SaveSettings(), null, 500, Timeout.Infinite);
    }

    private static void Send(object message, IShell target) => target.Post(JsonSerializer.Serialize(message, Json));

    private sealed record PopupRole(string View, JsonArray Args, Action? OnClosed);

    // Downloads report thousands of updates; the page only needs a few a second.
    private sealed class ThrottledProgress(Action<ProgressUpdate> report) : IProgress<ProgressUpdate>
    {
        private long _lastTicks;

        public void Report(ProgressUpdate value)
        {
            var now = Environment.TickCount64;
            if (now - Interlocked.Read(ref _lastTicks) < 100)
            {
                return;
            }

            Interlocked.Exchange(ref _lastTicks, now);
            report(value);
        }
    }
}
