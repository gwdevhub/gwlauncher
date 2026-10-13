using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// Add or modify form. The fields match the Windows AddAccountForm; Linux adds a path on this computer. Validation is the host's.
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
    private readonly Button _modsButton;
    private readonly TextBlock _error = Widgets.Error();

    public AccountEditorWindow(ILauncherHost host, int? index, Action onSaved)
    {
        _host = host;
        _index = index;
        _onSaved = onSaved;
        _draft = host.Draft(index);

        Title = index == null ? "Add Account" : "Modify Account";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 340;
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

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 8,
            ColumnSpacing = 8
        };
        var rows = new List<(string Label, Control Input, Control? Trailing)>
        {
            ("Title:", _title, null),
            ("Email:", _email, null),
            ("Pass:", _password, null),
            ("Char:", _character, null),
            (host.HasLinuxPaths ? "Win path:" : "Path:", _gwPath, null)
        };
        if (host.HasLinuxPaths)
        {
            var browse = Widgets.Button("...", () => { }, 0);
            browse.Padding = new Thickness(8, 2);
            browse.Click += async (_, _) => await BrowseForGw();
            rows.Add(("Linux path:", _linuxPath, browse));
        }

        rows.Add(("Ex Args:", _extraArgs, null));
        for (var row = 0; row < rows.Count; row++)
        {
            AddRow(grid, row, rows[row].Label, rows[row].Input, rows[row].Trailing);
        }

        _modsButton = Widgets.Button("", OpenMods, 110);
        UpdateModsButton();

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

    private static void AddRow(Grid grid, int row, string label, Control input, Control? trailing)
    {
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(WithCell(text, row, 0));
        grid.Children.Add(WithCell(input, row, 1));
        if (trailing != null)
        {
            grid.Children.Add(WithCell(trailing, row, 2));
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
