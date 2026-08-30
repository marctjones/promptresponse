using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Desktop.Tests.Workflows;

public partial class WorkflowTests
{
    [Fact]
    public async Task FillOnlyYourOwnPartOfAMultiPartyForm()
    {
        var corpus = RepositoryPath("tests", "Conformance", "v1", "valid", "roles.aprt");
        _session.Set((await _files.LoadFileAsync(corpus))!, corpus);
        _shell.HasRoles.Should().BeTrue();
        _shell.ActiveRoleChoice = _shell.AvailableRoles.Single(r => r.Id == "patient");
        foreach (var mine in _shell.PromptViewModels.Where(p => p.IsMine)) mine.Response = "answered";
        var path = Path_("partly-filled.aprf");
        _session.CurrentDocument!.DocumentType = DocumentType.FilledForm;
        await _files.SaveFileAsync(_session.CurrentDocument!, path);
        var reloaded = Reload(path);
        MustBeValid(reloaded, "filling one party's share");
        reloaded.Sections.SelectMany(Flatten).Count(p => !string.IsNullOrEmpty(p.Response)).Should().Be(4,
            "the patient's three fields plus the unassigned consent question");
        static IEnumerable<Prompt> Flatten(Section s) => s.Prompts.Concat(s.Sections.SelectMany(Flatten));
    }

    [Fact]
    public void WalkAWholeFormThroughTheWizard()
    {
        _shell.NewTemplateCommand.Execute(null);
        foreach (var title in new[] { "Second", "Third" })
        {
            _shell.AddTopLevelSectionCommand.Execute(null);
            _shell.Sections[^1].Title = title;
        }
        _shell.Sections.Should().HaveCount(3);
        _shell.ToggleWizardModeCommand.Execute(null);
        _shell.IsWizardMode.Should().BeTrue();
        var visited = 1;
        while (_shell.WizardNextCommand.CanExecute(null)) { _shell.WizardNextCommand.Execute(null); visited++; }
        visited.Should().Be(3, "the wizard must reach every section, then stop");
        var back = 0;
        while (_shell.WizardPreviousCommand.CanExecute(null)) { _shell.WizardPreviousCommand.Execute(null); back++; }
        back.Should().Be(2, "and walk back to the first without running past it");
    }
}
