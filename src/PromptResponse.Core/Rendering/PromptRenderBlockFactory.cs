using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Maps a prompt's response and rendering hints to the field-shaped blocks used
/// by ordinary sections and table rows. Keeping this mapping in one place
/// prevents fillable renderers from seeing subtly different metadata depending
/// on where a prompt appears in the document tree.
/// </summary>
internal static class PromptRenderBlockFactory
{
    public static FieldBlock CreateField(Prompt prompt, RenderOptions options)
    {
        var hasResponse = HasResponse(prompt.Response);
        return new FieldBlock(
            Label: prompt.Label,
            Value: hasResponse ? prompt.Response : options.EmptyFieldText,
            HasResponse: hasResponse,
            HelpText: NullIfBlank(prompt.Hints.HelpText),
            ExpectedDataType: NullIfBlank(prompt.Hints.ExpectedDataType),
            Id: prompt.Id,
            Choices: CopyChoices(prompt));
    }

    public static TableCellBlock CreateTableCell(Prompt? prompt, string fallbackId)
    {
        var value = prompt?.Response ?? string.Empty;
        return new TableCellBlock(
            Value: value,
            HasResponse: HasResponse(value),
            Id: prompt?.Id ?? fallbackId,
            ExpectedDataType: NullIfBlank(prompt?.Hints.ExpectedDataType),
            Choices: prompt is null ? null : CopyChoices(prompt));
    }

    public static bool HasResponse(string? value) => !string.IsNullOrWhiteSpace(value);

    private static IReadOnlyList<string>? CopyChoices(Prompt prompt) =>
        prompt.Hints.SuggestedValues.Count > 0 ? prompt.Hints.SuggestedValues.ToList() : null;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
