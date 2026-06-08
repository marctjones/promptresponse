namespace PromptResponse.Core.Rendering;

/// <summary>
/// A single semantic, layout-free unit of rendered output produced by
/// <see cref="DocumentRenderModelBuilder"/>. Output formats (PDF, plain text,
/// HTML, print) consume a sequence of these instead of re-walking the
/// <see cref="Models.AprDocument"/> tree themselves.
/// </summary>
/// <remarks>
/// Blocks describe meaning ("this is a level-2 heading", "this is a field with
/// a label and a value"), never presentation. Each renderer decides how a block
/// looks. This keeps a single document traversal shared across every output
/// format and honors the APR principle that content carries no layout.
/// </remarks>
public abstract record RenderBlock;

/// <summary>
/// A section heading at a given nesting depth.
/// </summary>
/// <param name="Level">1-based nesting depth (top-level sections are level 1).</param>
/// <param name="Text">The section title.</param>
/// <param name="Description">Optional section description, if present.</param>
public sealed record HeadingBlock(int Level, string Text, string? Description) : RenderBlock;

/// <summary>
/// A single prompt rendered as a label/value pair.
/// </summary>
/// <param name="Label">The prompt's user-visible label.</param>
/// <param name="Value">
/// The response, or the configured empty-field placeholder when the prompt has
/// no response and empty fields are being included.
/// </param>
/// <param name="HasResponse">Whether the prompt actually has a non-blank response.</param>
/// <param name="HelpText">Optional help text from the prompt's hints.</param>
/// <param name="ExpectedDataType">Optional advisory data-type hint (e.g. "date", "email").</param>
/// <param name="Id">
/// The prompt's stable id, used as the form-field name when authoring a
/// fillable PDF. Empty when not applicable.
/// </param>
/// <param name="Choices">
/// Suggested values from the prompt's hints. When non-empty, a fillable
/// renderer presents this field as a dropdown.
/// </param>
public sealed record FieldBlock(
    string Label,
    string Value,
    bool HasResponse,
    string? HelpText,
    string? ExpectedDataType,
    string Id = "",
    IReadOnlyList<string>? Choices = null) : RenderBlock;

/// <summary>
/// A table section flattened into headers and rows.
/// </summary>
/// <param name="ColumnHeaders">Column header labels, in column order.</param>
/// <param name="Rows">The table rows, in row order.</param>
public sealed record TableBlock(
    IReadOnlyList<string> ColumnHeaders,
    IReadOnlyList<TableRowBlock> Rows) : RenderBlock;

/// <summary>
/// One row of a <see cref="TableBlock"/>.
/// </summary>
/// <param name="Label">The row's header label.</param>
/// <param name="Cells">Cell values in column order (aligned to the table's headers).</param>
public sealed record TableRowBlock(string Label, IReadOnlyList<TableCellBlock> Cells);

/// <summary>
/// A summary of the document's signatures and their verification status, rendered
/// as a "Signatures" section at the end of the output.
/// </summary>
/// <param name="Signatures">One entry per signature, in document order.</param>
public sealed record SignatureBlock(IReadOnlyList<SignatureSummary> Signatures) : RenderBlock;

/// <summary>One signature's display summary for rendering.</summary>
/// <param name="Role">"Publisher" or "Filler".</param>
/// <param name="Signer">The signer's name (certificate subject).</param>
/// <param name="Scope">A human description of what the signature covers.</param>
/// <param name="ContentValid">Whether the covered content verifies (unaltered).</param>
/// <param name="Trust">Trust level (e.g. "Trusted", "SelfSigned", "Untrusted", "Invalid").</param>
/// <param name="Status">A human-readable status line.</param>
public sealed record SignatureSummary(
    string Role, string Signer, string Scope, bool ContentValid, string Trust, string Status);

/// <summary>
/// One cell of a <see cref="TableRowBlock"/>.
/// </summary>
/// <param name="Value">The cell's response text (empty string when unanswered).</param>
/// <param name="HasResponse">Whether the cell has a non-blank response.</param>
/// <param name="Id">
/// The cell prompt's stable id (the <c>"{rowId}.{columnId}"</c> convention), used
/// as the form-field name when a fillable renderer makes the cell editable. Empty
/// when not applicable.
/// </param>
/// <param name="ExpectedDataType">
/// The column's advisory data-type (e.g. "currency", "date", "boolean"); guides a
/// fillable renderer's input choice. Null when unspecified.
/// </param>
/// <param name="Choices">
/// The column's suggested values. When non-empty, a fillable renderer presents the
/// cell as a dropdown.
/// </param>
public sealed record TableCellBlock(
    string Value,
    bool HasResponse,
    string Id = "",
    string? ExpectedDataType = null,
    IReadOnlyList<string>? Choices = null);
