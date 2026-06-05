namespace PromptResponse.Core.Rendering;

/// <summary>
/// Format-agnostic options that control how an <see cref="Models.AprDocument"/>
/// is flattened into a <see cref="RenderModel"/> for output (PDF, plain text,
/// HTML, print, ...).
/// </summary>
/// <remarks>
/// These options describe <em>content</em> decisions that every output format
/// shares (e.g. whether to include unanswered fields). Format-specific concerns
/// such as page size, fonts, or margins belong to the individual
/// <see cref="IDocumentRenderer"/> implementation, not here — the core principle
/// is that the APR format and this shared model carry no layout information.
/// </remarks>
public sealed class RenderOptions
{
    /// <summary>
    /// Gets whether fields with no response are included in the output.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c> so a printed template shows every field as a
    /// blank to fill in. Set to <c>false</c> to produce a summary of only the
    /// answered fields (e.g. exporting a completed form for records).
    /// </remarks>
    public bool IncludeEmptyFields { get; init; } = true;

    /// <summary>
    /// Gets the placeholder text a renderer may use in place of an empty
    /// response when <see cref="IncludeEmptyFields"/> is <c>true</c>.
    /// </summary>
    public string EmptyFieldText { get; init; } = "(no response)";

    /// <summary>
    /// A shared default instance: include every field, empty ones marked with
    /// <see cref="EmptyFieldText"/>.
    /// </summary>
    public static RenderOptions Default { get; } = new();
}
