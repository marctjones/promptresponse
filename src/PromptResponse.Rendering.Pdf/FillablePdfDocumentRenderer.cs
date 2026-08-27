using Excise.Core.Authoring;
using Excise.Core.Graphics;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Renders an <see cref="AprDocument"/> to a <em>fillable</em> AcroForm PDF —
/// a blank (or partially filled) form whose prompts become live, editable
/// fields in any PDF viewer.
/// </summary>
/// <remarks>
/// Like <see cref="PdfDocumentRenderer"/>, this consumes the shared
/// <see cref="RenderModel"/> from <see cref="DocumentRenderModelBuilder"/>, but
/// emits interactive form fields via pdfe v2.4.0's <see cref="PdfDocumentBuilder"/>
/// (marctjones/pdfe#383/#380) instead of flattened text:
/// <list type="bullet">
///   <item>a <c>boolean</c> prompt → checkbox;</item>
///   <item>a prompt with suggested values → dropdown;</item>
///   <item>a <c>multiline</c> prompt → a multi-line text field; else a text field.</item>
/// </list>
/// The prompt's id is the stable field name; any existing response becomes the
/// field's default value. Empty fields are always included (a form needs its
/// blanks). Table sections are rendered read-only for now (per-cell fields are
/// a follow-up). MVP scope is Latin-text/base-14; field tooltips (<c>/TU</c>)
/// and tagged output are deferred upstream (pdfe#380/#275).
/// </remarks>
public sealed class FillablePdfDocumentRenderer : IDocumentRenderer
{
    private static readonly TextStyle MutedStyle =
        TextStyle.Body.WithColor(PdfColor.FromGray(0.4));

    private static readonly TextStyle HelpStyle =
        TextStyle.Body.WithSize(8).WithColor(PdfColor.FromGray(0.4));

    private static readonly HashSet<string> TruthyValues =
        new(StringComparer.OrdinalIgnoreCase) { "true", "yes", "y", "1", "x", "checked", "on" };

    private readonly IDocumentRenderModelBuilder _builder;
    private readonly PdfRenderOptions _print;

    /// <summary>
    /// Initializes the renderer, optionally with a custom model builder
    /// (defaults to <see cref="DocumentRenderModelBuilder"/>) and print options
    /// (page size + running footer; defaults to <see cref="PdfRenderOptions.Default"/>).
    /// </summary>
    public FillablePdfDocumentRenderer(IDocumentRenderModelBuilder? builder = null, PdfRenderOptions? print = null)
    {
        _builder = builder ?? new DocumentRenderModelBuilder();
        _print = print ?? PdfRenderOptions.Default;
    }

    /// <inheritdoc />
    public string FormatId => "pdf-form";

    /// <inheritdoc />
    public string FileExtension => ".pdf";

    /// <inheritdoc />
    public void Render(AprDocument document, RenderOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        // A fillable form must show every prompt as a blank, regardless of the
        // caller's empty-field preference.
        var formOptions = new RenderOptions
        {
            IncludeEmptyFields = true,
            EmptyFieldText = options.EmptyFieldText,
        };
        var model = _builder.Build(document, formOptions);
        var pdf = PdfDocumentBuilder.Create(PdfRenderHelpers.ToPageSize(_print.PageSize));
        PdfRenderHelpers.ApplyMetadata(pdf, document.Metadata);

        var title = string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title;
        pdf.Heading(title, level: 1);
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            pdf.Paragraph(model.Description!, MutedStyle);
        }

        foreach (var block in model.Blocks)
        {
            WriteBlock(pdf, block);
        }

        var doc = pdf.Build();
        PdfRenderHelpers.ApplyRunningElements(doc, _print, title);
        doc.Save(output);
    }

    private static void WriteBlock(PdfDocumentBuilder pdf, RenderBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                pdf.Heading(h.Text, Math.Min(h.Level + 1, 6));
                if (!string.IsNullOrWhiteSpace(h.Description))
                {
                    pdf.Paragraph(h.Description!, MutedStyle);
                }
                break;

            case FieldBlock f:
                WriteField(pdf, f);
                break;

            case TableBlock t:
                WriteFillableTable(pdf, t);
                break;

            case SignatureBlock s:
                pdf.Heading("Signatures", level: 2);
                foreach (var sig in s.Signatures)
                {
                    var badge = sig.ContentValid ? "[verified]" : "[INVALID]";
                    pdf.KeyValue($"{badge} {sig.Role}", $"{sig.Signer} - {sig.Scope}");
                    pdf.Paragraph($"trust: {sig.Trust} - {sig.Status}", HelpStyle);
                }
                break;
        }
    }

    private static void WriteField(PdfDocumentBuilder pdf, FieldBlock f)
    {
        var fieldName = string.IsNullOrEmpty(f.Id) ? null : f.Id;
        var defaultValue = f.HasResponse ? f.Value : null;
        var dataType = f.ExpectedDataType ?? string.Empty;
        var tooltip = FieldTooltip(f);

        if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            pdf.CheckBox(f.Label, fieldName,
                checkedByDefault: defaultValue is not null && TruthyValues.Contains(defaultValue),
                tooltip: tooltip);
        }
        else if (f.Choices is { Count: > 0 })
        {
            pdf.Dropdown(f.Label, ChoicesIncluding(f.Choices, defaultValue), fieldName, defaultValue,
                tooltip: tooltip);
        }
        else
        {
            var multiline = dataType.Equals("multiline", StringComparison.OrdinalIgnoreCase);
            pdf.TextField(f.Label, fieldName, multiline: multiline, lines: multiline ? 3 : 1,
                defaultValue: defaultValue, tooltip: tooltip);
        }

        if (!string.IsNullOrWhiteSpace(f.HelpText))
        {
            pdf.Paragraph(f.HelpText!, HelpStyle);
        }
    }

    /// <summary>
    /// Renders a table section as a fillable grid: each (fixed-row) cell becomes a
    /// live AcroForm field named by its cell id, so the table round-trips like the
    /// rest of the form. A cell's accessible name (<c>/TU</c>) combines its row and
    /// column headers. Dynamic tables have no rows, so this emits just the header.
    /// </summary>
    private static void WriteFillableTable(PdfDocumentBuilder pdf, TableBlock t)
    {
        var rows = new List<FillableTableRow>(t.Rows.Count);
        foreach (var row in t.Rows)
        {
            var cells = new List<FillableTableCell>(row.Cells.Count);
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                var columnHeader = c < t.ColumnHeaders.Count ? t.ColumnHeaders[c] : string.Empty;
                var fieldName = string.IsNullOrEmpty(cell.Id) ? $"cell_{row.Label}_{c}" : cell.Id;
                var tooltip = $"{row.Label} {columnHeader}".Trim();
                var value = cell.HasResponse ? cell.Value : null;
                var dataType = cell.ExpectedDataType ?? string.Empty;

                FillableTableCell spec;
                if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
                {
                    spec = new FillableTableCell(fieldName, FillableCellKind.CheckBox, value, Tooltip: tooltip);
                }
                else if (cell.Choices is { Count: > 0 })
                {
                    spec = new FillableTableCell(
                        fieldName, FillableCellKind.Choice, value, ChoicesIncluding(cell.Choices, value), tooltip);
                }
                else
                {
                    spec = new FillableTableCell(fieldName, FillableCellKind.Text, value, Tooltip: tooltip);
                }
                cells.Add(spec);
            }
            rows.Add(new FillableTableRow(row.Label, cells));
        }

        pdf.FillableTable(t.ColumnHeaders, rows);
    }

    /// <summary>
    /// The choice list to offer for a dropdown, guaranteed to contain the current answer.
    /// </summary>
    /// <remarks>
    /// <c>suggestedValues</c> offers options; a response outside the list is still valid
    /// (specification section 4.7). A PDF choice field rejects a default that is not one
    /// of its options, so exporting a form whose answer was typed rather than picked used
    /// to throw - and the desktop app died on it.
    ///
    /// Including the answer preserves it and keeps the dropdown. Dropping it instead
    /// would silently lose what someone wrote, which is the one thing this format exists
    /// to prevent.
    /// </remarks>
    /// <summary>What a PDF reader announces and shows on hover for a field (<c>/TU</c>).</summary>
    /// <remarks>
    /// <para>
    /// The label alone was going in, which told a hovering user the thing they could
    /// already read beside the box. The APR document usually knows more: helpText is the
    /// author's guidance, and it was reaching the printed page but not the field.
    /// </para>
    /// <para>
    /// The label leads because /TU is the field's accessible name in every reader that
    /// exposes one - dropping it in favour of the guidance would leave a screen-reader
    /// user with advice about a field they can no longer identify.
    /// </para>
    /// </remarks>
    private static string FieldTooltip(FieldBlock f) =>
        string.IsNullOrWhiteSpace(f.HelpText) ? f.Label : $"{f.Label} — {f.HelpText!.Trim()}";

    private static IReadOnlyList<string> ChoicesIncluding(IReadOnlyList<string> offered, string? answer)
    {
        if (string.IsNullOrEmpty(answer) || offered.Contains(answer, StringComparer.Ordinal))
        {
            return offered;
        }
        return [.. offered, answer];
    }
}
