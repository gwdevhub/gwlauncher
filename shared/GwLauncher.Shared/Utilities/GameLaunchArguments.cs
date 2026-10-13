namespace GW_Launcher.Utilities;

// The arguments the game is started with. Values are unquoted, and each launcher quotes them for its own process API.
public static class GameLaunchArguments
{
    public static List<(string Flag, string Value)> ForAccount(string email, string password, string character)
    {
        var arguments = new List<(string Flag, string Value)> { ("-email", email), ("-password", password) };
        if (!string.IsNullOrEmpty(character))
        {
            arguments.Add(("-character", character));
        }

        return arguments;
    }
}
