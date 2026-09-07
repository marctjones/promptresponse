using PromptResponse.Core.Rendering;
using PromptResponse.Desktop.Services;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.GuiSubmitDemo.Avalonia;

/// <summary>
/// A dialog service that answers exactly what the real "Submit via HTTPS" flow needs to
/// proceed, printing each dialog's title and message to the console first.
/// </summary>
/// <remarks>
/// A demo whose point is showing a person what the real app asks before it sends anything
/// must not swallow those questions silently — it prints them, then answers as a person
/// confirming would, the same role <c>--yes</c> plays for the CLI's own submit command.
/// </remarks>
internal sealed class AutoConfirmDialogs : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        Console.WriteLine($"  [dialog] {title}: {message}");
        Console.WriteLine("  [dialog] -> confirmed");
        return Task.FromResult(true);
    }

    public Task<string?> ShowInputAsync(string title, string message, string defaultValue = "",
        bool isPassword = false) => Task.FromResult<string?>(defaultValue);

    public Task<int?> ShowChoiceAsync(string title, string message, IReadOnlyList<string> choices)
    {
        Console.WriteLine($"  [dialog] {title}: {message}");
        for (var i = 0; i < choices.Count; i++) Console.WriteLine($"    {i}. {choices[i]}");
        Console.WriteLine($"  [dialog] -> chose 0 ({choices[0]})");
        return Task.FromResult<int?>(0);
    }

    public Task ShowPrintPreviewAsync(RenderModel model, bool includeEmptyFields) => Task.CompletedTask;
    public Task<bool> ShowImportReviewAsync(ImportQuality quality) => Task.FromResult(false);
}
