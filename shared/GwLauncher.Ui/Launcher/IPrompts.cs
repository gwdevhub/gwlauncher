namespace GwLauncher.Ui;

public sealed record ProgressOptions(
    string Title,
    string Heading,
    string? Note = null,
    bool Cancellable = true,
    string FailureText = "The task failed");

// Questions and progress a host shows on the page. The bridge implements it.
public interface IPrompts
{
    void Message(string title, string text);

    // The index of the option the user picked, or null when they closed the prompt.
    Task<int?> Choose(string title, string message, params string[] options);

    // Offers an update with its release notes and page link. True when the user chose to update.
    Task<bool> ConfirmUpdate(string title, string message, string notes, string releaseUrl);

    // Runs work behind a progress view. The result is the work's value, or null when it was cancelled or failed.
    Task<T?> Progress<T>(ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T>> work) where T : class;
}

public static class PromptExtensions
{
    public static async Task<bool> Confirm(this IPrompts prompts, string title, string message) =>
        await prompts.Choose(title, message, "No", "Yes") == 1;

    // Asked before a suspended game runs, so plugins can be checked or a debugger attached first.
    public static async Task<bool> ConfirmResume(this IPrompts prompts) =>
        await prompts.Choose("Launching paused", "Guild Wars is in a suspended state, plugins are not yet loaded.\n\nContinue?", "Cancel", "Continue") == 1;
}
