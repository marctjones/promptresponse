using FluentAssertions;
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
        // A renderer that historically wrote a "width" field must lose it on read/write —
        // the schema doesn't recognize it, so it must not survive a round trip.
        var docWithLegacyWidth = """
            {
              "version": "1.0",
              "documentType": "template",
              "metadata": { "title": "T" },
              "sections": [{
                "id": "s1",
                "title": "S",
                "prompts": [{
                  "id": "p1",
                  "label": "Quarterly",
                  "hints": {
                    "expectedDataType": "table",
                    "tableDefinition": {
                      "columns": [
                        { "id": "q1", "label": "Q1", "type": "currency", "width": "25%" },
                        { "id": "q2", "label": "Q2", "type": "currency", "width": "100px" }
                      ]
                    }
                  }
                }]
              }]
            }
            """;

        var doc = _serializer.Deserialize(docWithLegacyWidth);
        var json = _serializer.Serialize(doc);

        json.Should().NotContain("\"width\"", "round-trip must drop display-only fields like width");
        json.Should().NotContain("\"25%\"");
        json.Should().NotContain("\"100px\"");

        // The semantic content survives intact
        var rt = _serializer.Deserialize(json);
        var cols = rt.Sections[0].Prompts[0].Hints.TableDefinition!.Columns;
        cols.Should().HaveCount(2);
        cols[0].Id.Should().Be("q1");
        cols[0].Label.Should().Be("Q1");
        cols[0].Type.Should().Be("currency");
    }
}
