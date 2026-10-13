namespace GwLauncher.Linux;

// Points an account at a file that a resolve has written beside its compressed original.
public static class AccountRepoint
{
    // Returns whether the account changed, and a message for the user.
    public static (bool Changed, string Message) Apply(LinuxAccount account, string originalPath, string resolvedPath)
    {
        if (account.EffectiveGwPath == originalPath)
        {
            account.LinuxGwPath = resolvedPath;
            return (true, $"Gw.exe now runs from {resolvedPath}.");
        }

        if (Path.GetFileName(originalPath).Equals("Gw.dat", StringComparison.OrdinalIgnoreCase))
        {
            // The game looks for Gw.dat by name in its own folder, and no account setting can point it elsewhere.
            return (false, $"Gw.dat was decompressed to {resolvedPath}. The game needs it named Gw.dat in its own folder, so it still has to be placed there.");
        }

        var mod = account.Mods.FirstOrDefault(m => m.EffectivePath == originalPath);
        if (mod != null)
        {
            mod.LinuxPath = resolvedPath;
            return (true, $"The mod now loads from {resolvedPath}.");
        }

        return (false, "No setting of this account points at that file.");
    }
}
