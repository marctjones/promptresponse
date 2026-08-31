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
/// then its child sections (recursively). A section whose
/// <see cref="Section.Kind"/> is "table" is emitted as a single
/// <see cref="TableBlock"/> reporting the correspondence between its instances,
/// rather than recursing into them. The block says what the rows mean, not how to
/// draw them — a caller may present it as a grid, as cards, or as a flat run of
/// prompts.
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
        // A table's structure is a semantic claim, not a display instruction: the
        // caller may render this block as a grid, as cards, or as a flat sequence of
        // prompts. The model just reports the correspondence.
        if (section.IsTable && section.Sections.Count > 0)
        {
            blocks.Add(BuildTable(section));
            return;
        }

        foreach (var prompt in section.Prompts)
        {
            if (!PromptRenderBlockFactory.HasResponse(prompt.Response) && !options.IncludeEmptyFields)
            {
                continue;
            }

            blocks.Add(PromptRenderBlockFactory.CreateField(prompt, options));
        }

        foreach (var child in section.Sections)
        {
            AppendSection(child, level + 1, options, blocks);
        }
    }

    /// <summary>
    /// Derives the table from the sections and prompts themselves. There is no column
    /// definition to consult: a column header is the corresponding prompt's label, and
    /// cells correspond by position across instances. Nothing can drift out of step,
    /// because nothing is stated twice.
    /// </summary>
    private static TableBlock BuildTable(Section section)
    {
        var rowSections = section.Sections;

        // Field names come from the first instance; ValidateTable warns when the
        // others disagree, but rendering stays tolerant of a ragged table.
        var headers = rowSections[0].Prompts.Select(p => p.Label).ToList();

        var rows = new List<TableRowBlock>(rowSections.Count);
        foreach (var rowSection in rowSections)
        {
            var cells = new List<TableCellBlock>(headers.Count);
            for (var i = 0; i < headers.Count; i++)
            {
                var cellPrompt = i < rowSection.Prompts.Count ? rowSection.Prompts[i] : null;
                cells.Add(PromptRenderBlockFactory.CreateTableCell(cellPrompt, $"{rowSection.Id}.{i}"));
            }

            rows.Add(new TableRowBlock(rowSection.Title, cells));
        }

        return new TableBlock(headers, rows);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
