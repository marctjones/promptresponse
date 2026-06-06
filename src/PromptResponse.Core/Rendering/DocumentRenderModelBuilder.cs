using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// The single, shared traversal that flattens an <see cref="AprDocument"/> into
/// a <see cref="RenderModel"/>. Every output format consumes the result of this
/// one walk instead of re-implementing tree traversal.
/// </summary>
/// <remarks>
/// Traversal order within a section mirrors the established CLI exporters
/// (CSV / TXT / JSON): the section heading, then the section's direct prompts,
/// then its child sections (recursively). A section carrying a
/// <see cref="Section.TableLayout"/> is emitted as a single <see cref="TableBlock"/>
/// rather than recursing into its row sub-sections.
/// </remarks>
public sealed class DocumentRenderModelBuilder : IDocumentRenderModelBuilder
{
    /// <inheritdoc />
    public RenderModel Build(AprDocument document, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var blocks = new List<RenderBlock>();
        foreach (var section in document.Sections)
        {
            AppendSection(section, level: 1, options, blocks);
        }

        return new RenderModel(
            Title: document.Metadata.Title ?? string.Empty,
            Description: document.Metadata.Description,
            DocumentType: document.DocumentType,
            Blocks: blocks);
    }

    private static void AppendSection(Section section, int level, RenderOptions options, List<RenderBlock> blocks)
    {
        blocks.Add(new HeadingBlock(level, section.Title, NullIfBlank(section.Description)));

        // A table section renders as one table; its row sub-sections are cells,
        // not nested sections, so we do not recurse into them.
        if (section.TableLayout is { Columns.Count: > 0 } table)
        {
            blocks.Add(BuildTable(section, table));
            return;
        }

        foreach (var prompt in section.Prompts)
        {
            var hasResponse = !string.IsNullOrWhiteSpace(prompt.Response);
            if (!hasResponse && !options.IncludeEmptyFields)
            {
                continue;
            }

            var choices = prompt.Hints.SuggestedValues.Count > 0
                ? prompt.Hints.SuggestedValues.ToList()
                : null;

            blocks.Add(new FieldBlock(
                Label: prompt.Label,
                Value: hasResponse ? prompt.Response : options.EmptyFieldText,
                HasResponse: hasResponse,
                HelpText: NullIfBlank(prompt.Hints.HelpText),
                ExpectedDataType: NullIfBlank(prompt.Hints.ExpectedDataType),
                Id: prompt.Id,
                Choices: choices));
        }

        foreach (var child in section.Sections)
        {
            AppendSection(child, level + 1, options, blocks);
        }
    }

    private static TableBlock BuildTable(Section section, TableDefinition table)
    {
        var headers = table.Columns.Select(c => c.Label).ToList();

        var rows = new List<TableRowBlock>(section.Sections.Count);
        foreach (var rowSection in section.Sections)
        {
            var cells = new List<TableCellBlock>(table.Columns.Count);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var column = table.Columns[i];
                // Cells carry the id "{rowId}.{columnId}"; match by id, then fall
                // back to positional alignment for tolerance to malformed input.
                var cellId = $"{rowSection.Id}.{column.Id}";
                var cellPrompt =
                    rowSection.Prompts.FirstOrDefault(p => p.Id == cellId)
                    ?? (i < rowSection.Prompts.Count ? rowSection.Prompts[i] : null);

                var value = cellPrompt?.Response ?? string.Empty;
                var choices = column.SuggestedValues is { Count: > 0 }
                    ? column.SuggestedValues.ToList()
                    : null;
                cells.Add(new TableCellBlock(
                    value,
                    HasResponse: !string.IsNullOrWhiteSpace(value),
                    // Prefer the actual cell prompt's id; fall back to the convention
                    // so a fillable renderer can still name the field for blank cells.
                    Id: cellPrompt?.Id ?? cellId,
                    ExpectedDataType: NullIfBlank(column.Type),
                    Choices: choices));
            }

            rows.Add(new TableRowBlock(rowSection.Title, cells));
        }

        return new TableBlock(headers, rows);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
