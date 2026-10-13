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
    private readonly IShell _shell;
    private readonly Dictionary<int, TaskCompletionSource<int?>> _prompts = new();
    private CancellationTokenSource? _progress;
    private int _nextPrompt;

    public LauncherBridge(ILauncherHost host, IShell shell)
    {
        _host = host;
        _shell = shell;
        host.Prompts = this;
    }

    // The view the page opens on, when a desktop shortcut couldn't start its account.
    public object? Startup { get; set; }

    // Opens a view on the page from outside it, such as the tray menu's Settings item.
    public void Navigate(object view)
    {
        _shell.Show();
        Emit("navigate", view);
    }

    // Called by the shell with each message the page posts.
    public async void Receive(string json)
    {
        var id = 0;
        try
        {
            var request = JsonNode.Parse(json)!;
            id = (int)request["id"]!;
            var result = await Call((string)request["method"]!, request["args"] as JsonArray ?? new JsonArray());
            Send(new { id, result });
        }
        catch (Exception ex)
        {
            Send(new { id, error = ex.Message });
        }
    }

    public void Message(string title, string text)
    {
        _shell.Show();
        Emit("message", new { title, text });
    }

    public Task<int?> Choose(string title, string message, params string[] options)
    {
        var answer = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int id;
        lock (_prompts)
        {
            id = ++_nextPrompt;
            _prompts[id] = answer;
        }

        _shell.Show();
        Emit("choose", new { id, title, message, options });
        return answer.Task;
    }

    public async Task<T?> Progress<T>(ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T>> work) where T : class
    {
        using var cancellation = new CancellationTokenSource();
        _progress = cancellation;
        _shell.Show();
        Emit("progress", new { phase = "start", options.Title, options.Heading, options.Note, options.Cancellable });
        try
        {
            var result = await work(cancellation.Token, new ThrottledProgress(update =>
                Emit("progress", new { phase = "update", update.Fraction, update.Detail })));
            Emit("progress", new { phase = "end" });
            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Emit("progress", new { phase = "end" });
            return null;
        }
        catch (Exception ex)
        {
            Emit("progress", new { phase = "failed", text = $"{options.FailureText}: {ex.Message}" });
            return null;
        }
        finally
        {
            _progress = null;
        }
    }

    private async Task<object?> Call(string method, JsonArray args)
    {
        switch (method)
        {
            case "state":
                return State();
            case "accounts":
                var entries = _host.Accounts(out var error);
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
                return await LaunchFlow.StartAsync(_host, Arg<int>(args, 0));
            case "launchNow":
                return await _host.Launch(Arg<int>(args, 0));
            case "issues":
                var index = Arg<int>(args, 0);
                return _host.Accounts(out _)[index].Issues.Concat(await _host.VersionIssues(index)).ToList();
            case "fix":
                return await _host.Fix(Arg<int>(args, 0), Arg<AccountIssue>(args, 1)!);
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
                return await _shell.PickFile(Arg<string>(args, 0)!, Arg<bool>(args, 1));
            case "saveSettings":
                SaveSettings(Arg<GlobalSettings>(args, 0)!);
                return null;
            case "unlock":
                return _host.Unlock(Arg<string>(args, 0)!, Arg<bool>(args, 1));
            case "setup":
                return _host.SetupItems();
            case "startSetup":
                return _host.StartSetup();
            case "setupStatus":
                return _host.SetupStatus();
            case "answer":
                Answer(Arg<int>(args, 0), Arg<int?>(args, 1));
                return null;
            case "cancel":
                _progress?.Cancel();
                return null;
            case "copy":
                _shell.CopyText(Arg<string>(args, 0)!);
                return null;
            case "pin":
                _shell.Pinned = Arg<bool>(args, 0);
                return null;
            case "resize":
                _shell.Resize(Arg<int>(args, 0), Arg<int>(args, 1));
                return null;
            case "hide":
                _shell.Hide();
                return null;
            case "quit":
                _shell.Quit();
                return null;
            default:
                throw new InvalidOperationException($"Unknown method '{method}'.");
        }
    }

    private object State()
    {
        var setup = _host.SetupItems();
        return new
        {
            _shell.HasTray,
            _host.HasLinuxPaths,
            _host.CanCreateShortcuts,
            _host.HasWindowsOptions,
            _host.Locked,
            HasSetup = setup.Count > 0,
            SetupNeeded = setup.Any(item => !item.Done),
            _host.Settings,
            Version = $"GW Launcher {typeof(LauncherBridge).Assembly.GetName().Version?.ToString(3) ?? "unknown"} · {RuntimeInformation.OSDescription}",
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

    private void Emit(string name, object? data) => Send(new { @event = name, data });

    private void Send(object message) => _shell.Post(JsonSerializer.Serialize(message, Json));

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
