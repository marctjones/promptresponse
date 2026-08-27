using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// What the APR document knows must reach the PDF field, not just the printed page.
/// </summary>
/// <remarks>
/// A fillable export is often the last artefact anyone sees: it gets emailed, filled in
/// Acrobat, and returned, with the .aprf never opened. So whatever the field's tooltip
/// (<c>/TU</c>) carries is all the guidance a filler gets, and it is also the field's
/// accessible name in readers that expose one.
///
/// The label alone was going in - telling a hovering user the words already printed
/// beside the box - while the author's helpText reached the page and stopped there.
/// </remarks>
public class FieldTooltipTests
{
    private static byte[] Render(AprDocument document)
    {
        using var output = new MemoryStream();
        new FillablePdfDocumentRenderer().Render(document, RenderOptions.Default, output);
        return output.ToArray();
    }

    /// <summary>PDF text strings are UTF-16BE with octal escapes; decode before asserting.</summary>
    private static List<string> Tooltips(byte[] pdf)
    {
        static byte[] Unescape(string raw)
        {
            var outBytes = new List<byte>();
            for (var i = 0; i < raw.Length;)
            {
                if (raw[i] == '\\' && i + 3 < raw.Length && char.IsDigit(raw[i + 1]))
                {
                    outBytes.Add(Convert.ToByte(raw.Substring(i + 1, 3), 8)); i += 4;
                }
                else if (raw[i] == '\\') { outBytes.Add((byte)raw[i + 1]); i += 2; }
                else { outBytes.Add((byte)raw[i]); i++; }
            }
            return [.. outBytes];
        }

        var text = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(text, @"/TU\s*\(((?:\\.|[^()\\])*)\)", RegexOptions.Singleline)
            .Select(m => Unescape(m.Groups[1].Value))
            .Select(b => b.Length > 1 && b[0] == 0xFE && b[1] == 0xFF
                ? Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2)
                : Encoding.Latin1.GetString(b))
            .ToList();
    }

    private static AprDocument Form(params Prompt[] prompts) => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Tooltips" },
        Sections = [new Section { Id = "s", Title = "S", Prompts = [.. prompts] }],
    };

    [Fact]
    public void AFieldsGuidance_ReachesItsTooltip()
    {
        var pdf = Render(Form(new Prompt
        {
            Id = "ssn", Label = "Social Security Number",
            Hints = new PromptHints
            {
                ExpectedDataType = "text",
                HelpText = "Enter your 9-digit Social Security Number",
            },
        }));

        Tooltips(pdf).Should().Contain(
            "Social Security Number — Enter your 9-digit Social Security Number",
            "a fillable export is often the last artefact anyone sees, so the author's " +
            "guidance has to travel with the field rather than staying on the page");
    }

    [Fact]
    public void TheLabelLeads_SoTheFieldStaysIdentifiable()
    {
        var pdf = Render(Form(new Prompt
        {
            Id = "dob", Label = "Date of Birth",
            Hints = new PromptHints { HelpText = "Use YYYY-MM-DD if you can." },
        }));

        Tooltips(pdf).Should().ContainSingle(t => t.StartsWith("Date of Birth", StringComparison.Ordinal),
            "/TU is the field's accessible name; leading with the guidance would leave a " +
            "screen-reader user advice about a field they can no longer identify");
    }

    [Fact]
    public void AFieldWithNoGuidance_KeepsItsPlainLabel()
    {
        var pdf = Render(Form(new Prompt { Id = "name", Label = "Full name" }));

        Tooltips(pdf).Should().Contain("Full name")
            .And.NotContain(t => t.StartsWith("Full name —", StringComparison.Ordinal),
                "no guidance means no separator dangling off the end of the label");
    }

    [Theory]
    [InlineData("boolean", "Do you consent?")]
    [InlineData("multiline", "Describe the incident")]
    public void EveryFieldKind_CarriesItsGuidance(string dataType, string label)
    {
        var pdf = Render(Form(new Prompt
        {
            Id = "f", Label = label,
            Hints = new PromptHints { ExpectedDataType = dataType, HelpText = "Take your time." },
        }));

        Tooltips(pdf).Should().Contain($"{label} — Take your time.",
            $"a {dataType} field is no less in need of its author's guidance");
    }

    [Fact]
    public void AChoiceField_CarriesGuidanceAndItsOptions()
    {
        var pdf = Render(Form(new Prompt
        {
            Id = "dept", Label = "Department",
            Hints = new PromptHints
            {
                ExpectedDataType = "select",
                SuggestedValues = ["Sales", "Finance"],
                HelpText = "Pick the one that pays you.",
            },
        }));

        Tooltips(pdf).Should().Contain("Department — Pick the one that pays you.");
        Encoding.Latin1.GetString(pdf).Should().Contain("/Opt",
            "the offered options travel too, so the dropdown is usable in a PDF reader");
    }
}
