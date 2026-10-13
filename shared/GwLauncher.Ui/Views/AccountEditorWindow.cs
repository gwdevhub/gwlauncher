using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// Add or modify form. Fields follow the Windows AddAccountForm; Linux adds a path on this computer. Validation is the host's.
public sealed class AccountEditorWindow : AppWindow
{
    private readonly ILauncherHost _host;
    private readonly int? _index;
    private readonly Action _onSaved;
    private readonly AccountDraft _draft;
    private readonly TextBox _title = new();
    private readonly TextBox _email = new();
    private readonly TextBox _password = new() { PasswordChar = '*' };
    private readonly TextBox _character = new();
    private readonly TextBox _gwPath = new();
    private readonly TextBox _linuxPath = new();
    private readonly TextBox _extraArgs = new();
    private readonly CheckBox _steam = new() { Content = "Steam account" };
    private readonly CheckBox _elevated = new() { Content = "Run as administrator" };
    private readonly Button _modsButton;
    private readonly TextBlock _error = Widgets.Error();

    // Steam accounts log in through Steam, so their credential fields are hidden.
    private readonly List<Control> _credentials = new();

    public AccountEditorWindow(ILauncherHost host, int? index, Action onSaved)
    {
        _host = host;
        _index = index;
        _onSaved = onSaved;
        _draft = host.Draft(index);

        Title = index == null ? "Add Account" : "Modify Account";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        CanMinimize = false;
        CanMaximize = false;

        _title.Text = _draft.Title;
        _email.Text = _draft.Email;
        _password.Text = _draft.Password;
        _character.Text = _draft.Character;
        _gwPath.Text = _draft.GwPath;
        _linuxPath.Text = _draft.LinuxGwPath;
        _extraArgs.Text = _draft.ExtraArgs;
        _steam.IsChecked = _draft.SteamAccount;
        _elevated.IsChecked = _draft.Elevated;
        _steam.IsVisible = host.HasWindowsOptions;
        _elevated.IsVisible = host.HasWindowsOptions;
        _steam.IsCheckedChanged += (_, _) => UpdateCredentials();

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowSpacing = 8,
            ColumnSpacing = 8
        };
        var row = 0;
        AddRow(grid, row++, "Title:", _title, null, credential: false);
        AddRow(grid, row++, "Email:", _email, null, credential: true);
        AddRow(grid, row++, "Pass:", _password, null, credential: true);
        AddRow(grid, row++, "Char:", _character, null, credential: true);
        AddRow(grid, row++, host.HasLinuxPaths ? "Win path:" : "Path:", _gwPath, host.HasLinuxPaths ? null : Browse(() => _gwPath), credential: false);
        if (host.HasLinuxPaths)
        {
            AddRow(grid, row++, "Linux path:", _linuxPath, Browse(() => _linuxPath), credential: false);
        }

        AddRow(grid, row, "Ex Args:", _extraArgs, null, credential: false);
        row++;
        Grid.SetRow(_steam, row);
        Grid.SetColumnSpan(_steam, 3);
        grid.Children.Add(_steam);
        row++;
        Grid.SetRow(_elevated, row);
        Grid.SetColumnSpan(_elevated, 3);
        grid.Children.Add(_elevated);

        _modsButton = Widgets.Button("", OpenMods, 110);
        UpdateModsButton();
        UpdateCredentials();

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                grid,
                _error,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    Children =
                    {
                        WithColumn(_modsButton, 0),
                        WithColumn(Widgets.Row(Widgets.Button("Cancel", () => Close(), 80), Widgets.Button("Save", Save, 80)), 2)
                    }
                }
            }
        };
    }

    private Button Browse(Func<TextBox> target)
    {
        var browse = Widgets.Button("...", () => { }, 0);
        browse.Padding = new Thickness(8, 2);
        browse.Click += async (_, _) => await BrowseForGw(target());
        return browse;
    }

    private void AddRow(Grid grid, int row, string label, Control input, Control? trailing, bool credential)
    {
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(WithCell(text, row, 0));
        grid.Children.Add(WithCell(input, row, 1));
        if (trailing != null)
        {
            grid.Children.Add(WithCell(trailing, row, 2));
        }

        if (credential)
        {
            _credentials.Add(text);
            _credentials.Add(input);
        }
    }

    private void UpdateCredentials()
    {
        var steam = _steam.IsChecked == true;
        foreach (var control in _credentials)
        {
            control.IsVisible = !steam;
        }
    }

    private static T WithCell<T>(T control, int row, int column) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    private static T WithColumn<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private void OpenMods()
    {
        var window = new ModManagerWindow(_host, _draft);
        window.Closed += (_, _) => UpdateModsButton();
        window.ShowDialog(this);
    }

    // "Mods (3)", with a warning mark when an active mod can't be used.
    private void UpdateModsButton()
    {
        var broken = _draft.Mods.Any(mod => mod.Active && !mod.ReadOnly && mod.Problem != "");
        _modsButton.Content = $"Mods ({_draft.Mods.Count}){(broken ? " ⚠" : "")}";
    }

    private async Task BrowseForGw(TextBox target)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Gw.exe on this computer",
            AllowMultiple = false
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null)
        {
            target.Text = path;
        }
    }

    private void Save()
    {
        var error = _host.Save(_index, new AccountDraft
        {
            Title = _title.Text?.Trim() ?? "",
            Email = _email.Text?.Trim() ?? "",
            Password = _password.Text ?? "",
            Character = _character.Text?.Trim() ?? "",
            GwPath = _gwPath.Text?.Trim() ?? "",
            LinuxGwPath = _linuxPath.Text?.Trim() ?? "",
            ExtraArgs = _extraArgs.Text?.Trim() ?? "",
            UsePluginFolderMods = _draft.UsePluginFolderMods,
            SteamAccount = _steam.IsChecked == true,
            Elevated = _elevated.IsChecked == true,
            Mods = _draft.Mods
        });

        if (error != null)
        {
            _error.Text = error;
            return;
        }

        _onSaved();
        Close();
    }
}
