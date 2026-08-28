using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Did the form work this value out, or did you?
/// </summary>
/// <remarks>
/// <para>
/// A computed field stays editable (specification 8.6), and a correction survives
/// recomputation because responseMetadata.source records who wrote the value. All of that
/// machinery existed and none of it was visible: somebody looking at a total could not
/// tell whether the form had calculated it, and somebody who corrected one had no way to
/// know their answer would not be quietly reverted on the next pass.
/// </para>
/// <para>
/// An invisible guarantee is not much of a guarantee. Somebody who does not trust it will
/// keep re-checking a number that is never going to change back.
/// </para>
/// </remarks>
public class ProvenanceDisplayTests
{
    private sealed class Probe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    /// <summary>Subtotal + tax, with total computed from them.</summary>
    private static AprDocument Claim() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Claim", TemplateId = "c", TemplateVersion = "1.0" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Claim",
                Prompts =
                [
                    new Prompt { Id = "subtotal", Label = "Subtotal", Response = "100.00",
                        Hints = new PromptHints { ExpectedDataType = "currency" } },
                    new Prompt { Id = "tax", Label = "Tax", Response = "20.00",
                        Hints = new PromptHints { ExpectedDataType = "currency" } },
                    new Prompt { Id = "total", Label = "Total",
                        Hints = new PromptHints
                        {
                            ExpectedDataType = "currency",
                            ExprValue = "string(double(subtotal) + double(tax))",
                        } },
                    new Prompt { Id = "notes", Label = "Notes", Response = "nothing special" },
                ],
            },
        ],
    };

    private static MainShellViewModel ShellOver(AprDocument document)
    {
        var session = new DocumentSessionService();
        var profile = new ProfileService(new Probe(), applyAffordanceDefaults: false);
        var shell = new MainShellViewModel(Substitute.For<IFileService>(),
            Substitute.For<IDialogService>(), session, profile, new PromptViewModelFactory(profile));
        session.Set(document, null);
        return shell;
    }

    private static PromptViewModelBase Field(MainShellViewModel shell, string id) =>
        shell.PromptViewModels.Single(p => p.Id == id);

    [Fact]
    public void AnOrdinaryFieldSaysNothingAboutProvenance()
    {
        var shell = ShellOver(Claim());

        var notes = Field(shell, "notes");
        notes.IsComputedField.Should().BeFalse();
        notes.ShowProvenanceMark.Should().BeFalse(
            "most fields are ordinary; marking them all \"typed by you\" would say nothing " +
            "while burying the marks that mean something");
    }

    [Fact]
    public void AValueTheFormWorkedOut_SaysSo()
    {
        var shell = ShellOver(Claim());

        var total = Field(shell, "total");
        total.Response.Should().Be("120", "the expression ran");
        total.ValueIsCalculated.Should().BeTrue();
        total.ProvenanceLabel.Should().Be("Calculated");
    }

    /// <summary>The state that most needs saying.</summary>
    [Fact]
    public void TypingOverACalculatedValue_SaysYouChangedIt()
    {
        var shell = ShellOver(Claim());
        var total = Field(shell, "total");

        total.Response = "115.00";

        total.ValueWasOverridden.Should().BeTrue();
        total.ValueIsCalculated.Should().BeFalse();
        total.ProvenanceLabel.Should().Be("You changed this",
            "somebody who corrects a total needs to know they have overridden something");
    }

    [Fact]
    public void ACorrectionSurvivesRecomputation_AndKeepsSayingSo()
    {
        var shell = ShellOver(Claim());
        Field(shell, "total").Response = "115.00";

        // Change a driver, which is what triggers a recompute of everything downstream.
        Field(shell, "subtotal").Response = "200.00";

        var total = Field(shell, "total");
        total.Response.Should().Be("115.00",
            "recomputation must not overwrite a correction (specification 8.6)");
        total.ValueWasOverridden.Should().BeTrue(
            "and the mark must still say so afterwards, or somebody who does not trust the " +
            "guarantee will keep re-checking a number that is never going to change back");
    }

    [Fact]
    public void ClearingAnOverride_LetsTheFormCalculateAgain()
    {
        var shell = ShellOver(Claim());
        Field(shell, "total").Response = "115.00";
        Field(shell, "total").Response = "";

        Field(shell, "subtotal").Response = "300.00";

        var total = Field(shell, "total");
        total.Response.Should().Be("320", "an emptied field is the form's to fill again");
        total.ValueIsCalculated.Should().BeTrue();
        total.ProvenanceLabel.Should().Be("Calculated");
    }

    [Fact]
    public void ACalculatedFieldIsStillEditable()
    {
        var shell = ShellOver(Claim());

        var total = Field(shell, "total");
        total.ValueIsCalculated.Should().BeTrue();
        total.IsInputEnabled.Should().BeTrue(
            "being computed does not make a field read-only; a total that is wrong must be " +
            "correctable by the person filling the form (specification 8.6)");
    }

    [Fact]
    public void BothStatesReachAssistiveTechnology_AndSayWhatHappensNext()
    {
        var shell = ShellOver(Claim());
        var total = Field(shell, "total");

        total.ProvenanceAnnouncement.Should().Be(
            "Calculated by the form. You can type over it if it is wrong.",
            "a visual mark says nothing to a screen reader, and the useful half is what " +
            "you are allowed to do about it");

        total.Response = "115.00";

        total.ProvenanceAnnouncement.Should().Be(
            "You changed this from the calculated value. The form will not calculate over it again.");
    }
}
