namespace GwLauncher.Linux;

public static class AppPaths
{
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "gwlauncher");

    public static string IconFile => Path.Combine(DataDir, "gwlauncher-helmet.png");

    public static string AccountsFile =>
        Environment.GetEnvironmentVariable("GWLAUNCHER_ACCOUNTS") is { Length: > 0 } env
            ? env
            : Path.Combine(DataDir, "Accounts.json");
}
