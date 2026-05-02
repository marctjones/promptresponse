using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Failing tests for live-update bugs the user reported in the live app:
///   * Progress bar doesn't move as the user types into prompts
///   * Advisories don't auto-refresh when responses change
///   * Advisories don't link back to the prompt that triggered them
/// </summary>
public class LiveUpdateBugTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static MainShellViewModel BuildShell(AprDocument document)
    {
        var fileService = new Mock<IFileService>();
        var dialog = new Mock<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var shell = new MainShellViewModel(fileService.Object, dialog.Object, session, profile, factory);
        session.Set(document, "test.aprf");
        return shell;
    }

    private static AprDocument SimpleFilledForm(string firstResponse = "", string secondResponse = "")
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "s1", Title = "Contact",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "name", Label = "Name", Response = firstResponse, Hints = new PromptHints() },
                        new Prompt { Id = "age", Label = "Age", Response = secondResponse,
                                     Hints = new PromptHints { ExpectedDataType = "number" } },
                    },
                },
            },
        };
    }

    [Fact]
    public void Progress_AnsweringFirstPrompt_UpdatesPercentLive()
    {
        var shell = BuildShell(SimpleFilledForm());
        shell.Progress.PercentComplete.Should().Be(0, "no responses yet");
        shell.Progress.AnsweredPrompts.Should().Be(0);

        // Type a response into the first prompt VM (matches what TypeText into the
        // bound TextBox eventually does — sets Response on the VM).
        shell.PromptViewModels.First().Response = "Marc";

        shell.Progress.AnsweredPrompts.Should().Be(1,
            "BUG: progress should refresh as soon as a prompt's Response changes");
        shell.Progress.PercentComplete.Should().Be(50, "1 of 2 prompts answered");
    }

    [Fact]
    public void Progress_ClearingResponse_DropsCountLive()
    {
        var shell = BuildShell(SimpleFilledForm("Marc", "42"));
        shell.Progress.AnsweredPrompts.Should().Be(2, "loaded with both responses set");

        shell.PromptViewModels.First().Response = string.Empty;

        shell.Progress.AnsweredPrompts.Should().Be(1,
            "BUG: clearing a response must drop the live count immediately");
    }

    [Fact]
    public void Advisories_TypingNonNumericIntoNumberPrompt_AutoIncrementsCount()
    {
        // "five" in a number-hinted field should raise an advisory ("looks non-numeric").
        var shell = BuildShell(SimpleFilledForm());
        shell.AdvisoryCount.Should().Be(0, "empty fields raise no advisory");

        var ageVm = shell.PromptViewModels.First(vm => vm.Id == "age");
        ageVm.Response = "five";

        shell.AdvisoryCount.Should().BeGreaterThan(0,
            "BUG: typing a non-numeric value into a number-hinted prompt must raise an " +
            "advisory live; today the user has to click Refresh");
    }

    [Fact]
    public void Advisories_FixingInvalidResponse_AutoClears()
    {
        var doc = SimpleFilledForm(secondResponse: "five");
        var shell = BuildShell(doc);
        shell.AdvisoryCount.Should().BeGreaterThan(0, "loaded with non-numeric in number field");

        var ageVm = shell.PromptViewModels.First(vm => vm.Id == "age");
        ageVm.Response = "42";

        shell.AdvisoryCount.Should().Be(0,
            "BUG: correcting the value must clear the advisory immediately, not on next refresh");
    }

    [Fact]
    public void Advisories_ExposeIndividualItems_NotJustACount()
    {
        // The user can't tell WHICH field has an advisory or WHY without an itemized list.
        var doc = SimpleFilledForm(secondResponse: "five");
        var shell = BuildShell(doc);

        shell.Advisories.Should().NotBeNull("BUG: there's no Advisories list — only a count");
        shell.Advisories.Should().NotBeEmpty();
        var first = shell.Advisories.First();
        first.PromptId.Should().Be("age", "the advisory must point back to the prompt that triggered it");
        first.PromptLabel.Should().Be("Age", "the advisory must surface the prompt's user-visible label");
        first.Message.Should().NotBeNullOrWhiteSpace("the advisory must explain WHY it was raised");
    }
}
