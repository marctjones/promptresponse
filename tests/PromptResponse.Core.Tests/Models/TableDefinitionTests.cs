using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Vision-anchored tests for <see cref="TableDefinition"/>:
/// tables are semantic structures only. The schema must not carry display info.
/// Cells hold values and only values.
/// </summary>
public class TableDefinitionTests
{
    private readonly AprJsonSerializer _serializer = new();

    [Fact]
    public void TableColumn_ShouldNotExposeDisplayProperties()
    {
        // Vision: tables describe rows × columns of meaningful data, not how
        // they look. Width, alignment, color, font, etc. belong in the renderer's
        // stylesheet, never in .apr.
        var displayProperties = new[] { "Width", "Alignment", "Color", "Background", "FontSize", "Bold", "Style" };
        var columnProps = typeof(TableColumn).GetProperties().Select(p => p.Name).ToList();

        foreach (var forbidden in displayProperties)
        {
            columnProps.Should().NotContain(forbidden,
                $"TableColumn.{forbidden} would encode display in the schema, violating the 'no layout' rule");
        }
    }

    [Fact]
    public void TableColumn_RoundTrip_ShouldDropUnknownDisplayFields()
    {
        // A renderer that wrote display-only fields like "width" or "alignment"
        // on a column must lose them on read/write — the schema doesn't recognize
        // them, so they must not survive a round trip.
        var docWithDisplayFields = """
            {
              "version": "1.0",
              "documentType": "template",
              "metadata": { "title": "T" },
              "sections": [{
                "id": "s1",
                "title": "S",
                "tableLayout": {
                  "columns": [
                    { "id": "q1", "label": "Q1", "type": "currency", "width": "25%" },
                    { "id": "q2", "label": "Q2", "type": "currency", "width": "100px" }
                  ],
                  "fixedRows": [{ "id": "r1", "label": "R1" }]
                },
                "sections": [{
                  "id": "r1",
                  "title": "R1",
                  "prompts": [
                    { "id": "r1.q1", "label": "Q1", "response": "", "hints": { "expectedDataType": "currency" } },
                    { "id": "r1.q2", "label": "Q2", "response": "", "hints": { "expectedDataType": "currency" } }
                  ]
                }]
              }]
            }
            """;

        var doc = _serializer.Deserialize(docWithDisplayFields);
        var json = _serializer.Serialize(doc);

        json.Should().NotContain("\"width\"", "round-trip must drop display-only fields like width");
        json.Should().NotContain("\"25%\"");
        json.Should().NotContain("\"100px\"");

        // The semantic column metadata survives.
        var rt = _serializer.Deserialize(json);
        var cols = rt.Sections[0].TableLayout!.Columns;
        cols.Should().HaveCount(2);
        cols[0].Id.Should().Be("q1");
        cols[0].Label.Should().Be("Q1");
        cols[0].Type.Should().Be("currency");
    }
}
