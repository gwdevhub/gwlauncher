using System.Text.RegularExpressions;

namespace GwLauncher.Linux;

// Converts Windows paths from Accounts.json and shortcuts to Linux paths, and back for Wine.
public static partial class WindowsPaths
{
    // Drives are matched to mounts by the letter the label starts with: C-NVMe-Windows is C:, K-Internal-SSD is K:.
    public static string? ToLinux(string windowsPath)
    {
        var match = DrivePattern().Match(windowsPath);
        if (!match.Success)
        {
            return null;
        }

        var letter = char.ToUpperInvariant(match.Groups[1].Value[0]);
        var rest = match.Groups[2].Value.Replace('\\', '/').TrimStart('/');

        foreach (var root in MountCandidates(letter))
        {
            var candidate = Path.Combine(root, rest);
            if (FileCheck.IsUsableFile(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    // Wine maps the Linux root to Z:, so any Linux path can be given to a Wine process in Windows form.
    public static string ToWine(string linuxPath)
    {
        return "Z:" + linuxPath.Replace('/', '\\');
    }

    public static bool IsWindowsPath(string path) => DrivePattern().IsMatch(path);

    private static IEnumerable<string> MountCandidates(char letter)
    {
        var parents = new[] { Path.Combine("/media", Environment.UserName), "/mnt" };

        return parents
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Where(dir => Path.GetFileName(dir).StartsWith($"{letter}-", StringComparison.OrdinalIgnoreCase) ||
                          Path.GetFileName(dir).Equals(letter.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^([A-Za-z]):[\\/]?(.*)$")]
    private static partial Regex DrivePattern();
}
