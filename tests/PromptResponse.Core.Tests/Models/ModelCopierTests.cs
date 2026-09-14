using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Tests for <see cref="ModelCopier"/>, the one place model-copy policy lives.
/// </summary>
/// <remarks>
/// What is being defended is specification 5.8: an unrecognised member present on
/// read must still be present, unchanged, on write (APR-MODEL-021). A copy that
/// drops one makes any editor that copies a model — undo/redo above all — quietly
/// destructive, and the document that loses the member is the one belonging to
/// whoever used a newer producer.
/// </remarks>
public class ModelCopierTests
{
    private static readonly AprBeta6Reader Reader = new();

    [Fact]
    public void Copy_Prompt_KeepsExtensionMembers()
    {
        var prompt = new Prompt { Id = "p", Label = "P", Extensions = Members(("com.example.priority", "2")) };

        var copy = ModelCopier.Copy(prompt);

        copy.Extensions.Should().ContainKey("com.example.priority")
            .WhoseValue.GetRawText().Should().Be("2");
    }

    [Fact]
    public void Copy_Prompt_KeepsHintExtensionMembersAndBounds()
    {
        var prompt = new Prompt
        {
            Id = "p",
            Label = "P",
            Role = "nurse",
            Hints = new PromptHints
            {
                Min = 1,
                Max = 10,
                Step = 0.5,
                Extensions = Members(("com.example.widget", "\"dial\"")),
            },
        };

        var copy = ModelCopier.Copy(prompt);

        copy.Role.Should().Be("nurse");
        copy.Hints.Min.Should().Be(1);
        copy.Hints.Max.Should().Be(10);
        copy.Hints.Step.Should().Be(0.5);
        copy.Hints.Extensions.Should().ContainKey("com.example.widget");
    }

    [Fact]
    public void Copy_Section_KeepsExtensionMembersThroughTheTree()
    {
        var section = new Section
        {
            Id = "t",
            Title = "T",
            Kind = "table",
            Role = "office",
            Extensions = Members(("com.example.layout", "\"grid\"")),
            Sections =
            [
                new Section
                {
                    Id = "t.row1",
                    Title = "Row 1",
                    Prompts = [new Prompt { Id = "t.row1.c1", Label = "C1", Extensions = Members(("com.example.priority", "2")) }],
                },
            ],
        };

        var copy = ModelCopier.Copy(section);

        copy.Role.Should().Be("office");
        copy.Extensions.Should().ContainKey("com.example.layout");
        copy.Sections[0].Prompts[0].Extensions.Should().ContainKey("com.example.priority");
    }

    [Fact]
    public void Copy_Prompt_GivesTheCopyItsOwnExtensionBag()
    {
        var prompt = new Prompt { Id = "p", Label = "P", Extensions = Members(("com.example.priority", "2")) };

        var copy = ModelCopier.Copy(prompt);
        copy.Extensions!.Remove("com.example.priority");

        prompt.Extensions.Should().ContainKey("com.example.priority",
            "a copy holds its own members, or editing one object edits another");
    }

    [Fact]
    public void Copy_Prompt_KeepsExtensionMemberNamesCaseSensitive()
    {
        // Specification 5.8: member names are case-sensitive, so two spellings are
        // two members and a copy that folded case would merge them.
        var prompt = new Prompt
        {
            Id = "p",
            Label = "P",
            Extensions = Members(("com.example.Priority", "1"), ("com.example.priority", "2")),
        };

        ModelCopier.Copy(prompt).Extensions.Should().HaveCount(2);
    }

    [Fact]
    public void Copy_Prompt_DoesNotInventADeclaredResponse()
    {
        var document = Reader.ReadForm(
            """
            {
              "aprVersion": "1.0-beta.6",
              "metadata": { "title": "T" },
              "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
            }
            """,
            AprRepresentation.Jsonc);
        var prompt = document.Sections[0].Prompts[0];

        var copy = ModelCopier.Copy(prompt);

        copy.ResponseIsDeclared.Should().BeFalse(
            "the source said nothing about `response`, and a copy that claims it did is a different document");
        document.Sections[0].Prompts[0] = copy;
        Reader.WriteForm(document, AprRepresentation.Jsonc).Should().NotContain("\"response\"");
    }

    [Fact]
    public void Copy_Prompt_KeepsADeclaredEmptyResponse()
    {
        var prompt = new Prompt { Id = "p", Label = "P", Response = "" };

        ModelCopier.Copy(prompt).ResponseIsDeclared.Should().BeTrue();
    }

    [Fact]
    public void Copy_Section_KeepsWhetherCollectionsWereDeclared()
    {
        var declared = new Section { Id = "s", Title = "S", Prompts = [], Sections = [] };
        var silent = new Section { Id = "s", Title = "S" };

        var declaredCopy = ModelCopier.Copy(declared);
        var silentCopy = ModelCopier.Copy(silent);

        declaredCopy.PromptsAreDeclared.Should().BeTrue();
        declaredCopy.SectionsAreDeclared.Should().BeTrue();
        silentCopy.PromptsAreDeclared.Should().BeFalse();
        silentCopy.SectionsAreDeclared.Should().BeFalse();
    }

    [Fact]
    public void Copy_Section_GivesTheCopyItsOwnChildren()
    {
        var section = new Section
        {
            Id = "s",
            Title = "S",
            Prompts = [new Prompt { Id = "p", Label = "P" }],
            Sections = [new Section { Id = "s.child", Title = "Child" }],
        };

        var copy = ModelCopier.Copy(section);
        copy.Prompts[0].Label = "Edited";
        copy.Sections[0].Title = "Edited";

        section.Prompts[0].Label.Should().Be("P");
        section.Sections[0].Title.Should().Be("Child");
    }

    [Fact]
    public void ModelCopierCopiesEveryMember()
    {
        // A member added to the models and forgotten here is dropped from every copy
        // in silence — which is the failure this test exists to make loud. Naming the
        // members by hand is what makes adding one a decision rather than an omission.
        Written(typeof(Prompt)).Should().Equal(
            Sorted(
                nameof(Prompt.Extensions), nameof(Prompt.Id), nameof(Prompt.Label),
                nameof(Prompt.Response), nameof(Prompt.Hints), nameof(Prompt.Role)),
            "ModelCopier.Copy(Prompt) must name every prompt member");

        Written(typeof(PromptHints)).Should().Equal(
            Sorted(
                nameof(PromptHints.Extensions), nameof(PromptHints.Placeholder),
                nameof(PromptHints.ExpectedDataType), nameof(PromptHints.SuggestedValues),
                nameof(PromptHints.HelpText), nameof(PromptHints.ValidationPattern),
                nameof(PromptHints.Min), nameof(PromptHints.Max), nameof(PromptHints.Step),
                nameof(PromptHints.ExprHidden), nameof(PromptHints.ExprValue),
                nameof(PromptHints.ExprExpected), nameof(PromptHints.ExprValidation),
                nameof(PromptHints.ExprReadOnly)),
            "ModelCopier.Copy(PromptHints) must name every hint member");

        Written(typeof(Section)).Should().Equal(
            Sorted(
                nameof(Section.Extensions), nameof(Section.Id), nameof(Section.Title),
                nameof(Section.Description), nameof(Section.Sections), nameof(Section.Prompts),
                nameof(Section.Kind), nameof(Section.Role), nameof(Section.CanAddRows),
                nameof(Section.MaxRows)),
            "ModelCopier.Copy(Section) must name every section member");
    }

    [Fact]
    public void ModelCopierCopiesEveryPieceOfReaderState()
    {
        // Reader state is [JsonIgnore] and settable only inside the assembly: presence
        // flags and the computed marker. A copy that loses one writes a document the
        // source did not say, so these are named here too.
        ReaderState(typeof(Prompt)).Should().Equal(
            Sorted(
                nameof(Prompt.ResponseIsDeclared), nameof(Prompt.ComputedInThisSession),
                nameof(Prompt.HintsAreDeclared)));
        ReaderState(typeof(PromptHints)).Should().Equal(
            Sorted(nameof(PromptHints.SuggestedValuesAreDeclared)));
        ReaderState(typeof(Section)).Should().Equal(
            Sorted(nameof(Section.SectionsAreDeclared), nameof(Section.PromptsAreDeclared)));
    }

    private static string[] Written(Type model) => model.GetProperties()
        .Where(property => property.CanWrite
            && property.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length == 0)
        .Select(property => property.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static string[] ReaderState(Type model) => model.GetProperties()
        .Where(property => property.CanWrite
            && property.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length != 0)
        .Select(property => property.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static string[] Sorted(params string[] names)
        => names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    private static Dictionary<string, JsonElement> Members(params (string Name, string Json)[] members)
        => members.ToDictionary(
            member => member.Name,
            member => JsonDocument.Parse(member.Json).RootElement.Clone(),
            StringComparer.Ordinal);
}
