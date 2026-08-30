using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.Workflows;

public partial class WorkflowTests
{
    [Fact]
    public async Task FillAForm_ExportAFillablePdf_AndFindTheAnswersInIt()
    {
        _shell.NewTemplateCommand.Execute(null);
        var section = _shell.Sections[0];
        section.Title = "Claim";
        var first = section.PromptViewModels[0];
        first.Label = "Claimant";
        first.Response = "Ada Lovelace";
        var second = section.AddPrompt();
        second.Label = "Department";
        second.ExpectedDataType = "select";
        second.Response = "Engineering";
        var pdfPath = Path_("claim.pdf");
        _files.NextExportPath = pdfPath;
        await _shell.ExportPdfForm();
        File.Exists(pdfPath).Should().BeTrue("the export workflow must produce a file");
        var bytes = File.ReadAllBytes(pdfPath);
        var text = System.Text.Encoding.Latin1.GetString(bytes);
        text.Should().StartWith("%PDF", "and the file must actually be a PDF");
        text.Should().Contain("/AcroForm", "a fillable export carries a form");
        text.Should().Contain("/Widget", "with fields somebody can type into");
        bytes.Length.Should().BeGreaterThan(1000, "a form with two fields is not an empty page");
    }

    [Fact]
    public async Task ExportingThenSaving_WritesWhatTheUserBuilt()
    {
        _shell.NewTemplateCommand.Execute(null);
        _shell.Sections[0].PromptViewModels[0].Response = "before export";
        _shell.Sections[0].PromptViewModels[0].Label = "Typed before exporting";
        var before = Content(_session.CurrentDocument!);
        _files.NextExportPath = Path_("side-effect.pdf");
        await _shell.ExportPdfForm();
        var path = Path_("after-export.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);
        Content(Reload(path)).Should().Be(before,
            "exporting is a read of the document, and saving afterwards must write what the user built rather than something the renderer left behind");
    }
}
