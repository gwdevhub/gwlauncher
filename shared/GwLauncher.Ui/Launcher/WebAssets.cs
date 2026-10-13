using System.Reflection;

namespace GwLauncher.Ui;

// The page, its stylesheet and script, and the helmet icon, all embedded in this assembly.
public static class WebAssets
{
    // index.html with the stylesheet and script inlined, so each platform loads one string with no file paths.
    public static string Page()
    {
        return Read("wwwroot/index.html")
            .Replace("<!-- styles -->", $"<style>{Read("wwwroot/app.css")}</style>")
            .Replace("<!-- script -->", $"<script>{Read("wwwroot/app.js")}</script>");
    }

    // Writes the helmet icon to a file, for the window icon, the tray and desktop shortcuts.
    public static string SaveIcon(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var source = Open("gwlauncher-helmet.png");
        using var target = File.Create(path);
        source.CopyTo(target);
        return path;
    }

    private static string Read(string name)
    {
        using var reader = new StreamReader(Open(name));
        return reader.ReadToEnd();
    }

    private static Stream Open(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"The {name} resource is missing from GwLauncher.Ui.");
}
