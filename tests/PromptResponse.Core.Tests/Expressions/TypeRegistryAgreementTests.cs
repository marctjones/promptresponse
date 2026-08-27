using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Expressions;

/// <summary>
/// The published type registry and the expression type environment must agree.
/// </summary>
/// <remarks>
/// <para>
/// schemas/apr-types-1.0.json declares a celType for every registered expectedDataType,
/// and the expression binding maps the same names independently. Nothing connected the
/// two, so registering a type meant three edits in three files with no guarantee they
/// matched - and a disagreement stays invisible until an expression over that type
/// evaluates against the wrong environment.
/// </para>
/// <para>
/// Asserted through Check, the authoring-time diagnostic, rather than by reaching into
/// the binding: an author's experience of the type is what the registry is promising, so
/// that is what gets pinned. The registry is the artefact a third-party implementation
/// reads, so it is the authority and the code follows it.
/// </para>
/// </remarks>
public class TypeRegistryAgreementTests
{
    private static string RegistryPath => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "schemas", "apr-types-1.0.json");

    /// <summary>An expression that type-checks only against the celType the registry declares.</summary>
    private static string ProbeFor(string celType) => celType switch
    {
        "string" => "_this.startsWith(\"a\")",
        "double" => "_this + 1.0 > 0.0",
        "bool" => "_this && true",
        "timestamp" => "_this.getFullYear() > 2000",
        "list<string>" => "_this.exists(x, x == \"a\")",
        _ => throw new Xunit.Sdk.XunitException(
            $"the registry declares celType '{celType}', which this test has no probe for. " +
            "Either a new CEL type was registered or there is a typo in the registry."),
    };

    public static IEnumerable<object[]> RegisteredTypes()
    {
        using var registry = JsonDocument.Parse(File.ReadAllText(RegistryPath));
        foreach (var entry in registry.RootElement
                     .GetProperty("expectedDataType").GetProperty("types").EnumerateArray())
        {
            yield return [entry.GetProperty("id").GetString()!, entry.GetProperty("celType").GetString()!];
        }
    }

    private static FormExpressionContext ContextWith(string expectedDataType)
    {
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Probe" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "Probe",
                    Prompts =
                    [
                        new Prompt
                        {
                            Id = "field", Label = "Field",
                            Hints = new PromptHints { ExpectedDataType = expectedDataType },
                        },
                    ],
                },
            ],
        };
        return FormExpressionContext.Create(document);
    }

    [Theory]
    [MemberData(nameof(RegisteredTypes))]
    public void EveryRegisteredType_TypeChecksAsTheRegistryDeclares(string id, string celType)
    {
        var diagnostics = ContextWith(id).Check(ProbeFor(celType), id);

        diagnostics.Should().BeEmpty(
            $"schemas/apr-types-1.0.json declares '{id}' as CEL {celType}, so " +
            $"`{ProbeFor(celType)}` must type-check against it. The registry is what a " +
            "third-party implementation reads, so the code follows the registry, not the " +
            $"reverse. Diagnostics: {string.Join(" | ", diagnostics)}");
    }

    /// <summary>A type the registry does not list must still work, as plain text.</summary>
    /// <remarks>
    /// Specification 4.7: an unrecognised expectedDataType degrades to a text field and
    /// MUST NOT error. That is what lets the registry grow without breaking readers older
    /// than the addition - and it is why zipcode and ssn could be retired from the example
    /// files without any reader needing to change.
    /// </remarks>
    [Theory]
    [InlineData("zipcode")]
    [InlineData("ssn")]
    [InlineData("something-invented-next-year")]
    public void UnregisteredType_DegradesToText(string id)
    {
        ContextWith(id).Check("_this.startsWith(\"a\")", id).Should().BeEmpty(
            "an unrecognised expectedDataType degrades to text (specification 4.7)");
    }
}
