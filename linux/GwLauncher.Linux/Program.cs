using GwLauncher.Linux;

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        PrintUsage();
        return 0;
    }

    var store = new AccountStore(AppPaths.AccountsFile);

    try
    {
        store.Load(ReadMasterPassword);
    }
    catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException)
    {
        Console.Error.WriteLine($"Could not read {AppPaths.AccountsFile}: {ex.Message}");
        return 1;
    }

    switch (args[0])
    {
        case "list":
            foreach (var account in store.Accounts)
            {
                Console.WriteLine($"{account.Name}\t{account.Email}\t{account.EffectiveGwPath}");
            }

            return 0;

        case "launch" when args.Length >= 2:
            var dryRun = args.Contains("--dry-run");
            var target = args[1];
            var selected = store.Accounts.FirstOrDefault(a =>
                a.Name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                a.Email.Equals(target, StringComparison.OrdinalIgnoreCase));

            if (selected == null)
            {
                Console.Error.WriteLine($"No account matching '{target}'. Run 'gwlauncher list'.");
                return 1;
            }

            return Launch(selected, dryRun);

        default:
            PrintUsage();
            return 1;
    }
}

static int Launch(LinuxAccount account, bool dryRun)
{
    try
    {
        var psi = WineLauncher.BuildStartInfo(account);

        if (dryRun)
        {
            Console.WriteLine(WineLauncher.Describe(psi));
            return 0;
        }

        // Launching an out-of-date exe lets the game update itself, which breaks mods, so the CLI refuses instead of asking.
        if (AccountDiagnostics.IsGwExeOutdatedAsync(account).GetAwaiter().GetResult())
        {
            Console.Error.WriteLine($"The Gw.exe for '{account.Name}' is out of date, so it wasn't started. Update it from the launcher's View issues window, then launch again.");
            return 1;
        }

        WineLauncher.PrepareLaunch(account);
        var pid = WineLauncher.Start(psi);
        Console.WriteLine($"Started '{account.Name}' (pid {pid}).");
        return 0;
    }
    catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static string ReadMasterPassword()
{
    var env = Environment.GetEnvironmentVariable("GWLAUNCHER_MASTER_PASSWORD");
    if (!string.IsNullOrEmpty(env))
    {
        return env;
    }

    Console.Write("Master password: ");
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            break;
        }

        if (key.Key == ConsoleKey.Backspace && chars.Count > 0)
        {
            chars.RemoveAt(chars.Count - 1);
        }
        else if (!char.IsControl(key.KeyChar))
        {
            chars.Add(key.KeyChar);
        }
    }

    Console.WriteLine();
    return new string(chars.ToArray());
}

static void PrintUsage()
{
    Console.WriteLine("""
        gwlauncher (Linux) — launches Guild Wars accounts through Wine

        Usage:
          gwlauncher list
          gwlauncher launch <name|email> [--dry-run]

        Environment:
          GWLAUNCHER_ACCOUNTS         path to Accounts.json (default ~/.local/share/gwlauncher/Accounts.json)
          GWLAUNCHER_MASTER_PASSWORD  master password for an encrypted Accounts.json (otherwise prompted)
          GWLAUNCHER_WINE             wine binary to use (default: wine)
        """);
}
