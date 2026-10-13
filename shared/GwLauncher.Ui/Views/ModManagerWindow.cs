using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GW_Launcher.Launcher;
using GW_Launcher.Utilities;

namespace GwLauncher.Ui;

// Mods for one account. Edits the draft's mods, and lists plugin-folder files read-only when that setting is on.
public sealed class ModManagerWindow : AppWindow
{
    private readonly ILauncherHost _host;
    private readonly AccountDraft _draft;
    private readonly CheckBox _pluginsSwitch = new() { Content = "Use plugin folder mods" };
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBlock _message = Widgets.Error();

    public ModManagerWindow(ILauncherHost host, AccountDraft draft)
    {
        _host = host;
        _draft = draft;
        _pluginsSwitch.IsChecked = draft.UsePluginFolderMods;
        _pluginsSwitch.IsCheckedChanged += (_, _) =>
        {
            _draft.UsePluginFolderMods = _pluginsSwitch.IsChecked == true;
            Refresh();
        };

        Title = "Mod Manager";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 400;
        Height = 360;
        MinWidth = 340;
        MinHeight = 240;

        _list.ItemTemplate = new FuncDataTemplate<ModDraft>((mod, _) => mod == null ? null : BuildRow(mod));

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(12), RowSpacing = 8 };
        Grid.SetRow(_pluginsSwitch, 0);
        Grid.SetRow(_list, 1);
        Grid.SetRow(_message, 2);
        var buttons = Widgets.Row(
            Widgets.Button("Add...", AddMod, 90),
            Widgets.Button("Remove Selected", RemoveSelected, 0),
            Widgets.Button("Close", () => Close(), 90));
        Grid.SetRow(buttons, 3);
        layout.Children.Add(_pluginsSwitch);
        layout.Children.Add(_list);
        layout.Children.Add(_message);
        layout.Children.Add(buttons);
        Content = layout;

        Refresh();
    }

    private void Refresh()
    {
        var rows = _draft.Mods.ToList();

        // Plugin-folder files can't be edited here, as on Windows. Accounts with the plugins switch off don't see them.
        if (_draft.UsePluginFolderMods)
        {
            rows.AddRange(_host.PluginFiles(_draft));
        }

        _list.ItemsSource = rows;
    }

    private Control BuildRow(ModDraft mod)
    {
        var check = new CheckBox
        {
            IsChecked = mod.Active,
            IsEnabled = !mod.ReadOnly,
            VerticalAlignment = VerticalAlignment.Center
        };
        check.IsCheckedChanged += (_, _) => mod.Active = check.IsChecked == true;

        var details = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    // Split on both separators, since Windows paths from another machine use backslashes.
                    Text = $"{mod.FileName.Split('\\', '/').Last()}  [{(mod.Type == ModType.kModTypeDLL ? "DLL" : "Texmod")}]",
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Opacity = mod.Problem == "" ? 1 : 0.5
                }
            }
        };

        if (mod.CanLocate)
        {
            details.Children.Add(Widgets.Text($"Linux: {(mod.LinuxPath == "" ? "not set" : mod.LinuxPath)}", 11));
        }

        if (mod.ReadOnly)
        {
            details.Children.Add(Widgets.Text("From plugins folder (read-only)", 11));
        }

        if (mod.ReadOnly && Path.GetExtension(mod.FileName).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            details.Children.Add(Widgets.Text($"Shortcut to: {mod.Target ?? "unknown"}", 11));
        }

        if (mod.Problem != "")
        {
            details.Children.Add(new TextBlock { Text = mod.Problem, Foreground = Brushes.Firebrick, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        }

        if (mod.CanResolve)
        {
            var resolve = Widgets.Button("Resolve…", () => { }, 0);
            resolve.HorizontalAlignment = HorizontalAlignment.Left;
            resolve.Padding = new Thickness(10, 4);
            resolve.Click += async (_, _) =>
            {
                resolve.IsEnabled = false;
                await _host.Resolve(this, mod);
                Refresh();
            };
            details.Children.Add(resolve);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(check, 0);
        Grid.SetColumn(details, 1);
        grid.Children.Add(check);
        grid.Children.Add(details);

        if (mod.CanLocate)
        {
            var locate = Widgets.Button("Locate...", () => { }, 0);
            locate.Padding = new Thickness(8, 2);
            locate.VerticalAlignment = VerticalAlignment.Center;
            locate.Click += async (_, _) => await LocateFor(mod);
            Grid.SetColumn(locate, 2);
            grid.Children.Add(locate);
        }

        ToolTip.SetTip(grid, mod.FileName);
        return grid;
    }

    private async Task LocateFor(ModDraft mod)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Locate {mod.FileName.Split('\\', '/').Last()} on this computer",
            AllowMultiple = false
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path == null)
        {
            return;
        }

        // Only the local path changes; the FileName stays as the account has it.
        mod.LinuxPath = path;
        _host.RefreshMod(mod);
        _message.Text = "";
        Refresh();
    }

    private async void AddMod()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add mod",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Mods") { Patterns = new[] { "*.dll", "*.tpf", "*.zip" } } }
        });

        foreach (var path in files.Select(file => file.TryGetLocalPath()).OfType<string>())
        {
            if (_draft.Mods.Any(mod => mod.FileName.Equals(path, StringComparison.Ordinal)))
            {
                continue;
            }

            var mod = new ModDraft { FileName = path, Type = PluginScanner.TypeForPath(path), Active = true };
            _host.RefreshMod(mod);
            _draft.Mods.Add(mod);
        }

        Refresh();
    }

    private void RemoveSelected()
    {
        var selected = _list.SelectedItems?.OfType<ModDraft>().Where(mod => !mod.ReadOnly).ToList() ?? new List<ModDraft>();
        if (selected.Count == 0)
        {
            _message.Text = "Select account mods to remove. Plugin-folder files can only be removed from the folder.";
            return;
        }

        foreach (var mod in selected)
        {
            _draft.Mods.Remove(mod);
        }

        _message.Text = "";
        Refresh();
    }
}
