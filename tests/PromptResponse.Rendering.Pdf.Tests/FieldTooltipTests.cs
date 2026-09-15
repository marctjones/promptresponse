using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;
using PdfeDoc = Excise.Core.Document.PdfDocument;

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

    /// <summary>
    /// Every field's accessible name (<c>/TU</c>), read through the same public API a
    /// consumer would use.
    /// </summary>
    /// <remarks>
    /// This used to regex the saved bytes for <c>/TU (...)</c> and hand-decode the
    /// UTF-16BE octal escapes. That assertion could not tell "the tooltip is missing"
    /// apart from "the tooltip is in a carrier I cannot read", and the difference
    /// mattered: when the PDF engine began packing AcroForm dictionaries into
    /// Flate-compressed object streams, all six tests here went red against output
    /// that was perfectly correct — the data was always there, `qpdf --qdf` showed it.
    /// Cost three reverted dependency upgrades and three upstream bug reports to work
    /// out. Reopening the document and reading /TU off the field dictionary asserts
    /// the requirement (a filler and a screen reader can get the guidance) rather than
    /// an incidental fact about how the bytes happen to be packed.
    /// </remarks>
    private static List<string> Tooltips(byte[] pdf)
    {
        using var doc = PdfeDoc.Open(pdf);
        var form = doc.GetAcroForm();
        form.Should().NotBeNull("a fillable export must carry an AcroForm");
        return form!.Fields
            .Select(f => f.RawDictionary.GetStringOrNull("TU"))
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!)
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
        using var doc = PdfeDoc.Open(pdf);
        var acro = doc.GetAcroForm();
        acro.Should().NotBeNull();
        acro!.Fields.Should().ContainSingle(f => f.Options != null && f.Options.Count > 0)
            .Which.Options.Should().Contain(["Sales", "Finance"],
                "the offered options travel too, so the dropdown is usable in a PDF reader");
    }
}
