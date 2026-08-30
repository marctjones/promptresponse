using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Workflows;

public partial class WorkflowTests
{
    /// <summary>Open a template to fill it, answer it, save it as a filled form.</summary>
    [Fact]
    public async Task FillATemplate_AndSaveItAsAFilledForm()
    {
        var templatePath = Path_("blank.aprt");
        File.WriteAllText(templatePath, _serializer.Serialize(new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Intake", TemplateId = "intake", TemplateVersion = "1.0" },
            Sections = [new Section { Id = "s", Title = "About you", Prompts =
            [new Prompt { Id = "name", Label = "Name" }, new Prompt { Id = "email", Label = "Email", Hints = new PromptHints { ExpectedDataType = "email" } }] }],
        }));
        var loaded = await _files.LoadFileAsync(templatePath);
        _session.Set(loaded!, templatePath);
        foreach (var (prompt, answer) in _shell.PromptViewModels.Zip(new[] { "Ada Lovelace", "ada@example.com" }))
            prompt.Response = answer;
        _session.CurrentDocument!.DocumentType = DocumentType.FilledForm;
        var filledPath = Path_("answered.aprf");
        await _files.SaveFileAsync(_session.CurrentDocument!, filledPath);
        var reloaded = Reload(filledPath);
        MustBeValid(reloaded, "filling a form");
        reloaded.DocumentType.Should().Be(DocumentType.FilledForm);
        reloaded.Sections[0].Prompts.Select(p => p.Response).Should().Equal("Ada Lovelace", "ada@example.com");
        reloaded.Metadata.TemplateId.Should().Be("intake", "a filled form remembers which template it answers");
    }

    [Fact]
    public async Task FillSf86Template_SaveFilledCopy_AndReopenIt()
    {
        var source = RepositoryPath("examples", "sf-86-background-check.aprt");
        var filledPath = Path_("sf-86-filled.aprf");
        await _shell.OpenFromPath(source, openForFilling: true);
        _shell.IsEditMode.Should().BeFalse("--open is the form-filling entry point");
        _shell.ShowFullFillList.Should().BeTrue();
        _shell.PromptViewModels.Should().HaveCount(111);
        _shell.PromptViewModels.Single(p => p.Id == "prompt_investigation_type").Response = "Initial Investigation";
        _session.CurrentDocument!.DocumentType = DocumentType.FilledForm;
        await _files.SaveFileAsync(_session.CurrentDocument, filledPath);
        var reloaded = Reload(filledPath);
        MustBeValid(reloaded, "filling the SF-86 template");
        reloaded.DocumentType.Should().Be(DocumentType.FilledForm);
        reloaded.Metadata.TemplateId.Should().Be("sf-86-2024");
        reloaded.Sections.SelectMany(Flatten).Single(p => p.Id == "prompt_investigation_type").Response.Should().Be("Initial Investigation");
        static IEnumerable<Prompt> Flatten(Section s) => s.Prompts.Concat(s.Sections.SelectMany(Flatten));
    }

    [Fact]
    public async Task AnswerADateFieldInPlainEnglish_AndItSurvivesTheRoundTrip()
    {
        _shell.NewTemplateCommand.Execute(null);
        var prompt = _shell.Sections[0].PromptViewModels[0];
        prompt.Label = "When did it happen?";
        prompt.ExpectedDataType = "date";
        prompt.Response = "some time last spring, I think";
        var path = Path_("freetext.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);
        var reloaded = Reload(path);
        MustBeValid(reloaded, "answering a date field in prose");
        reloaded.Sections[0].Prompts[0].Response.Should().Be("some time last spring, I think",
            "a type hint suggests an affordance; it never restricts what may be written");
    }

    [Fact]
    public async Task ReopenAFilledForm_ChangeAnAnswer_AndSaveOverIt()
    {
        var path = Path_("existing.aprf");
        File.WriteAllText(path, _serializer.Serialize(new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Claim", TemplateId = "c", TemplateVersion = "1.0" },
            Sections = [new Section { Id = "s", Title = "Claim", Prompts = [new Prompt { Id = "amount", Label = "Amount", Response = "100" }] }],
        }));
        _session.Set((await _files.LoadFileAsync(path))!, path);
        _session.Mode.Should().Be(DocumentMode.FillingForm, "a filled form opens ready to fill, not to restructure");
        _shell.PromptViewModels.Single().Response = "250";
        await _files.SaveFileAsync(_session.CurrentDocument!, path);
        Reload(path).Sections[0].Prompts[0].Response.Should().Be("250");
    }
}
