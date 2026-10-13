using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace GwLauncher.Linux;

// Unpacks the embedded Windows DLL loader folder next to the launcher, where WineLauncher looks for it.
public static class InjectorInstaller
{
    private const string ResourceName = "injector.zip";
    private const string StampName = ".bundle-sha256";

    public static void Ensure()
    {
        // Dev builds without the published injector have no resource; WineLauncher reports the missing loader when a launch needs it.
        using var archive = Assembly.GetEntryAssembly()?.GetManifestResourceStream(ResourceName);
        if (archive == null)
        {
            return;
        }

        var folder = Path.GetDirectoryName(WineLauncher.InjectorPath)!;
        var stamp = Path.Combine(folder, StampName);
        var digest = Convert.ToHexString(SHA256.HashData(archive));
        if (File.Exists(WineLauncher.InjectorPath) && File.Exists(stamp) && File.ReadAllText(stamp) == digest)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        archive.Position = 0;
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Read))
        {
            zip.ExtractToDirectory(folder, overwriteFiles: true);
        }

        File.WriteAllText(stamp, digest);
    }
}
