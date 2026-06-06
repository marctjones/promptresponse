using Pdfe.Core.Authoring;
using Pdfe.Core.Graphics;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Renders an <see cref="AprDocument"/> to a flattened PDF, using the pdfe
/// engine's high-level <see cref="PdfDocumentBuilder"/> behind the shared
/// <see cref="IDocumentRenderer"/> seam.
/// </summary>
/// <remarks>
/// Consumes the format-agnostic <see cref="RenderModel"/> produced by
/// <see cref="DocumentRenderModelBuilder"/> — the single shared document
/// traversal — and maps each block onto <see cref="PdfDocumentBuilder"/>, which
/// handles word-wrap, pagination, and table layout (pdfe v2.4.0+,
/// marctjones/pdfe#383). No layout bookkeeping lives here.
/// <para>
/// MVP scope: a flattened (non-interactive), Latin-text PDF using the base-14
/// fonts. Unicode font embedding, a fillable-AcroForm variant, and tagged
/// (accessible) output are deferred and tracked upstream in pdfe
/// (marctjones/pdfe#378, #380, #275).
/// </para>
/// </remarks>
public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    private static readonly TextStyle MutedStyle =
        TextStyle.Body.WithColor(PdfColor.FromGray(0.4));

    private static readonly TextStyle HelpStyle =
        TextStyle.Body.WithSize(8).WithColor(PdfColor.FromGray(0.4));

    private readonly IDocumentRenderModelBuilder _builder;

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
        var pdf = PdfDocumentBuilder.Create();
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

        pdf.Save(output);
    }

    private static void WriteBlock(PdfDocumentBuilder pdf, RenderBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                // Document title is level 1, so sections start at level 2.
                pdf.Heading(h.Text, Math.Min(h.Level + 1, 6));
                if (!string.IsNullOrWhiteSpace(h.Description))
                {
                    pdf.Paragraph(h.Description!, MutedStyle);
                }
                break;

            case FieldBlock f:
                pdf.KeyValue(f.Label, f.Value);
                if (!string.IsNullOrWhiteSpace(f.HelpText))
                {
                    pdf.Paragraph(f.HelpText!, HelpStyle);
                }
                break;

            case TableBlock t:
                pdf.Table(PdfRenderHelpers.BuildTableRows(t), headerRow: true);
                break;
        }
    }
}
