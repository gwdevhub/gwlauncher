using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GwLauncher.Ui;

// Same sections, descriptions and enabling rules on every platform.
public sealed class SettingsWindow : AppWindow
{
    private readonly ILauncherHost _host;
    private readonly CheckBox _checkForUpdates = new() { Content = "Check for updates" };
    private readonly CheckBox _autoUpdate = new() { Content = "Auto update" };
    private readonly CheckBox _keepInTray = new() { Content = "Keep in system tray" };
    private readonly CheckBox _launchMinimized = new() { Content = "Launch minimised" };
    private readonly TextBox _timeout = new() { Width = 90 };
    private readonly TextBlock _error = Widgets.Error();

    public SettingsWindow(ILauncherHost host)
    {
        _host = host;
        var settings = host.Settings;

        Title = "GW Launcher - Settings";
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanMinimize = false;
        CanMaximize = false;
        CanResize = false;

        _checkForUpdates.IsChecked = settings.CheckForUpdates;
        _autoUpdate.IsChecked = settings.AutoUpdate;
        _keepInTray.IsChecked = settings.KeepInSystemTray;
        _launchMinimized.IsChecked = settings.LaunchMinimized;
        _timeout.Text = settings.TimeoutOnModlaunch.ToString();

        _checkForUpdates.IsCheckedChanged += (_, _) =>
        {
            _autoUpdate.IsEnabled = _checkForUpdates.IsChecked == true;
            if (_checkForUpdates.IsChecked != true)
            {
                _autoUpdate.IsChecked = false;
            }
        };
        _keepInTray.IsCheckedChanged += (_, _) => _launchMinimized.IsEnabled = host.HasTray && _keepInTray.IsChecked == true;

        _autoUpdate.IsEnabled = _checkForUpdates.IsChecked == true;
        _keepInTray.IsEnabled = host.HasTray;
        _launchMinimized.IsEnabled = host.HasTray && _keepInTray.IsChecked == true;

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                Section("General"),
                Option(_keepInTray, host.HasTray
                    ? "Run from a tray icon; closing hides it to the tray."
                    : "Not available: no system tray was found on this desktop."),
                Option(_launchMinimized, "Start hidden in the tray instead of showing the window."),

                Section("Updates"),
                Option(_checkForUpdates, "Look for new launcher and Gw.exe versions at startup."),
                Option(_autoUpdate, "Install launcher updates without asking first."),

                Section("Advanced"),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { new TextBlock { Text = "Timeout on mod launch (ms):", VerticalAlignment = VerticalAlignment.Center }, _timeout }
                },
                Widgets.Text("How long to wait for a mod DLL to load into the game.", 11),

                _error,
                Widgets.Row(Widgets.Button("Cancel", () => Close(), 80), Widgets.Button("OK", Save, 80))
            }
        };
    }

    private static TextBlock Section(string title) => new() { Text = title, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) };

    private static StackPanel Option(CheckBox box, string description) => new()
    {
        Spacing = 2,
        Children =
        {
            box,
            new TextBlock { Text = description, FontSize = 11, Opacity = 0.7, Margin = new Thickness(24, 0, 0, 0), TextWrapping = TextWrapping.Wrap }
        }
    };

    private void Save()
    {
        if (!uint.TryParse(_timeout.Text, out var timeout) || timeout == 0)
        {
            _error.Text = "Enter the mod launch timeout as a whole number of milliseconds.";
            return;
        }

        var settings = _host.Settings;
        settings.CheckForUpdates = _checkForUpdates.IsChecked == true;
        settings.AutoUpdate = _autoUpdate.IsChecked == true;
        settings.KeepInSystemTray = _keepInTray.IsChecked == true;
        settings.LaunchMinimized = _launchMinimized.IsChecked == true;
        settings.TimeoutOnModlaunch = timeout;

        _host.SaveSettings();
        Close();
    }
}
