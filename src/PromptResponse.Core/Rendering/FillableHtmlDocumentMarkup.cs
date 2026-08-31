using System.Net;
using System.Text;

namespace PromptResponse.Core.Rendering;

/// <summary>Writes document-level structure shared by the fillable HTML form.</summary>
internal static class FillableHtmlDocumentMarkup
{
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

    public static void AppendStart(StringBuilder output, string title)
    {
        output.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n");
        output.Append("<meta charset=\"utf-8\">\n");
        output.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        output.Append("<title>").Append(Encode(title)).Append("</title>\n");
        output.Append("<style>").Append(Css).Append("</style>\n");
        output.Append("</head>\n<body>\n");
    }

    public static void AppendFormStart(StringBuilder output, RenderModel model, string title)
    {
        output.Append("<h1>").Append(Encode(title)).Append("</h1>\n");
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            output.Append("<p class=\"muted\">").Append(Encode(model.Description!)).Append("</p>\n");
        }

        output.Append("<form id=\"apr-form\" onsubmit=\"return false\">\n");
    }

    public static void AppendFormEnd(StringBuilder output) => output.Append("</form>\n");

    public static void AppendHeading(StringBuilder output, HeadingBlock heading)
    {
        var tag = "h" + Math.Clamp(heading.Level + 1, 2, 6); // document title is h1
        output.Append('<').Append(tag).Append('>').Append(Encode(heading.Text)).Append("</").Append(tag).Append(">\n");
        if (!string.IsNullOrWhiteSpace(heading.Description))
        {
            output.Append("<p class=\"muted\">").Append(Encode(heading.Description!)).Append("</p>\n");
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
