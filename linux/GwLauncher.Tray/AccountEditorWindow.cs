using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// Add/Modify form with the same fields as the Windows AddAccountForm, plus a Linux path beside the Windows one.
// The Windows path stays as Accounts.json has it; the Linux path goes to the sidecar file.
public sealed class AccountEditorWindow : AppWindow
{
    private static readonly System.Text.RegularExpressions.Regex WindowsPath = new(@"^[A-Za-z]:\\");

    private readonly LinuxAccount _original;
    private readonly Action<LinuxAccount> _onSave;

    // A copy, so Cancel leaves the account's mods unchanged.
    private readonly List<LinuxMod> _mods;

    private readonly TextBox _title = new();
    private readonly TextBox _email = new();
    private readonly TextBox _password = new() { PasswordChar = '*' };
    private readonly TextBox _character = new();
    private readonly TextBox _winPath = new();
    private readonly TextBox _linuxPath = new();
    private readonly TextBox _extraArgs = new();
    private bool _usePluginFolders;
    private Button? _modsButton;
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public AccountEditorWindow(LinuxAccount? existing, Action<LinuxAccount> onSave)
    {
        _original = existing ?? new LinuxAccount();
        _onSave = onSave;
        _mods = _original.Mods.Select(mod => mod.Clone()).ToList();

        Title = existing == null ? "Add Account" : "Modify Account";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 340;
        SizeToContent = SizeToContent.Height;
        CanMinimize = false;
        CanMaximize = false;

        _title.Text = _original.Title;
        _email.Text = _original.Email;
        _password.Text = _original.Password;
        _character.Text = _original.Character;
        _winPath.Text = _original.GwPath;
        _linuxPath.Text = _original.LinuxGwPath ?? "";
        _extraArgs.Text = _original.ExtraArgs;
        _usePluginFolders = _original.UsePluginFolderMods;

        var browse = new Button { Content = "...", Padding = new Thickness(8, 2) };
        browse.Click += async (_, _) => await BrowseForGw();

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 8,
            ColumnSpacing = 8
        };
        AddRow(grid, 0, "Title:", _title);
        AddRow(grid, 1, "Email:", _email);
        AddRow(grid, 2, "Pass:", _password);
        AddRow(grid, 3, "Char:", _character);
        AddRow(grid, 4, "Win path:", _winPath);
        AddRow(grid, 5, "Linux path:", _linuxPath, browse);
        AddRow(grid, 6, "Ex Args:", _extraArgs);

        _modsButton = new Button { MinWidth = 110, Padding = new Thickness(12, 6) };
        _modsButton.Click += (_, _) => OpenMods();
        UpdateModsButton();
        var mods = _modsButton;

        var save = new Button { Content = "Save", MinWidth = 80, Padding = new Thickness(12, 6) };
        save.Click += (_, _) => Save();
        var cancel = new Button { Content = "Cancel", MinWidth = 80, Padding = new Thickness(12, 6) };
        cancel.Click += (_, _) => Close();

        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, save } };
        Grid.SetColumn(mods, 0);
        Grid.SetColumn(actions, 2);
        buttons.Children.Add(mods);
        buttons.Children.Add(actions);

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children = { grid, _error, buttons }
        };
    }

    private void OpenMods()
    {
        var window = new ModManagerWindow(_mods, GwFolderHint(), _usePluginFolders, value => _usePluginFolders = value);
        window.Closed += (_, _) => UpdateModsButton();
        window.Show(this);
    }

    // "Mods (3)", with a warning mark when an active mod can't be used.
    private void UpdateModsButton()
    {
        if (_modsButton == null)
        {
            return;
        }

        var broken = _mods.Any(mod => mod.Active && !mod.IsAvailable);
        _modsButton.Content = $"Mods ({_mods.Count}){(broken ? " ⚠" : "")}";
    }

    private static void AddRow(Grid grid, int row, string label, Control input, Control? trailing = null)
    {
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 0);
        Grid.SetRow(input, row);
        Grid.SetColumn(input, 1);
        grid.Children.Add(text);
        grid.Children.Add(input);

        if (trailing != null)
        {
            Grid.SetRow(trailing, row);
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }
    }

    // The folder that holds Gw.exe, for finding the plugins folder. Prefers the Linux path.
    private string GwFolderHint()
    {
        var path = !string.IsNullOrWhiteSpace(_linuxPath.Text) ? _linuxPath.Text : _winPath.Text ?? "";
        return path.Trim();
    }

    private async Task BrowseForGw()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Gw.exe on this computer",
            AllowMultiple = false
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null)
        {
            _linuxPath.Text = path;
        }
    }

    private void Save()
    {
        var winPath = _winPath.Text?.Trim() ?? "";
        var linuxPath = _linuxPath.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(_email.Text))
        {
            _error.Text = "Enter the account's email.";
            return;
        }

        if (winPath == "" && linuxPath == "")
        {
            _error.Text = "Enter a path to Gw.exe.";
            return;
        }

        if (linuxPath != "" && !FileCheck.IsUsableFile(linuxPath))
        {
            _error.Text = "Gw.exe was not found at the Linux path.";
            return;
        }

        // With no Windows path, the Linux path is the account's path, as on a Linux-only account.
        var gwPath = winPath == "" ? linuxPath : winPath;
        var linuxGwPath = winPath == "" || linuxPath == "" ? null : linuxPath;

        if (linuxGwPath == null && !WindowsPath.IsMatch(gwPath) && !FileCheck.IsUsableFile(gwPath))
        {
            _error.Text = "Gw.exe was not found at that path.";
            return;
        }

        // Keep fields this form doesn't edit (elevated, ...) so saving never drops them.
        var account = new LinuxAccount
        {
            Title = _title.Text?.Trim() ?? "",
            Email = _email.Text.Trim(),
            Password = _password.Text ?? "",
            Character = _character.Text?.Trim() ?? "",
            GwPath = gwPath,
            LinuxGwPath = linuxGwPath,
            ExtraArgs = _extraArgs.Text?.Trim() ?? "",
            Mods = _mods,
            UsePluginFolderMods = _usePluginFolders,
            Extra = _original.Extra
        };

        _onSave(account);
        Close();
    }
}
