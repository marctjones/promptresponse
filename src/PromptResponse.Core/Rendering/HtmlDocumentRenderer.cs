using System.Net;
using System.Text;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// A dependency-free <see cref="IDocumentRenderer"/> that renders a document to
/// a self-contained, accessible HTML page. Foundation for a browser fill/view
/// path; also useful standalone (<c>apr export --format=html</c>).
/// </summary>
/// <remarks>
/// All dynamic text is HTML-encoded (responses are arbitrary user input, so this
/// is an XSS boundary). Output is semantic: a single <c>&lt;h1&gt;</c> title,
/// nested headings by section depth, labeled fields, and real tables with
/// row/column headers, plus a <c>lang</c> attribute and minimal inline CSS for
/// readability.
/// </remarks>
public sealed class HtmlDocumentRenderer : IDocumentRenderer
{
    private const string Css =
        "body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;line-height:1.5;max-width:48rem;margin:2rem auto;padding:0 1rem;color:#1a1a1a}" +
        "h1{font-weight:600}h2,h3,h4,h5,h6{margin-top:1.5rem}" +
        ".muted{color:#666}.field{margin:.75rem 0}.label{font-weight:600}" +
        ".value{white-space:pre-wrap}.help{font-size:.85em;color:#666;margin:.1rem 0 0}" +
        "table{border-collapse:collapse;width:100%;margin:.75rem 0}" +
        "th,td{border:1px solid #ccc;padding:.4rem .6rem;text-align:left;vertical-align:top}" +
        "th{background:#f5f5f5}" +
        ".signatures{list-style:none;padding:0}.sig{border-left:3px solid #2e7d32;padding:.4rem .6rem;margin:.5rem 0;background:#f6f9f6}" +
        ".sig.bad{border-left-color:#b00;background:#fbf0f0}.sig-badge{font-weight:600}.sig.bad .sig-badge{color:#b00}" +
        ".sig-status{font-size:.85em;color:#555}";

    private readonly IDocumentRenderModelBuilder _builder;

    /// <summary>
    /// Initializes the renderer, optionally with a custom model builder
    /// (defaults to <see cref="DocumentRenderModelBuilder"/>).
    /// </summary>
    public HtmlDocumentRenderer(IDocumentRenderModelBuilder? builder = null)
    {
        _builder = builder ?? new DocumentRenderModelBuilder();
    }

    /// <inheritdoc />
    public string FormatId => "html";

    /// <inheritdoc />
    public string FileExtension => ".html";

    /// <inheritdoc />
    public void Render(AprDocument document, RenderOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var model = _builder.Build(document, options);
        var html = BuildHtml(model);

        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(html);
        writer.Flush();
    }

    private static string BuildHtml(RenderModel model)
    {
        var title = string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title;
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(Enc(title)).Append("</title>\n");
        sb.Append("<style>").Append(Css).Append("</style>\n");
        sb.Append("</head>\n<body>\n");

        sb.Append("<h1>").Append(Enc(title)).Append("</h1>\n");
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            sb.Append("<p class=\"muted\">").Append(Enc(model.Description!)).Append("</p>\n");
        }

        foreach (var block in model.Blocks)
        {
            AppendBlock(sb, block);
        }

        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static void AppendBlock(StringBuilder sb, RenderBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                var tag = "h" + Math.Clamp(h.Level + 1, 2, 6); // document title is h1
                sb.Append('<').Append(tag).Append('>').Append(Enc(h.Text)).Append("</").Append(tag).Append(">\n");
                if (!string.IsNullOrWhiteSpace(h.Description))
                {
                    sb.Append("<p class=\"muted\">").Append(Enc(h.Description!)).Append("</p>\n");
                }
                break;

            case FieldBlock f:
                sb.Append("<div class=\"field\">");
                sb.Append("<div class=\"label\">").Append(Enc(f.Label)).Append("</div>");
                var valueClass = f.HasResponse ? "value" : "value muted";
                sb.Append("<div class=\"").Append(valueClass).Append("\">").Append(Enc(f.Value)).Append("</div>");
                if (!string.IsNullOrWhiteSpace(f.HelpText))
                {
                    sb.Append("<p class=\"help\">").Append(Enc(f.HelpText!)).Append("</p>");
                }
                sb.Append("</div>\n");
                break;

            case TableBlock t:
                AppendTable(sb, t);
                break;

            case SignatureBlock sig:
                AppendSignatures(sb, sig);
                break;
        }
    }

    /// <summary>Renders the signatures summary with a verified/invalid badge per signature.</summary>
    internal static void AppendSignatures(StringBuilder sb, SignatureBlock block)
    {
        sb.Append("<h2>Signatures</h2>\n<ul class=\"signatures\">\n");
        foreach (var s in block.Signatures)
        {
            var badge = s.ContentValid ? "✓ verified" : "✗ INVALID";
            var cls = s.ContentValid ? "sig" : "sig bad";
            sb.Append("<li class=\"").Append(cls).Append("\">")
              .Append("<span class=\"sig-badge\">").Append(badge).Append("</span> ")
              .Append("<strong>").Append(Enc(s.Role)).Append("</strong>: ").Append(Enc(s.Signer))
              .Append(" — ").Append(Enc(s.Scope))
              .Append("<br><span class=\"sig-status\">trust: ").Append(Enc(s.Trust)).Append(" · ").Append(Enc(s.Status)).Append("</span>")
              .Append("</li>\n");
        }
        sb.Append("</ul>\n");
    }

    private static void AppendTable(StringBuilder sb, TableBlock table)
    {
        sb.Append("<table>\n<thead>\n<tr><th></th>");
        foreach (var col in table.ColumnHeaders)
        {
            sb.Append("<th scope=\"col\">").Append(Enc(col)).Append("</th>");
        }
        sb.Append("</tr>\n</thead>\n<tbody>\n");
        foreach (var row in table.Rows)
        {
            sb.Append("<tr><th scope=\"row\">").Append(Enc(row.Label)).Append("</th>");
            foreach (var cell in row.Cells)
            {
                sb.Append("<td>").Append(Enc(cell.Value)).Append("</td>");
            }
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}
