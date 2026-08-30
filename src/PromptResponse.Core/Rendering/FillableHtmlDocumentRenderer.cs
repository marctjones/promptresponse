using System.Text;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Renders an <see cref="AprDocument"/> to a self-contained fillable HTML page.
/// The page embeds the original document and downloads a filled <c>.aprf</c>
/// without a server or backend.
/// </summary>
/// <remarks>
/// The renderer owns model construction and composition. Document structure,
/// editable controls, and the download transport live in dedicated markup helpers
/// so their independent security boundaries remain apparent.
/// </remarks>
public sealed class FillableHtmlDocumentRenderer : IDocumentRenderer
{
    private readonly IDocumentRenderModelBuilder _builder;
    private readonly IAprSerializer _serializer;

    /// <summary>
    /// Initializes the renderer, optionally with a custom model builder and
    /// serializer (defaulting to <see cref="DocumentRenderModelBuilder"/> and
    /// <see cref="AprJsonSerializer"/>).
    /// </summary>
    public FillableHtmlDocumentRenderer(
        IDocumentRenderModelBuilder? builder = null,
        IAprSerializer? serializer = null)
    {
        _builder = builder ?? new DocumentRenderModelBuilder();
        _serializer = serializer ?? new AprJsonSerializer();
    }

    /// <inheritdoc />
    public string FormatId => "html-form";

    /// <inheritdoc />
    public string FileExtension => ".html";

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
        var html = BuildHtml(model, _serializer.Serialize(document));

        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(html);
        writer.Flush();
    }

    private static string BuildHtml(RenderModel model, string embeddedJson)
    {
        var title = string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title;
        var output = new StringBuilder();

        FillableHtmlDocumentMarkup.AppendStart(output, title);
        FillableHtmlDocumentMarkup.AppendFormStart(output, model, title);
        AppendBlocks(output, model.Blocks);
        FillableHtmlDocumentMarkup.AppendFormEnd(output);
        FillableHtmlDownloadSupport.Append(output, embeddedJson);
        output.Append("</body>\n</html>\n");
        return output.ToString();
    }

    private static void AppendBlocks(StringBuilder output, IReadOnlyList<RenderBlock> blocks)
    {
        var fieldSequence = 0;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    FillableHtmlDocumentMarkup.AppendHeading(output, heading);
                    break;
                case FieldBlock field:
                    FillableHtmlFieldMarkup.AppendField(output, field, ++fieldSequence);
                    break;
                case TableBlock table:
                    FillableHtmlFieldMarkup.AppendTable(output, table, ref fieldSequence);
                    break;
                case SignatureBlock signature:
                    HtmlDocumentRenderer.AppendSignatures(output, signature);
                    break;
            }
        }
    }
}
