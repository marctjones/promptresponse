using System.Text;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// A dependency-free reference <see cref="IDocumentRenderer"/> that renders a
/// document to readable UTF-8 plain text.
/// </summary>
/// <remarks>
/// This renderer lives in Core to anchor the rendering seam with a fully
/// testable, zero-dependency consumer of <see cref="RenderModel"/>. Heavier
/// formats (PDF via QuestPDF, HTML) implement the same
/// <see cref="IDocumentRenderer"/> contract in their own projects so that no
/// third-party rendering dependency leaks into Core.
/// </remarks>
public sealed class PlainTextDocumentRenderer : IDocumentRenderer
{
    private readonly IDocumentRenderModelBuilder _builder;

    /// <summary>
    /// Initializes the renderer, optionally with a custom model builder
    /// (defaults to <see cref="DocumentRenderModelBuilder"/>).
    /// </summary>
    public PlainTextDocumentRenderer(IDocumentRenderModelBuilder? builder = null)
    {
        _builder = builder ?? new DocumentRenderModelBuilder();
    }

    /// <inheritdoc />
    public string FormatId => "text";

    /// <inheritdoc />
    public string FileExtension => ".txt";

    /// <inheritdoc />
    public void Render(AprDocument document, RenderOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var model = _builder.Build(document, options);
        var text = RenderToString(model);

        // Leave the stream open: callers own its lifetime.
        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(text);
        writer.Flush();
    }

    private static string RenderToString(RenderModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title);
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            sb.AppendLine(model.Description);
        }
        sb.AppendLine();

        foreach (var block in model.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    sb.AppendLine($"{new string('#', Math.Clamp(h.Level, 1, 6))} {h.Text}");
                    if (!string.IsNullOrWhiteSpace(h.Description))
                    {
                        sb.AppendLine(h.Description);
                    }
                    sb.AppendLine();
                    break;

                case FieldBlock f:
                    sb.AppendLine($"{f.Label}: {f.Value}");
                    break;

                case TableBlock t:
                    AppendTable(sb, t);
                    break;

                case SignatureBlock s:
                    sb.AppendLine("## Signatures");
                    foreach (var sig in s.Signatures)
                    {
                        var badge = sig.ContentValid ? "[verified]" : "[INVALID]";
                        sb.AppendLine($"{badge} {sig.Role}: {sig.Signer} - {sig.Scope}");
                        sb.AppendLine($"  trust: {sig.Trust} - {sig.Status}");
                    }
                    sb.AppendLine();
                    break;
            }
        }

        return sb.ToString();
    }

    private static void AppendTable(StringBuilder sb, TableBlock table)
    {
        if (table.ColumnHeaders.Count > 0)
        {
            sb.AppendLine(string.Join(" | ", new[] { string.Empty }.Concat(table.ColumnHeaders)));
        }

        foreach (var row in table.Rows)
        {
            var cells = row.Cells.Select(c => c.Value);
            sb.AppendLine(string.Join(" | ", new[] { row.Label }.Concat(cells)));
        }
        sb.AppendLine();
    }
}
