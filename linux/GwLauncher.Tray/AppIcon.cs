using Avalonia.Controls;
using Avalonia.Platform;

namespace GwLauncher.Tray;

// The helmet artwork the Windows launcher uses, shared by the window icons and the desktop shortcut.
public static class AppIcon
{
    private static readonly Uri Asset = new("avares://GW_Launcher/Assets/gwlauncher-helmet.png");

    public static WindowIcon Window() => new(AssetLoader.Open(Asset));

    public static void SaveTo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var source = AssetLoader.Open(Asset);
        using var target = File.Create(path);
        source.CopyTo(target);
    }
}
