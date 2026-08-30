using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// "Is this one mine?" answered by the form instead of by asking somebody.
/// </summary>
/// <remarks>
/// The point of roles in a reader: a patient handed a three-party intake should not have
/// to work out which questions the nurse fills. Picking a role marks theirs. It never
/// hides or disables anything - the app cannot know who is really at the keyboard, and a
/// nurse covering reception must not be blocked by a dropdown (specification 4.10).
/// </remarks>
public class RoleAwarenessTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static string CorpusFixture => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "v1", "valid", "roles.aprt");

    private static MainShellViewModel ShellOver(AprDocument document)
    {
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var shell = new MainShellViewModel(
            Substitute.For<IFileService>(), Substitute.For<IDialogService>(),
            session, profile, new PromptViewModelFactory(profile));

        // Set after construction: the shell builds its view models when the session
        // raises DocumentChanged, so a document set beforehand is never seen.
        session.Set(document, null);
        return shell;
    }

    private static MainShellViewModel Intake() =>
        ShellOver(new AprJsonSerializer().Deserialize(File.ReadAllText(CorpusFixture)));

    private static PromptViewModelBase Field(MainShellViewModel shell, string id) =>
        shell.PromptViewModels.Single(p => p.Id == id);

    [Fact]
    public void AMultiPartyForm_OffersTheRolesItUses_PlusEveryone()
    {
        var shell = Intake();

        shell.HasRoles.Should().BeTrue();
        shell.AvailableRoles.Select(r => r.Name)
            .Should().Equal("Everyone", "Patient", "Nurse", "Office use");
    }

    [Fact]
    public void TheRolePicker_ShowsTheAuthorsDescriptions()
    {
        Intake().AvailableRoles.Single(r => r.Id == "nurse").Display
            .Should().Be("Nurse — Clinical staff recording observations.",
                "declaring roles exists so the picker offers something better than a slug");
    }

    [Fact]
    public void ItOpensShowingEverything_UntilSomeoneSaysWhoTheyAre()
    {
        var shell = Intake();

        shell.ActiveRoleChoice!.Id.Should().BeNull(
            "before anyone chooses, the form looks exactly as it did before roles existed");
        shell.PromptViewModels.Should().OnlyContain(p => !p.ShowsMineAccent,
            "accenting every field says nothing");
    }

    [Fact]
    public void ChoosingARole_MarksTheirFieldsAndNobodyElses()
    {
        var shell = Intake();

        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "nurse");

        Field(shell, "bp").IsMine.Should().BeTrue();
        Field(shell, "bp").ShowsMineAccent.Should().BeTrue();

        Field(shell, "name").IsSomeoneElses.Should().BeTrue("that is the patient's question");
        Field(shell, "name").RoleBadge.Should().Be("For Patient");
    }

    [Fact]
    public void APromptOverridingItsSection_FollowsItsOwnRole()
    {
        var shell = Intake();
        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "nurse");

        var symptoms = Field(shell, "symptoms");

        symptoms.IsSomeoneElses.Should().BeTrue(
            "it sits in the nurse's section but is the patient's question to answer");
        symptoms.RoleBadge.Should().Be("For Patient");
    }

    [Fact]
    public void AFieldWithNoRole_IsAlwaysYours()
    {
        var shell = Intake();
        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "office");

        Field(shell, "agree").IsMine.Should().BeTrue(
            "an unassigned field belongs to whoever is filling");
    }

    /// <summary>Marked, never locked. The load-bearing assertion.</summary>
    /// <remarks>
    /// A disabled box is evidence of nothing - whoever holds the file can edit the JSON
    /// directly - so disabling would buy no safety and cost the nurse covering reception
    /// their afternoon. Accountability comes from signatures (specification 9.3).
    /// </remarks>
    [Fact]
    public void SomeoneElsesFields_StayAnswerable()
    {
        var shell = Intake();
        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "patient");

        var officeField = Field(shell, "ref");

        officeField.IsSomeoneElses.Should().BeTrue();
        officeField.IsInputEnabled.Should().BeTrue(
            "a role marks a field, it does not lock it (specification 4.10)");
        officeField.IsVisible.Should().BeTrue("nothing is hidden either");

        officeField.Response = "REF-1234";
        officeField.Response.Should().Be("REF-1234", "and the answer is accepted");
    }

    /// <summary>A visual accent communicates nothing to a screen reader.</summary>
    [Fact]
    public void WhoseFieldItIs_ReachesAssistiveTechnology()
    {
        var shell = Intake();
        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "patient");

        Field(shell, "ref").RoleAnnouncement.Should().Be(
            "For Office use. You can still answer it if you need to.",
            "the accessible description must carry both facts the accent carries: whose " +
            "field it is, and that it is still answerable");
    }

    [Fact]
    public void SwitchingRoles_UpdatesEveryField()
    {
        var shell = Intake();

        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "patient");
        Field(shell, "name").IsMine.Should().BeTrue();

        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "office");
        Field(shell, "name").IsMine.Should().BeFalse();
        Field(shell, "ref").IsMine.Should().BeTrue();
    }

    [Fact]
    public void ChoosingARole_RefreshesTheShellRoleSummaryAndDescription()
    {
        var shell = Intake();

        shell.ActiveRoleChoice = shell.AvailableRoles.Single(r => r.Id == "nurse");

        shell.ActiveRoleDescription.Should().Be("Clinical staff recording observations.");
        shell.ActiveRoleSummary.Should().Be(
            "3 of 8 fields are for Nurse. The rest are marked, and still answerable.");
    }

    [Fact]
    public void ASinglePartyForm_ShowsNoPicker()
    {
        var shell = ShellOver(new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Simple" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "S",
                    Prompts = [new Prompt { Id = "p", Label = "Name" }],
                },
            ],
        });

        shell.HasRoles.Should().BeFalse(
            "most forms have one party and should not carry a control they never need");
    }
}
