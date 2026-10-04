namespace GW_Launcher.Forms;

public partial class UpdatePromptForm : Form
{
    private readonly string _releaseUrl;

    public UpdatePromptForm(string message, string changelogMarkdown, string releaseUrl)
    {
        InitializeComponent();
        _releaseUrl = releaseUrl;
        labelMessage.Text = message;
        textBoxChangelog.Text = MarkdownToPlainText(changelogMarkdown);
        linkLabelRelease.Enabled = !string.IsNullOrEmpty(releaseUrl);
        // Match the old MessageBox default: Enter without an explicit choice declines the update.
        ActiveControl = buttonNotNow;
    }

    // Release notes are markdown a TextBox can't render, so flatten headings, bullets and bold into plain text.
    private static string MarkdownToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return "No release notes available.";

        var lines = markdown.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.TrimEnd())
            .Select(l => Regex.Replace(l, @"^#+\s*", ""))
            .Select(l => Regex.Replace(l, @"^\s*[-*]\s+", "  • "))
            .Select(l => Regex.Replace(l, @"\*\*(.+?)\*\*", "$1"));
        var text = string.Join("\n", lines);
        text = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
        return text.Replace("\n", Environment.NewLine);
    }

    private void TextBoxChangelog_Enter(object sender, EventArgs e)
    {
        // A focused read-only TextBox selects all its text; clear that so the notes read normally.
        textBoxChangelog.SelectionLength = 0;
    }

    private void LinkLabelRelease_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open release page: {ex.Message}");
        }
    }
}
