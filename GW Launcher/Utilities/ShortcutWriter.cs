namespace GW_Launcher.Utilities;

// Writes a .lnk through WScript.Shell, bound at runtime so the build doesn't need the IWshRuntimeLibrary COM reference.
internal static class ShortcutWriter
{
    public static void Create(string path, string targetPath, string arguments, string workingDirectory, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.Description = description;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Arguments = arguments;
        shortcut.TargetPath = targetPath;
        shortcut.Save();
    }
}
