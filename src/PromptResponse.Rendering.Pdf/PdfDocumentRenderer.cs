using Pdfe.Core.Document;
using Pdfe.Core.Graphics;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Renders an <see cref="AprDocument"/> to a flattened PDF, using the
/// pdfe engine (<c>Pdfe.Core</c>) behind the shared
/// <see cref="IDocumentRenderer"/> seam.
/// </summary>
/// <remarks>
/// Consumes the format-agnostic <see cref="RenderModel"/> produced by
/// <see cref="DocumentRenderModelBuilder"/> — the single shared document
/// traversal — and lays each block out top-to-bottom with word-wrap and
/// automatic pagination (<see cref="PdfLayoutWriter"/>).
/// <para>
/// MVP scope: a flattened (non-interactive), Latin-text PDF using the base-14
/// fonts. Unicode font embedding, a fillable AcroForm variant, and tagged
/// (accessible) output are deferred and tracked upstream in pdfe
/// (marctjones/pdfe#378, #380, #275) and on the PromptResponse MVP list.
/// </para>
/// </remarks>
public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    private readonly IDocumentRenderModelBuilder _builder;

    /// <summary>Heading font size by nesting level (clamped); index 0 unused.</summary>
    private static double HeadingSize(int level) => level switch
    {
        1 => 14,
        2 => 13,
        3 => 12,
        _ => 11,
    };

    /// <summary>
    /// Initializes the renderer, optionally with a custom model builder
    /// (defaults to <see cref="DocumentRenderModelBuilder"/>).
    /// </summary>
    public PdfDocumentRenderer(IDocumentRenderModelBuilder? builder = null)
    {
        _builder = builder ?? new DocumentRenderModelBuilder();
    }

    /// <inheritdoc />
    public string FormatId => "pdf";

    /// <inheritdoc />
    public string FileExtension => ".pdf";

    /// <inheritdoc />
    public void Render(AprDocument document, RenderOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var model = _builder.Build(document, options);

        using var pdf = PdfDocument.CreateNew();
        using (var writer = new PdfLayoutWriter(pdf))
        {
            WriteHeader(writer, model);
            foreach (var block in model.Blocks)
            {
                WriteBlock(writer, block);
            }
        }

        pdf.Save(output);
    }

    private static void WriteHeader(PdfLayoutWriter writer, RenderModel model)
    {
        var title = string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title;
        writer.Paragraph(title, PdfFont.HelveticaBold(18), spacingAfter: 4);
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            writer.Paragraph(model.Description, PdfFont.Helvetica(10), spacingAfter: 4, muted: true);
        }
        writer.Gap(8);
    }

    private static void WriteBlock(PdfLayoutWriter writer, RenderBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                writer.Gap(6);
                writer.Paragraph(h.Text, PdfFont.HelveticaBold(HeadingSize(h.Level)), spacingAfter: 2);
                if (!string.IsNullOrWhiteSpace(h.Description))
                {
                    writer.Paragraph(h.Description, PdfFont.Helvetica(9), spacingAfter: 2, muted: true);
                }
                break;

            case FieldBlock f:
                writer.Paragraph(f.Label, PdfFont.HelveticaBold(10));
                writer.Paragraph(f.Value, PdfFont.Helvetica(10), indent: 12, spacingAfter: 6, muted: !f.HasResponse);
                if (!string.IsNullOrWhiteSpace(f.HelpText))
                {
                    writer.Paragraph(f.HelpText!, PdfFont.Helvetica(8), indent: 12, spacingAfter: 4, muted: true);
                }
                break;

            case TableBlock t:
                var rows = t.Rows
                    .Select(r => (r.Label, (IReadOnlyList<string>)r.Cells.Select(c => c.Value).ToList()))
                    .ToList();
                writer.Table(t.ColumnHeaders, rows, PdfFont.HelveticaBold(9), PdfFont.Helvetica(9));
                break;
        }
    }
}
