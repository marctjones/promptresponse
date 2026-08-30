using System.Net;
using System.Text;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Writes live field and table controls for a fillable APR page. Dynamic values
/// are HTML-encoded at this markup boundary before they enter an attribute or element.
/// </summary>
internal static class FillableHtmlFieldMarkup
{
    private static readonly HashSet<string> TruthyValues =
        new(StringComparer.OrdinalIgnoreCase) { "true", "yes", "y", "1", "x", "checked", "on" };

    public static void AppendField(StringBuilder output, FieldBlock field, int sequence)
    {
        var elementId = "f" + sequence;
        var promptId = field.Id;
        var value = field.HasResponse ? field.Value : string.Empty;
        var dataType = field.ExpectedDataType ?? string.Empty;
        var hasHelp = !string.IsNullOrWhiteSpace(field.HelpText);
        var helpId = hasHelp ? elementId + "-help" : null;
        var describedBy = hasHelp ? " aria-describedby=\"" + helpId + "\"" : string.Empty;
        var dataAttribute = promptId.Length == 0 ? string.Empty : " data-prompt-id=\"" + Encode(promptId) + "\"";

        if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            var isChecked = field.HasResponse && TruthyValues.Contains(value);
            output.Append("<div class=\"field check\">");
            output.Append("<input type=\"checkbox\" id=\"").Append(elementId).Append('\"').Append(dataAttribute)
                .Append(isChecked ? " checked" : string.Empty).Append(describedBy).Append(">");
            output.Append("<label for=\"").Append(elementId).Append("\">").Append(Encode(field.Label)).Append("</label>");
            AppendHelp(output, field.HelpText, helpId);
            output.Append("</div>\n");
            return;
        }

        output.Append("<div class=\"field\">");
        output.Append("<label for=\"").Append(elementId).Append("\">").Append(Encode(field.Label)).Append("</label>");
        if (field.Choices is { Count: > 0 })
        {
            output.Append("<select id=\"").Append(elementId).Append('\"').Append(dataAttribute).Append(describedBy).Append(">");
            AppendSelectOptions(output, field.Choices, value, field.HasResponse, "— choose —");
            output.Append("</select>");
        }
        else if (dataType.Equals("multiline", StringComparison.OrdinalIgnoreCase))
        {
            output.Append("<textarea id=\"").Append(elementId).Append('\"').Append(dataAttribute).Append(describedBy).Append('>')
                .Append(Encode(value)).Append("</textarea>");
        }
        else
        {
            output.Append("<input type=\"").Append(InputType(dataType)).Append("\" id=\"").Append(elementId).Append('\"')
                .Append(dataAttribute).Append(" value=\"").Append(Encode(value)).Append('\"').Append(describedBy).Append(">");
        }

        AppendHelp(output, field.HelpText, helpId);
        output.Append("</div>\n");
    }

    public static void AppendTable(StringBuilder output, TableBlock table, ref int sequence)
    {
        output.Append("<table>\n<thead>\n<tr><th></th>");
        foreach (var column in table.ColumnHeaders)
        {
            output.Append("<th scope=\"col\">").Append(Encode(column)).Append("</th>");
        }
        output.Append("</tr>\n</thead>\n<tbody>\n");
        foreach (var row in table.Rows)
        {
            output.Append("<tr><th scope=\"row\">").Append(Encode(row.Label)).Append("</th>");
            for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
            {
                var cell = row.Cells[columnIndex];
                var columnHeader = columnIndex < table.ColumnHeaders.Count ? table.ColumnHeaders[columnIndex] : string.Empty;
                output.Append("<td>");
                AppendCellInput(output, cell, row.Label, columnHeader, ++sequence);
                output.Append("</td>");
            }
            output.Append("</tr>\n");
        }
        output.Append("</tbody>\n</table>\n");
    }

    private static void AppendHelp(StringBuilder output, string? helpText, string? helpId)
    {
        if (helpId is null) return;
        output.Append("<p class=\"help\" id=\"").Append(helpId).Append("\">").Append(Encode(helpText!)).Append("</p>");
    }

    private static void AppendCellInput(StringBuilder output, TableCellBlock cell, string rowLabel, string columnHeader, int sequence)
    {
        if (cell.Id.Length == 0)
        {
            output.Append(Encode(cell.Value));
            return;
        }

        var elementId = "f" + sequence;
        var value = cell.HasResponse ? cell.Value : string.Empty;
        var dataType = cell.ExpectedDataType ?? string.Empty;
        var ariaLabel = Encode((rowLabel + " " + columnHeader).Trim());
        var common = " id=\"" + elementId + "\" data-prompt-id=\"" + Encode(cell.Id) + "\" aria-label=\"" + ariaLabel + "\"";

        if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            var isChecked = cell.HasResponse && TruthyValues.Contains(value);
            output.Append("<input type=\"checkbox\"").Append(common).Append(isChecked ? " checked" : string.Empty).Append('>');
        }
        else if (cell.Choices is { Count: > 0 })
        {
            output.Append("<select").Append(common).Append('>');
            AppendSelectOptions(output, cell.Choices, value, cell.HasResponse, "—");
            output.Append("</select>");
        }
        else
        {
            output.Append("<input type=\"").Append(InputType(dataType)).Append('\"').Append(common)
                .Append(" value=\"").Append(Encode(value)).Append("\">");
        }
    }

    private static void AppendSelectOptions(StringBuilder output, IReadOnlyList<string> choices, string value, bool hasResponse, string emptyOptionText)
    {
        output.Append("<option value=\"\">").Append(hasResponse ? string.Empty : emptyOptionText).Append("</option>");
        foreach (var choice in choices)
        {
            var selected = hasResponse && string.Equals(choice, value, StringComparison.Ordinal) ? " selected" : string.Empty;
            output.Append("<option value=\"").Append(Encode(choice)).Append('\"').Append(selected).Append('>').Append(Encode(choice)).Append("</option>");
        }
    }

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

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
