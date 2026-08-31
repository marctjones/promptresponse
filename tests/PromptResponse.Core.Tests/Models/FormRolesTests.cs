using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Which part of a multi-party form belongs to whom.
/// </summary>
/// <remarks>
/// A role answers "is this one mine?" so the person filling the form does not have to
/// ask. It never answers "may I type here?" - the format has no identity at fill time,
/// and pretending otherwise would turn an advisory hint into a lock the format forbids.
/// </remarks>
public class FormRolesTests
{
    private static string CorpusFixture => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "beta6", "forms", "roles.apr.jsonc");

    private static AprDocument Intake() =>
        new AprJsonSerializer().Deserialize(File.ReadAllText(CorpusFixture));

    [Fact]
    public void APromptInheritsItsSectionsRole()
    {
        var roles = FormRoles.Resolve(Intake()).ToDictionary(r => r.Prompt.Id, r => r.Role);

        roles["name"].Should().Be("patient");
        roles["bp"].Should().Be("nurse");
        roles["ref"].Should().Be("office");
    }

    [Fact]
    public void APromptsOwnRoleOverridesItsSections()
    {
        var roles = FormRoles.Resolve(Intake()).ToDictionary(r => r.Prompt.Id, r => r.Role);

        roles["symptoms"].Should().Be("patient",
            "a single question can be handed back to the patient without splitting the " +
            "nurse's section in two");
    }

    [Fact]
    public void AFieldWithNoRoleAnywhere_BelongsToAnyone()
    {
        FormRoles.Resolve(Intake()).Single(r => r.Prompt.Id == "agree").Role
            .Should().BeNull("an unassigned field is for whoever is filling the form");
    }

    [Fact]
    public void TheInnermostRoleWins()
    {
        var outer = new Section { Id = "o", Title = "Outer", Role = "patient" };
        var inner = new Section { Id = "i", Title = "Inner", Role = "nurse" };
        var prompt = new Prompt { Id = "p", Label = "P" };
        inner.Prompts.Add(prompt);
        outer.Sections.Add(inner);

        FormRoles.Effective(prompt, [outer, inner]).Should().Be("nurse");
    }

    [Fact]
    public void RolesTheFormUses_AreListedInDocumentOrder()
    {
        // A reader offering "which role are you?" needs the list the document uses.
        FormRoles.Used(Intake()).Should().Equal("patient", "nurse", "office");
    }

    /// <summary>A role never makes a document invalid, whatever it says.</summary>
    /// <remarks>
    /// The vocabulary is open (specification 4.10) because roles are domain-specific.
    /// A reader meeting "notary" or "prüfer" shows the field normally rather than erroring,
    /// which is what lets one form work across an industry nobody enumerated in advance.
    /// </remarks>
    [Theory]
    [InlineData("patient")]
    [InlineData("notary")]
    [InlineData("prüfer")]
    [InlineData("whoever-happens-to-be-nearest")]
    public void AnUnrecognisedRole_IsStillValid(string role)
    {
        var document = Intake();
        document.Sections[0].Role = role;

        new DocumentValidator().Validate(document).IsValid.Should().BeTrue(
            "the role vocabulary is open; an unknown one degrades to no special treatment");
    }

    [Fact]
    public void Roles_SurviveARoundTrip()
    {
        var serializer = new AprJsonSerializer();
        var reloaded = serializer.Deserialize(serializer.Serialize(Intake()));

        FormRoles.Resolve(reloaded).Single(r => r.Prompt.Id == "symptoms").Role
            .Should().Be("patient");
        reloaded.Sections.Single(s => s.Id == "office").Role.Should().Be("office");
    }

    // ── Declaring roles, so the identifiers have names worth showing ──

    [Fact]
    public void ADeclaredRole_HasANameToShowAPerson()
    {
        var document = Intake();

        FormRoles.DisplayName(document, "nurse").Should().Be("Nurse");
        FormRoles.Definition(document, "nurse")!.Description
            .Should().Be("Clinical staff recording observations.");
    }

    [Fact]
    public void ADeclaredRoleWithNoName_FallsBackToItsIdentifier()
    {
        var document = Intake();
        document.Roles!.Single(r => r.Id == "office").Name = null;

        FormRoles.DisplayName(document, "office").Should().Be("office",
            "a reader must always have something to show, even for a half-filled declaration");
    }

    /// <summary>Referencing a role nobody declared is valid, and worth a nudge.</summary>
    /// <remarks>
    /// Declaring must stay optional or the vocabulary is not open, and an industry with a
    /// party nobody enumerated in advance would be locked out. But an author who assigns a
    /// section to "notary" and forgets to declare it has produced a form that shows a bare
    /// slug where a name belongs, which is worth telling them at authoring time.
    /// </remarks>
    [Fact]
    public void AnUndeclaredRole_IsValidAndReported()
    {
        var document = Intake();
        document.Sections[0].Role = "notary";

        new DocumentValidator().Validate(document).IsValid.Should().BeTrue(
            "declaring is optional; the vocabulary is open (specification 4.10)");
        FormRoles.Undeclared(document).Should().Contain("notary");
        FormRoles.DisplayName(document, "notary").Should().Be("notary",
            "with no declaration the identifier is what a reader shows");
    }

    [Fact]
    public void AFormThatDeclaresNoRoles_WritesNoRolesMember()
    {
        var serializer = new AprJsonSerializer();
        var document = Intake();
        document.Roles = null;

        serializer.Serialize(document).Should().NotContain("\"roles\"",
            "a single-party form should not carry an empty declaration it never uses");
    }

    [Fact]
    public void RoleDeclarations_SurviveARoundTrip()
    {
        var serializer = new AprJsonSerializer();
        var reloaded = serializer.Deserialize(serializer.Serialize(Intake()));

        reloaded.Roles.Should().HaveCount(3);
        FormRoles.DisplayName(reloaded, "patient").Should().Be("Patient");
    }
}
