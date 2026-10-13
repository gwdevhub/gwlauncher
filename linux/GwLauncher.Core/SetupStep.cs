namespace GwLauncher.Linux;

// One prerequisite the launcher needs before it can run accounts.
public sealed class SetupStep
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required Func<bool> IsSatisfied { get; init; }

    // Shell command that satisfies the step, or null when the user has to act manually.
    public required Func<string?> Command { get; init; }

    public bool NeedsRoot { get; init; }
}

public static class Setup
{
    public static IReadOnlyList<SetupStep> Steps { get; } =
    [
        new SetupStep
        {
            Title = "Wine",
            Description = "Runs Guild Wars on Linux.",
            IsSatisfied = WineInstaller.IsWineAvailable,
            Command = WineInstaller.InstallCommand,
            NeedsRoot = true
        }
    ];

    public static bool IsComplete => Steps.All(step => step.IsSatisfied());
}
