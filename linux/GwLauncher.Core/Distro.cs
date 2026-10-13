namespace GwLauncher.Linux;

// Picks the package-manager command for this distro from /etc/os-release, so installs work on the common families.
public static class Distro
{
    public static string? InstallCommand(string debian, string fedora, string arch, string suse)
    {
        var ids = ReadOsReleaseIds();
        if (ids.Contains("debian") || ids.Contains("ubuntu"))
        {
            return debian;
        }

        if (ids.Contains("fedora") || ids.Contains("rhel"))
        {
            return fedora;
        }

        if (ids.Contains("arch"))
        {
            return arch;
        }

        if (ids.Contains("suse") || ids.Contains("opensuse") || ids.Contains("opensuse-tumbleweed"))
        {
            return suse;
        }

        return null;
    }

    private static HashSet<string> ReadOsReleaseIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists("/etc/os-release"))
        {
            return ids;
        }

        foreach (var line in File.ReadAllLines("/etc/os-release"))
        {
            if (line.StartsWith("ID=") || line.StartsWith("ID_LIKE="))
            {
                foreach (var id in line[(line.IndexOf('=') + 1)..].Trim('"').Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}
