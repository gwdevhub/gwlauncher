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

    // Runs work behind a progress view. The result is the work's value, or null when it was cancelled or failed.
    Task<T?> Progress<T>(ProgressOptions options, Func<CancellationToken, IProgress<ProgressUpdate>, Task<T>> work) where T : class;
}

public static class PromptExtensions
{
    public static async Task<bool> Confirm(this IPrompts prompts, string title, string message) =>
        await prompts.Choose(title, message, "No", "Yes") == 1;
}
