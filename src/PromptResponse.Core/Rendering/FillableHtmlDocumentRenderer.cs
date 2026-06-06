using System.Net;
using System.Text;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Renders an <see cref="AprDocument"/> to a <em>fillable</em>, self-contained
/// HTML page: a real <c>&lt;form&gt;</c> whose prompts become live inputs, with a
/// "Download filled form" button that writes a valid <c>.aprf</c> JSON file —
/// no server, no backend, no toolchain. Open it in any browser, fill, download.
/// </summary>
/// <remarks>
/// <para>
/// This is the browser fill path the static <see cref="HtmlDocumentRenderer"/>
/// laid the foundation for. It mirrors <c>FillablePdfDocumentRenderer</c>'s field
/// mapping (boolean → checkbox, suggested values → dropdown, multiline → textarea,
/// otherwise a typed text input) over the shared <see cref="RenderModel"/>.
/// </para>
/// <para>
/// The round-trip is data-driven: the original document is embedded verbatim as
/// JSON, and a small vanilla-JS shim copies each input's value back into that tree
/// by prompt id, flips <c>documentType</c> to <c>filledForm</c>, and downloads the
/// result. The embedded JSON is unicode-escaped so it cannot break out of its
/// <c>&lt;script&gt;</c> container, and every dynamic value is HTML-encoded — the
/// same XSS boundary the static renderer enforces (responses are arbitrary input).
/// </para>
/// <para>
/// Table sections are rendered read-only for now (their per-cell prompts carry no
/// id through the render model, so they cannot round-trip yet) — the same scope
/// boundary the fillable PDF draws.
/// </para>
/// </remarks>
public sealed class FillableHtmlDocumentRenderer : IDocumentRenderer
{
    private static readonly HashSet<string> TruthyValues =
        new(StringComparer.OrdinalIgnoreCase) { "true", "yes", "y", "1", "x", "checked", "on" };

    private const string Css =
        "body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;line-height:1.5;max-width:48rem;margin:2rem auto;padding:0 1rem;color:#1a1a1a}" +
        "h1{font-weight:600}h2,h3,h4,h5,h6{margin-top:1.5rem}" +
        ".muted{color:#666}.field{margin:1rem 0}.field>label{display:block;font-weight:600;margin-bottom:.25rem}" +
        ".help{font-size:.85em;color:#666;margin:.15rem 0 0}" +
        "input[type=text],input[type=email],input[type=number],input[type=date],input[type=tel],input[type=url],textarea,select" +
        "{width:100%;box-sizing:border-box;padding:.4rem .5rem;font:inherit;border:1px solid #aaa;border-radius:4px}" +
        "input[type=checkbox]{width:auto;margin-right:.4rem}.check>label{font-weight:600}" +
        "textarea{min-height:4.5rem;resize:vertical}" +
        "table{border-collapse:collapse;width:100%;margin:.75rem 0}" +
        "th,td{border:1px solid #ccc;padding:.4rem .6rem;text-align:left;vertical-align:top}th{background:#f5f5f5}" +
        ".bar{position:sticky;bottom:0;background:#fff;border-top:1px solid #ddd;padding:1rem 0;margin-top:2rem}" +
        ".bar button{font:inherit;font-weight:600;padding:.55rem 1.1rem;border:0;border-radius:6px;background:#0b5fff;color:#fff;cursor:pointer}" +
        ".bar button:hover{background:#0848c4}";

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
        var embeddedJson = _serializer.Serialize(document);
        var html = BuildHtml(model, embeddedJson);

        var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(html);
        writer.Flush();
    }

    private static string BuildHtml(RenderModel model, string embeddedJson)
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

        sb.Append("<form id=\"apr-form\" onsubmit=\"return false\">\n");
        var fieldSeq = 0;
        foreach (var block in model.Blocks)
        {
            AppendBlock(sb, block, ref fieldSeq);
        }
        sb.Append("</form>\n");

        sb.Append("<div class=\"bar\"><button type=\"button\" id=\"apr-download\">Download filled form</button></div>\n");

        // The original document, embedded verbatim for the JS round-trip. Unicode-
        // escaped so no value can break out of the <script> container.
        sb.Append("<script type=\"application/json\" id=\"apr-document\">")
          .Append(EncodeForScript(embeddedJson))
          .Append("</script>\n");
        sb.Append("<script>\n").Append(DownloadScript).Append("</script>\n");

        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static void AppendBlock(StringBuilder sb, RenderBlock block, ref int seq)
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
                AppendField(sb, f, ++seq);
                break;

            case TableBlock t:
                AppendTable(sb, t);
                break;
        }
    }

    private static void AppendField(StringBuilder sb, FieldBlock f, int seq)
    {
        // A stable, valid element id; the prompt id is the round-trip key.
        var elementId = "f" + seq;
        var promptId = f.Id;
        var value = f.HasResponse ? f.Value : string.Empty;
        var dataType = f.ExpectedDataType ?? string.Empty;
        var help = !string.IsNullOrWhiteSpace(f.HelpText);
        var helpId = help ? elementId + "-help" : null;
        var describedBy = help ? " aria-describedby=\"" + helpId + "\"" : string.Empty;
        var dataAttr = promptId.Length == 0 ? string.Empty : " data-prompt-id=\"" + Enc(promptId) + "\"";

        if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            var isChecked = f.HasResponse && TruthyValues.Contains(value);
            sb.Append("<div class=\"field check\">");
            sb.Append("<input type=\"checkbox\" id=\"").Append(elementId).Append('"').Append(dataAttr)
              .Append(isChecked ? " checked" : string.Empty).Append(describedBy).Append(">");
            sb.Append("<label for=\"").Append(elementId).Append("\">").Append(Enc(f.Label)).Append("</label>");
            AppendHelp(sb, f, helpId);
            sb.Append("</div>\n");
            return;
        }

        sb.Append("<div class=\"field\">");
        sb.Append("<label for=\"").Append(elementId).Append("\">").Append(Enc(f.Label)).Append("</label>");

        if (f.Choices is { Count: > 0 })
        {
            sb.Append("<select id=\"").Append(elementId).Append('"').Append(dataAttr).Append(describedBy).Append(">");
            sb.Append("<option value=\"\">").Append(f.HasResponse ? string.Empty : "— choose —").Append("</option>");
            foreach (var choice in f.Choices)
            {
                var selected = f.HasResponse && string.Equals(choice, value, StringComparison.Ordinal) ? " selected" : string.Empty;
                sb.Append("<option value=\"").Append(Enc(choice)).Append('"').Append(selected).Append('>').Append(Enc(choice)).Append("</option>");
            }
            sb.Append("</select>");
        }
        else if (dataType.Equals("multiline", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<textarea id=\"").Append(elementId).Append('"').Append(dataAttr).Append(describedBy).Append('>')
              .Append(Enc(value)).Append("</textarea>");
        }
        else
        {
            sb.Append("<input type=\"").Append(InputType(dataType)).Append("\" id=\"").Append(elementId).Append('"')
              .Append(dataAttr).Append(" value=\"").Append(Enc(value)).Append('"').Append(describedBy).Append(">");
        }

        AppendHelp(sb, f, helpId);
        sb.Append("</div>\n");
    }

    private static void AppendHelp(StringBuilder sb, FieldBlock f, string? helpId)
    {
        if (helpId is null) return;
        sb.Append("<p class=\"help\" id=\"").Append(helpId).Append("\">").Append(Enc(f.HelpText!)).Append("</p>");
    }

    /// <summary>Maps an advisory APR data-type hint to an HTML input type.</summary>
    private static string InputType(string dataType) => dataType.ToLowerInvariant() switch
    {
        "email" => "email",
        "number" or "integer" or "decimal" or "currency" => "number",
        "date" => "date",
        "datetime" => "datetime-local",
        "time" => "time",
        "phone" or "tel" => "tel",
        "url" => "url",
        _ => "text",
    };

    private static void AppendTable(StringBuilder sb, TableBlock table)
    {
        // Read-only for now (per-cell prompts carry no id through the model).
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

    /// <summary>
    /// Escapes the embedded JSON so it cannot terminate its <c>&lt;script&gt;</c>
    /// container (<c>&lt;/script&gt;</c>, <c>&lt;!--</c>) while staying valid JSON.
    /// </summary>
    private static string EncodeForScript(string json) =>
        json.Replace("<", "\\u003c").Replace(">", "\\u003e").Replace("&", "\\u0026");

    // Vanilla JS, no dependencies: copy inputs back into the embedded document by
    // prompt id, mark it filled, and download as <title>.aprf.
    private const string DownloadScript = """
(function () {
  var raw = document.getElementById('apr-document').textContent;
  function collect() {
    var map = {};
    document.querySelectorAll('[data-prompt-id]').forEach(function (el) {
      var id = el.getAttribute('data-prompt-id');
      map[id] = el.type === 'checkbox' ? (el.checked ? 'true' : 'false') : el.value;
    });
    return map;
  }
  function apply(section, map, stamp) {
    (section.prompts || []).forEach(function (p) {
      if (Object.prototype.hasOwnProperty.call(map, p.id)) {
        p.response = map[p.id];
        p.responseMetadata = p.responseMetadata || {};
        p.responseMetadata.lastModified = stamp;
      }
    });
    (section.sections || []).forEach(function (s) { apply(s, map, stamp); });
  }
  document.getElementById('apr-download').addEventListener('click', function () {
    var doc = JSON.parse(raw);
    var map = collect();
    var stamp = new Date().toISOString();
    (doc.sections || []).forEach(function (s) { apply(s, map, stamp); });
    doc.documentType = 'filledForm';
    var name = (doc.metadata && doc.metadata.title ? doc.metadata.title : 'form').replace(/[\\/:*?"<>|]+/g, '_');
    var blob = new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = name + '.aprf';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 0);
  });
})();
""";
}
