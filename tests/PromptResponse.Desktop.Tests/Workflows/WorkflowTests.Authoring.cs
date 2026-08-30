using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Workflows;

public partial class WorkflowTests
{
    /// <summary>New template, build it, save it, open it again.</summary>
    [Fact]
    public async Task AuthorATemplateFromNothing_AndReopenIt()
    {
        _shell.NewTemplateCommand.Execute(null);
        _shell.HasDocument.Should().BeTrue("a new template opens a document");
        _session.Mode.Should().Be(DocumentMode.EditingTemplate);
        var section = _shell.Sections.Should().ContainSingle().Subject;
        section.Title = "Applicant";
        var prompt = section.PromptViewModels.Should().ContainSingle().Subject;
        prompt.Label = "Full name";
        prompt.ExpectedDataType = "text";
        prompt.HelpText = "As it appears on your passport.";
        var second = section.AddPrompt();
        second.Label = "Date of birth";
        second.ExpectedDataType = "date";
        var path = Path_("authored.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);
        var reloaded = Reload(path);
        MustBeValid(reloaded, "authoring a template");
        reloaded.DocumentType.Should().Be(DocumentType.Template);
        var saved = reloaded.Sections.Should().ContainSingle().Subject;
        saved.Title.Should().Be("Applicant");
        saved.Prompts.Select(p => p.Label).Should().Equal("Full name", "Date of birth");
        saved.Prompts[0].Hints.HelpText.Should().Be("As it appears on your passport.");
        saved.Prompts[1].Hints.ExpectedDataType.Should().Be("date");
    }

    /// <summary>Add sections and prompts, undo the lot, redo the lot.</summary>
    [Fact]
    public void UndoAndRedoAWholeAuthoringSession()
    {
        _shell.NewTemplateCommand.Execute(null);
        var before = _serializer.Serialize(_session.CurrentDocument!);
        var section = _shell.Sections[0];
        section.Title = "Employment";
        section.AddPrompt().Label = "Employer";
        section.AddPrompt().Label = "Role";
        var nested = section.AddNestedSection();
        nested.Title = "Previous employer";
        var after = _serializer.Serialize(_session.CurrentDocument!);
        after.Should().NotBe(before, "the session changed the document");
        while (_shell.UndoCommand.CanExecute(null)) _shell.UndoCommand.Execute(null);
        _serializer.Serialize(_session.CurrentDocument!).Should().Be(before,
            "undoing every step must land exactly where the session started");
        while (_shell.RedoCommand.CanExecute(null)) _shell.RedoCommand.Execute(null);
        _serializer.Serialize(_session.CurrentDocument!).Should().Be(after,
            "and redoing every step must land exactly where it ended");
    }

    /// <summary>Build a table, give it a column and rows, save, reopen.</summary>
    [Fact]
    public async Task BuildATable_FillItsCells_AndReopenIt()
    {
        _shell.NewTemplateCommand.Execute(null);
        var section = _shell.Sections[0];
        section.Title = "Line items";
        section.ConvertToDynamicTable();
        section.IsTableSection.Should().BeTrue();
        section.AddColumn();
        section.AddRow();
        section.Columns.Should().HaveCount(2);
        section.NestedSections.Should().HaveCount(2, "a starter row plus the one just added");
        foreach (var (row, index) in section.NestedSections.Select((r, i) => (r, i)))
        foreach (var (cell, column) in row.PromptViewModels.Select((c, j) => (c, j)))
            cell.Response = $"r{index}c{column}";
        var path = Path_("table.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);
        var reloaded = Reload(path);
        MustBeValid(reloaded, "building a table");
        var table = reloaded.Sections.Should().ContainSingle().Subject;
        table.Kind.Should().Be("table");
        table.Sections.Should().HaveCount(2);
        table.Sections.SelectMany(r => r.Prompts).Select(p => p.Response)
            .Should().Equal("r0c0", "r0c1", "r1c0", "r1c1");
        table.Sections.SelectMany(r => r.Prompts).Select(p => p.Id)
            .Should().OnlyHaveUniqueItems("cells share the document's id namespace");
    }
}
