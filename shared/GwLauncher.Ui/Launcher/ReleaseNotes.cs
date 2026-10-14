using System.Text.RegularExpressions;

namespace GwLauncher.Ui;

// Release notes are markdown, which the page doesn't render, so headings, bullets and bold are flattened to plain text.
public static class ReleaseNotes
{
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "No release notes available.";
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.TrimEnd())
            .Select(line => Regex.Replace(line, @"^#+\s*", ""))
            .Select(line => Regex.Replace(line, @"^\s*[-*]\s+", "  • "))
            .Select(line => Regex.Replace(line, @"\*\*(.+?)\*\*", "$1"));
        return Regex.Replace(string.Join("\n", lines), @"\n{3,}", "\n\n").Trim();
    }
}
