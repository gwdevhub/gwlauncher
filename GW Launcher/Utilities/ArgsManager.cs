namespace GW_Launcher.Utilities;

internal static class ArgsManager
{
    private static Dictionary<string, string> _commandLineArgs;

    static ArgsManager()
    {
        _commandLineArgs = GetCommandLineArgs();
    }

    public static bool processAutoCloseAfterLaunch()
    {
        return _commandLineArgs.ContainsKey("autoclose") && _commandLineArgs["autoclose"] == "true";
    }

    public static List<int> processProfileArgs()
    {
        List<int> profilesToLaunch = new();
        if (_commandLineArgs.ContainsKey("profiles"))
        {
            var profiles = _commandLineArgs["profiles"];
            foreach (var profile in profiles.Split(','))
            {
                if (int.TryParse(profile, out int profileValue))
                {
                    profilesToLaunch.Add(profileValue);
                }
            }
        }
        return profilesToLaunch;
    }

    private static Dictionary<string, string> GetCommandLineArgs()
    {
        var argsDictionary = new Dictionary<string, string>();
        var args = Environment.GetCommandLineArgs().Skip(1);
        foreach (var chunk in args.Chunk(2))
        {
            if (chunk.Length == 2)
            {
                argsDictionary[chunk.First()] = chunk.Last();
            }
        }
        return argsDictionary;
    }
}
