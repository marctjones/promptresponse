using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Vision-anchored tests for table sections: a table is a semantic claim about
/// structure, carries no display information, and introduces no new primitive.
/// </summary>
public class TableSectionTests
{
    private readonly AprJsonSerializer _serializer = new();

    [Fact]
    public void Section_ShouldNotExposeDisplayProperties()
    {
        // Width, alignment, colour and friends belong in a renderer's stylesheet,
        // never in .apr. A table is rows × fields of meaningful data, not a picture.
        var displayProperties = new[] { "Width", "Alignment", "Color", "Background", "FontSize", "Bold", "Style" };
        var sectionProps = typeof(Section).GetProperties().Select(p => p.Name).ToList();

        foreach (var forbidden in displayProperties)
        {
            sectionProps.Should().NotContain(forbidden,
                $"Section.{forbidden} would encode display in the schema, violating the 'no layout' rule");
        }
    }

    [Fact]
    public void TableIsASection_AndCellsArePrompts_NoNewPrimitives()
    {
        var section = new Section { Id = "t", Title = "T", Kind = "table" };

        section.IsTable.Should().BeTrue();
        section.Sections.Should().BeOfType<List<Section>>("rows are ordinary sections");
        section.Prompts.Should().BeOfType<List<Prompt>>("cells are ordinary prompts");
    }

    [Fact]
    public void CanAddRows_DefaultsToFixed()
    {
        // Fixed is the safe failure: a tax-year table that silently gained a row is a
        // worse outcome than a line-item table needing one explicit property.
        new Section { Kind = "table" }.AllowsAddingRows.Should().BeFalse();
        new Section { Kind = "table", CanAddRows = "true" }.AllowsAddingRows.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_DropsRetiredDisplayFields_ButKeepsTheStructure()
    {
        // "width" was retired from the format, so it is dropped rather than preserved
        // as an unknown member — otherwise removing it would never take effect.
        var json = """
            {
              "version": "1.0-beta",
              "documentType": "template",
              "metadata": { "title": "T" },
              "sections": [{
                "id": "s1",
                "title": "S",
                "kind": "table",
                "sections": [{
                  "id": "r1",
                  "title": "R1",
                  "prompts": [
                    { "id": "r1.q1", "label": "Q1", "response": "", "width": "25%",
                      "hints": { "expectedDataType": "currency" } },
                    { "id": "r1.q2", "label": "Q2", "response": "", "hints": { "expectedDataType": "currency" } }
                  ]
                }]
              }]
            }
            """;

        var roundTripped = _serializer.Serialize(_serializer.Deserialize(json));

        roundTripped.Should().NotContain("\"width\"", "retired display fields must not survive a round-trip");
        roundTripped.Should().NotContain("\"25%\"");

        var doc = _serializer.Deserialize(roundTripped);
        var table = doc.Sections[0];
        table.IsTable.Should().BeTrue();

        // The column headers are the cell prompts' labels — there is no separate
        // column record that could disagree with them.
        var cells = table.Sections[0].Prompts;
        cells.Should().HaveCount(2);
        cells[0].Label.Should().Be("Q1");
        cells[0].Hints.ExpectedDataType.Should().Be("currency");
    }
}
