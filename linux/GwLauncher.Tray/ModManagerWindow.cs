using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// Mods for one account. Edits the list it's given, which the account editor saves with the account.
public sealed class ModManagerWindow : AppWindow
{
    private readonly List<LinuxMod> _mods;
    private readonly string _gwFolderHint;
    private bool _usePluginFolders;
    private readonly Action<bool> _onPluginsChanged;
    private readonly CheckBox _pluginsSwitch = new() { Content = "Use plugin folder mods" };
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };

    public ModManagerWindow(List<LinuxMod> mods, string gwFolderHint, bool usePluginFolders, Action<bool> onPluginsChanged)
    {
        _mods = mods;
        _gwFolderHint = gwFolderHint;
        _usePluginFolders = usePluginFolders;
        _onPluginsChanged = onPluginsChanged;
        _pluginsSwitch.IsChecked = usePluginFolders;
        _pluginsSwitch.IsCheckedChanged += (_, _) =>
        {
            _usePluginFolders = _pluginsSwitch.IsChecked == true;
            _onPluginsChanged(_usePluginFolders);
            Refresh();
        };

        Title = "Mod Manager";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 400;
        Height = 360;
        MinWidth = 340;
        MinHeight = 240;

        _list.ItemTemplate = new FuncDataTemplate<ModRow>((row, _) => row == null ? null : BuildRow(row));

        var add = new Button { Content = "Add...", MinWidth = 90, Padding = new Thickness(12, 6) };
        add.Click += async (_, _) => await AddMod();

        var remove = new Button { Content = "Remove Selected", Padding = new Thickness(12, 6) };
        remove.Click += (_, _) => RemoveSelected();

        var close = new Button { Content = "Close", MinWidth = 90, Padding = new Thickness(12, 6) };
        close.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { add, remove, close }
        };

        Grid.SetRow(_pluginsSwitch, 0);
        Grid.SetRow(_list, 1);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(12), RowSpacing = 8 };
        layout.Children.Add(_pluginsSwitch);
        layout.Children.Add(_list);
        Grid.SetRow(_message, 2);
        layout.Children.Add(_message);
        Grid.SetRow(buttons, 3);
        layout.Children.Add(buttons);
        Content = layout;

        Refresh();
    }

    private void Refresh()
    {
        var rows = _mods.Select(mod => new ModRow(
                mod,
                mod.FileName,
                mod.Type,
                ReadOnly: false,
                Problem: ProblemFor(mod)))
            .ToList();

        // Plugin-folder files can't be edited here, as on Windows. Accounts with the plugins switch off don't see them.
        var pluginFiles = _usePluginFolders ? LinuxMod.PluginFolderFiles(_gwFolderHint) : new List<PluginFile>();
        rows.AddRange(pluginFiles.Select(plugin => new ModRow(
            null,
            plugin.SourcePath,
            plugin.Type,
            ReadOnly: true,
            Problem: PluginProblem(plugin),
            Target: plugin.TargetPath)));

        _list.ItemsSource = rows;
    }

    // Problems are judged on the path the mod will actually load from: its Linux path if one is set.
    private static string? PluginProblem(PluginFile plugin)
    {
        if (plugin.TargetPath == null)
        {
            return "Shortcut target couldn't be found on this computer";
        }

        return FileCheck.IsUsableFile(plugin.TargetPath) ? null : UnusableFileMessage(plugin.TargetPath);
    }

    // The reason a file can't be used, as a sentence. Compressed files also point at the Resolve button.
    private static string UnusableFileMessage(string path)
    {
        var reason = ReparseInspector.UnusableReason(path);
        var sentence = char.ToUpperInvariant(reason[0]) + reason[1..];
        return ReparseInspector.IsWofStub(path) ? sentence + " Use Resolve to decompress it." : sentence;
    }

    private static string? ProblemFor(LinuxMod mod)
    {
        if (mod.IsWindowsPath)
        {
            return "Windows path with no Linux path. Use Locate to choose the file here.";
        }

        return FileCheck.IsUsableFile(mod.EffectivePath) ? null : UnusableFileMessage(mod.EffectivePath);
    }

    private Control BuildRow(ModRow row)
    {
        var check = new CheckBox
        {
            IsChecked = row.Mod?.Active ?? true,
            IsEnabled = !row.ReadOnly,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (row.Mod != null)
        {
            var mod = row.Mod;
            check.IsCheckedChanged += (_, _) => mod.Active = check.IsChecked == true;
        }

        var name = new TextBlock
        {
            // Split on both separators, since Windows paths from another machine use backslashes.
            Text = $"{row.FileName.Split('\\', '/').Last()}  [{(row.Type == ModType.kModTypeDLL ? "DLL" : "Texmod")}]",
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = row.Problem == null ? 1 : 0.5
        };

        var details = new StackPanel { Spacing = 2, Children = { name } };

        // A Linux-path mod has nothing to set, so the Linux line only appears for Windows-path mods or once one is set.
        if (row.Mod != null && (row.Mod.IsWindowsPath || row.Mod.LinuxPath != null))
        {
            details.Children.Add(new TextBlock
            {
                Text = $"Linux: {row.Mod.LinuxPath ?? "not set"}",
                FontSize = 11,
                Opacity = 0.6,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        if (row.Problem != null)
        {
            details.Children.Add(new TextBlock
            {
                Text = row.Problem,
                Foreground = Brushes.Firebrick,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (row.ReadOnly)
        {
            details.Children.Add(new TextBlock { Text = "From plugins folder (read-only)", FontSize = 11, Opacity = 0.6 });
        }

        if (row.ReadOnly && Path.GetExtension(row.FileName).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            details.Children.Add(new TextBlock
            {
                Text = $"Shortcut to: {row.Target ?? "unknown"}",
                FontSize = 11,
                Opacity = 0.6,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var path = row.Mod?.EffectivePath ?? row.Target ?? row.FileName;
        if (row.Problem != null && ReparseInspector.IsWofStub(path))
        {
            var resolve = new Button { Content = "Resolve…", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4) };
            resolve.Click += async (_, _) =>
            {
                resolve.IsEnabled = false;
                await ResolveWindow.RunAsync(this, path);
                Refresh();
            };
            details.Children.Add(resolve);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(check, 0);
        Grid.SetColumn(details, 1);
        grid.Children.Add(check);
        grid.Children.Add(details);

        if (row.Mod != null && (row.Mod.IsWindowsPath || row.Mod.LinuxPath != null))
        {
            var mod = row.Mod;
            var locate = new Button { Content = "Locate...", Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center };
            locate.Click += async (_, _) => await LocateFor(mod);
            Grid.SetColumn(locate, 2);
            grid.Children.Add(locate);
        }

        ToolTip.SetTip(grid, row.FileName);
        return grid;
    }

    private async Task LocateFor(LinuxMod mod)
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

        // Only the Linux path changes; the Windows FileName stays as Accounts.json has it.
        mod.LinuxPath = path;
        _message.Text = "";
        Refresh();
    }

    private async Task AddMod()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add mod",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Mods") { Patterns = new[] { "*.dll", "*.tpf", "*.zip" } }
            }
        });

        foreach (var path in files.Select(file => file.TryGetLocalPath()).OfType<string>())
        {
            if (_mods.Any(mod => mod.FileName.Equals(path, StringComparison.Ordinal)))
            {
                continue;
            }

            _mods.Add(new LinuxMod { FileName = path, Type = LinuxMod.TypeForPath(path), Active = true });
        }

        Refresh();
    }

    private void RemoveSelected()
    {
        var selected = _list.SelectedItems?.OfType<ModRow>().Where(row => !row.ReadOnly).ToList() ?? new List<ModRow>();
        if (selected.Count == 0)
        {
            _message.Text = "Select account mods to remove. Plugin-folder files can only be removed from the folder.";
            return;
        }

        foreach (var row in selected)
        {
            _mods.Remove(row.Mod!);
        }

        _message.Text = "";
        Refresh();
    }

    private sealed record ModRow(LinuxMod? Mod, string FileName, ModType Type, bool ReadOnly, string? Problem, string? Target = null);
}
