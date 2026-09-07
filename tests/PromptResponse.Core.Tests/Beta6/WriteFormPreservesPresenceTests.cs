using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>
/// A document read and written back without an edit must still be the same document.
/// </summary>
/// <remarks>
/// No conformance case can ask this. `tools/PromptResponse.ConformanceDriver` answers a
/// round-trip case through <see cref="AprBeta6Reader.WriteStream"/>, which writes the
/// parsed value and so preserves everything by construction. <see cref="AprBeta6Reader.WriteForm"/>
/// regenerates from the typed model, and that is the path the desktop and command-line
/// save paths take, so it is the one that decides whether opening a signed form and
/// saving it leaves every attestation over it still resolving.
///
/// The specification computes a digest over "the fully parsed JSON semantic model", and
/// says a form digest includes every member "that survived parsing" (APR-DIGEST-006,
/// APR-DIGEST-002). A member that was absent did not survive parsing, so a writer that
/// materializes it has written a different document.
/// </remarks>
public class WriteFormPreservesPresenceTests
{
    private readonly AprBeta6Reader _reader = new();

    private string RoundTrip(string source) =>
        _reader.WriteForm(_reader.ReadForm(source, AprRepresentation.Jsonc), AprRepresentation.Jsonc);

    private static string DigestOf(string source) =>
        AprSemanticDigest.Digest(JsonDocument.Parse(source).RootElement);

    /// <summary>The smallest valid document: every optional member absent.</summary>
    private const string Minimal =
        """
        {
          "aprVersion": "1.0-beta.6",
          "metadata": { "title": "T" },
          "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
        }
        """;

    /// <summary>The same document with every one of those members written out.</summary>
    private const string Explicit =
        """
        {
          "aprVersion": "1.0-beta.6",
          "documentType": "template",
          "metadata": { "title": "T" },
          "sections": [ { "id": "s", "title": "S", "sections": [],
            "prompts": [ { "id": "p", "label": "P", "response": "",
              "hints": { "suggestedValues": [] } } ] } ]
        }
        """;

    [Fact]
    public void AbsentMembersStayAbsent()
    {
        DigestOf(RoundTrip(Minimal)).Should().Be(DigestOf(Minimal),
            "a document nobody edited must keep the digest every attestation over it names");
    }

    [Fact]
    public void DeclaredMembersStayDeclared()
    {
        // The other direction, and the reason presence is tracked rather than emptiness
        // normalized: dropping a member the source carried moves the digest just as far.
        DigestOf(RoundTrip(Explicit)).Should().Be(DigestOf(Explicit));
    }

    [Fact]
    public void TheTwoAreDifferentDocuments()
    {
        // If these ever digest the same, the two tests above stop asking anything.
        DigestOf(Minimal).Should().NotBe(DigestOf(Explicit));
    }

    [Fact]
    public void AMemberSetInMemoryIsWritten()
    {
        // Presence follows assignment, so a document built rather than read still says
        // what it was given — this is how a filled form gets its documentType.
        var document = _reader.ReadForm(Minimal, AprRepresentation.Jsonc);
        document.DocumentType = DocumentType.FilledForm;

        _reader.WriteForm(document, AprRepresentation.Jsonc)
            .Should().Contain("\"documentType\": \"filledForm\"");
    }

    [Fact]
    public void HintMembersAreAllAccountedFor()
    {
        // PromptHints.IsEmpty names every member by hand, and the writer asks it whether
        // a prompt's `hints` is worth writing. A member added without being named there
        // would make a populated hints object look empty, and it would be dropped from a
        // document that carried it — in silence, which is the part that matters.
        var named = typeof(PromptHints).GetProperties()
            .Where(property => property.CanWrite
                && property.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length == 0)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var accounted = new[]
        {
            nameof(PromptHints.Extensions), nameof(PromptHints.Placeholder),
            nameof(PromptHints.ExpectedDataType), nameof(PromptHints.SuggestedValues),
            nameof(PromptHints.HelpText), nameof(PromptHints.ValidationPattern),
            nameof(PromptHints.Min), nameof(PromptHints.Max), nameof(PromptHints.Step),
            nameof(PromptHints.ExprHidden), nameof(PromptHints.ExprValue),
            nameof(PromptHints.ExprExpected), nameof(PromptHints.ExprValidation),
            nameof(PromptHints.ExprReadOnly),
        }.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        named.Should().Equal(accounted,
            "PromptHints.IsEmpty must name every hint member, or one will be dropped");
    }

    [Fact]
    public void HintsMutatedInPlaceAreStillWritten()
    {
        // The declared flag follows assignment, and the desktop's editing path mutates
        // the object it already has. Emptiness is what catches that.
        var document = _reader.ReadForm(Minimal, AprRepresentation.Jsonc);
        document.Sections[0].Prompts[0].Hints.HelpText = "As it appears on your passport";

        _reader.WriteForm(document, AprRepresentation.Jsonc)
            .Should().Contain("As it appears on your passport");
    }

    [Fact]
    public void PromptsAddedToASectionAreStillWritten()
    {
        var document = _reader.ReadForm(Minimal, AprRepresentation.Jsonc);
        document.Sections[0].Sections.Add(new Section { Id = "child", Title = "Child" });

        _reader.WriteForm(document, AprRepresentation.Jsonc).Should().Contain("\"child\"");
    }

    public static TheoryData<string> ShippedForms()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(
                     Path.Combine(Root(), "tests", "Conformance", "beta6", "forms")))
        {
            data.Add(path);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedForms))]
    public void AShippedFormComesBackAsItself(string fixture)
    {
        // The general form of the two above, over documents nobody wrote for this test.
        // It is the property that matters — a writer adds nothing and drops nothing — and
        // it is what caught `displayName`: a computed property on RoleDefinition that
        // serialized, so every role in every document this library wrote gained an
        // unprefixed member the specification does not define (APR-MODEL-031), and the
        // form's digest moved with it.
        var representation = fixture.EndsWith(".yaml", StringComparison.Ordinal)
            ? AprRepresentation.Yaml : AprRepresentation.Jsonc;
        var source = File.ReadAllText(fixture);

        var written = _reader.WriteForm(_reader.ReadForm(source, representation), representation);

        Digest(written, representation).Should().Be(Digest(source, representation),
            "reading and writing {0} must leave the document it names", Path.GetFileName(fixture));
    }

    private static string Digest(string source, AprRepresentation representation) =>
        AprSemanticDigest.Digest(new AprBeta6Reader()
            .ReadStream(source, representation).OfType<AprFormRecord>().First().Value);

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}
