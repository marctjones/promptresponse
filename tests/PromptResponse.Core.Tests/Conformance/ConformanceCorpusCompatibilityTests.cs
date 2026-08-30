using AwesomeAssertions;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>Forward compatibility, registry, expression, and rendering contract checks.</summary>
public sealed class ConformanceCorpusCompatibilityTests : ConformanceCorpusTestBase
{
    [Fact]
    public void TypeHints_NeverRewriteAResponse()
    {
        var path = Path.Combine(CorpusDir("valid"), "hidden-characters-preserved.aprf");
        var responses = ResponsesById(Serializer.Deserialize(Serializer.Serialize(Serializer.Deserialize(File.ReadAllText(path)))));
        const string zeroWidthSpace = "\u200b";
        responses["url_hint"].Should().Contain(zeroWidthSpace);
        responses["email_hint"].Should().Contain(zeroWidthSpace);
        responses["text_hint"].Should().Contain(zeroWidthSpace);
        responses["persian_zwnj"].Should().Contain("\u200c", "legitimate ZWNJ must survive too");
    }

    [Fact]
    public void TypeRegistry_AgreesWithTheImplementation()
    {
        var registryPath = Path.GetFullPath(Path.Combine(CorpusDir("valid"), "..", "..", "..", "..", "schemas", "apr-types-1.0.json"));
        using var registry = System.Text.Json.JsonDocument.Parse(File.ReadAllText(registryPath));
        var mismatches = new List<string>();
        foreach (var type in registry.RootElement.GetProperty("expectedDataType").GetProperty("types").EnumerateArray())
        {
            var id = type.GetProperty("id").GetString()!;
            var claimed = type.GetProperty("celType").GetString()!;
            var document = new Core.Models.AprDocument
            {
                Metadata = new Core.Models.Metadata { Title = "T" },
                Sections = [new Core.Models.Section { Id = "s", Title = "S", Prompts =
                    [new Core.Models.Prompt { Id = "field", Label = "Field", Hints = new Core.Models.PromptHints { ExpectedDataType = id } }] }],
            };
            var actual = Core.Expressions.FormExpressions.BuildContext(document).DeclaredTypeOf("field");
            var normalised = actual.ToLowerInvariant().Split('.').Last();
            var expected = claimed.ToLowerInvariant().Split('<').First();
            if (!normalised.Contains(expected) && !expected.Contains(normalised))
                mismatches.Add($"{id}: registry says {claimed}, implementation declares {actual}");
        }
        mismatches.Should().BeEmpty("the type registry is the published vocabulary and must match the code");
    }

    [Fact]
    public void ExpressionBinding_MatchesThePublishedVectors()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(CorpusDir("expressions"), "vectors.json")));
        var failures = new List<string>();
        foreach (var testCase in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = testCase.GetProperty("name").GetString()!;
            var expression = testCase.GetProperty("expr").GetString()!;
            var expected = testCase.GetProperty("expect").ValueKind == System.Text.Json.JsonValueKind.Null ? null : testCase.GetProperty("expect").GetString();
            var section = new Core.Models.Section { Id = "s", Title = "S" };
            foreach (var field in testCase.GetProperty("fields").EnumerateArray())
                section.Prompts.Add(new Core.Models.Prompt { Id = field.GetProperty("id").GetString()!, Label = field.GetProperty("id").GetString()!, Response = field.GetProperty("response").GetString() ?? string.Empty, Hints = new Core.Models.PromptHints { ExpectedDataType = field.GetProperty("type").GetString() } });
            var subject = new Core.Models.Prompt { Id = "_subject", Label = "Subject", Hints = new Core.Models.PromptHints { ExprValue = expression } };
            section.Prompts.Add(subject);
            var actual = Core.Expressions.FormExpressions.ComputeValue(subject, Core.Expressions.FormExpressions.BuildContext(new Core.Models.AprDocument { Metadata = new Core.Models.Metadata { Title = "Vectors" }, Sections = [section] }));
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                failures.Add($"{name}: `{expression}` expected {expected ?? "<degrade>"} but got {actual ?? "<degrade>"}");
        }
        failures.Should().BeEmpty("every published binding vector must reproduce exactly");
    }

    [Theory]
    [InlineData("unknown-fields.aprt")]
    [InlineData("newer-minor-accepted.aprt")]
    public void UnknownMembers_SurviveARoundTrip(string fixture)
    {
        var originalJson = File.ReadAllText(Path.Combine(CorpusDir("valid"), fixture));
        using var original = System.Text.Json.JsonDocument.Parse(originalJson);
        using var roundTripped = System.Text.Json.JsonDocument.Parse(Serializer.Serialize(Serializer.Deserialize(originalJson)));
        var originalUnknown = UnknownMemberNames(original.RootElement).ToList();
        originalUnknown.Should().NotBeEmpty($"{fixture} is meant to carry unknown members");
        UnknownMemberNames(roundTripped.RootElement).Should().BeEquivalentTo(originalUnknown,
            $"{fixture} must preserve every unrecognised member across a round-trip");
    }

    [Fact]
    public void NewerMinorVersion_ValidatesWithAnAdvisoryWarning()
    {
        var result = Validator.Validate(Serializer.Deserialize(File.ReadAllText(Path.Combine(CorpusDir("valid"), "newer-minor-accepted.aprt"))));
        result.IsValid.Should().BeTrue("a newer minor version is readable, not invalid");
        result.Warnings.Should().Contain(w => w.WarningCode == "NEWER_MINOR_VERSION");
    }

    [Fact]
    public void DocumentType_IsAuthoritative_OverTheFileExtension()
    {
        var path = Path.Combine(CorpusDir("valid"), "documenttype-beats-extension.aprt");
        var document = Serializer.Deserialize(File.ReadAllText(path));
        Path.GetExtension(path).Should().Be(".aprt", "the fixture is deliberately misnamed");
        document.DocumentType.Should().Be(Core.Models.DocumentType.FilledForm, "documentType in the file decides, never the filename");
        Validator.Validate(document).IsValid.Should().BeTrue("a filled form named .aprt is unusual, not invalid");
    }

    [Fact]
    public void SectionOrdering_OwnPromptsRenderBeforeChildSections()
    {
        var document = Serializer.Deserialize(File.ReadAllText(Path.Combine(CorpusDir("valid"), "section-ordering.aprt")));
        var fieldIds = new DocumentRenderModelBuilder().Build(document, RenderOptions.Default).Blocks.OfType<FieldBlock>().Select(b => b.Id).ToList();
        fieldIds.Should().Equal(new[] { "own_first", "own_second", "child_first", "child_second" },
            "a section's own prompts must precede its child sections, in array order");
    }

    private static SortedDictionary<string, string> ResponsesById(Core.Models.AprDocument document)
    {
        var responses = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Walk(Core.Models.Section section) { foreach (var prompt in section.Prompts) responses[prompt.Id] = prompt.Response; foreach (var child in section.Sections) Walk(child); }
        foreach (var section in document.Sections) Walk(section);
        return responses;
    }

    private static IEnumerable<string> UnknownMemberNames(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) { if (property.Name.StartsWith("x-", StringComparison.Ordinal)) yield return property.Name; foreach (var nested in UnknownMemberNames(property.Value)) yield return nested; }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) foreach (var nested in UnknownMemberNames(item)) yield return nested;
    }
}
