using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.Conformance;

/// <summary>
/// Runs the shared conformance corpus through the desktop application's own view-model
/// layer, rather than only through Core.
/// </summary>
/// <remarks>
/// <para>
/// Core passing the corpus says the serializer and validator are correct. It says
/// nothing about the layer a person actually touches: the shell builds a view-model tree
/// over the document, prompt view-models write back into it, capability profiles format
/// values for display, and expressions recompute. Any of those could corrupt a document
/// that Core handles perfectly.
/// </para>
/// <para>
/// This is where the desktop's conformance stops being inherited and starts being
/// tested.
/// </para>
/// </remarks>
public class DesktopCorpusConformanceTests
{
    private static readonly AprJsonSerializer Serializer = new();

    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static string CorpusDir(string kind) => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "v1", kind);

    public static IEnumerable<object[]> ValidCorpusFiles() =>
        Directory.GetFiles(CorpusDir("valid"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    private static (MainShellViewModel Shell, AprDocument Document) Open(string path)
    {
        var document = Serializer.Deserialize(File.ReadAllText(path));
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var shell = new MainShellViewModel(
            Substitute.For<IFileService>(), Substitute.For<IDialogService>(),
            session, profile, new PromptViewModelFactory(profile));

        session.Set(document, path);   // after the shell subscribes to DocumentChanged
        return (shell, document);
    }

    /// <summary>
    /// Opening a document in the desktop shell must not change it.
    /// </summary>
    /// <remarks>
    /// The shell recomputes expressions, applies a capability profile, and materializes a
    /// view-model per prompt on load. None of that is allowed to alter a stored answer:
    /// a person who opens a filled form and closes it must still have their form.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ValidCorpusFiles))]
    public void OpeningACorpusDocument_ChangesNoResponse(string path)
    {
        var before = ResponsesById(Serializer.Deserialize(File.ReadAllText(path)));

        var (_, document) = Open(path);

        ResponsesById(document).Should().Equal(before,
            $"{Path.GetFileName(path)} must survive being opened in the desktop app unchanged");
    }

    /// <summary>
    /// A full round-trip through the view-model layer must preserve the document,
    /// including members the application does not understand.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValidCorpusFiles))]
    public void RoundTrippingThroughTheShell_PreservesTheDocument(string path)
    {
        var originalJson = File.ReadAllText(path);
        var (_, document) = Open(path);

        var written = Serializer.Serialize(document);
        var reloaded = Serializer.Deserialize(written);

        reloaded.DocumentType.Should().Be(Serializer.Deserialize(originalJson).DocumentType,
            "documentType is authoritative and the app must not reinterpret it");
        reloaded.Version.Should().Be(Serializer.Deserialize(originalJson).Version);

        using var before = System.Text.Json.JsonDocument.Parse(originalJson);
        using var after = System.Text.Json.JsonDocument.Parse(written);
        UnknownMemberNames(after.RootElement).Should().BeEquivalentTo(
            UnknownMemberNames(before.RootElement),
            "a member the desktop app does not understand must still be there after a save");
    }

    /// <summary>
    /// Every prompt in the corpus gets a view-model, and typing any string into it is
    /// accepted and stored verbatim.
    /// </summary>
    /// <remarks>
    /// This is the format's central rule, checked at the layer that could break it: a
    /// capability profile formats values for display, and an input mask could rewrite
    /// what someone typed. Neither may reach the stored answer.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ValidCorpusFiles))]
    public void EveryPromptAcceptsAnyString_WhateverItsTypeHint(string path)
    {
        var (shell, document) = Open(path);

        // Guard against a vacuous pass: if the shell materialised no prompt view-models,
        // the loop below would assert nothing at all.
        shell.PromptViewModels.Should().NotBeEmpty(
            $"{Path.GetFileName(path)} has prompts, so the shell must materialise view-models for them");

        // Assert against the document rather than the view-model, so this proves the
        // typed text reached the stored answer rather than merely the display.
        const string awkward = "N/A — ask my daughter";
        foreach (var vm in shell.PromptViewModels)
        {
            if (vm.IsReadOnly)
            {
                continue;   // a presentation choice; not typed into here
            }
            vm.Response = awkward;

            var stored = ResponsesById(document);
            stored.Should().ContainKey(vm.Id);
            stored[vm.Id].Should().Be(awkward,
                $"prompt '{vm.Id}' must store exactly what was typed, whatever its type hint");
        }
    }

    public static IEnumerable<object[]> InvalidCorpusFiles() =>
        Directory.GetFiles(CorpusDir("invalid"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> MalformedCorpusFiles() =>
        Directory.GetFiles(CorpusDir("malformed"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    /// <summary>
    /// A structurally invalid document still opens in the desktop app.
    /// </summary>
    /// <remarks>
    /// Specification section 6.3 keeps parsing and validation as separate stages so a
    /// reader can load a flawed document and show a person what is wrong with it, rather
    /// than refusing to open it and leaving them with nothing.
    /// </remarks>
    [Theory]
    [MemberData(nameof(InvalidCorpusFiles))]
    public void AnInvalidDocument_StillOpens_SoTheProblemCanBeShown(string path)
    {
        var open = () => Open(path);

        open.Should().NotThrow($"{Path.GetFileName(path)} is invalid, not unreadable");
    }

    /// <summary>
    /// A malformed document is refused rather than silently repaired.
    /// </summary>
    /// <remarks>
    /// The strings-only guarantee is only worth something if a violation is refused. A
    /// response given as a JSON number must not become "42" — that would invent data
    /// nobody entered.
    /// </remarks>
    [Theory]
    [MemberData(nameof(MalformedCorpusFiles))]
    public void AMalformedDocument_IsRefused_NotSilentlyRepaired(string path)
    {
        var parse = () => Serializer.Deserialize(File.ReadAllText(path));

        parse.Should().Throw<SerializationException>(
            $"{Path.GetFileName(path)} must not be coerced into something that looks valid");
    }

    /// <summary>
    /// The desktop app reports a broken signature without withholding the data.
    /// </summary>
    /// <remarks>
    /// Specification section 9.5: a signature is an assertion about a document, never a
    /// permission to read it. A tampered form still opens and still shows its answers.
    /// </remarks>
    [Fact]
    public void ATamperedSignature_DoesNotStopTheDocumentOpening()
    {
        var path = Path.Combine(CorpusDir("signatures"), "tampered-metadata-url.aprt");

        var (shell, document) = Open(path);

        document.Signatures.Should().NotBeNullOrEmpty("the signature is still present");
        shell.PromptViewModels.Should().NotBeEmpty("and the answers are still readable");

        var results = Core.Signing.AprVerifier.VerifyAll(document);
        results.Should().Contain(r => !r.ContentValid, "while verification correctly fails");
    }

    private static SortedDictionary<string, string> ResponsesById(AprDocument document)
    {
        var responses = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Walk(Section section)
        {
            foreach (var prompt in section.Prompts)
            {
                responses[prompt.Id] = prompt.Response;
            }
            foreach (var child in section.Sections)
            {
                Walk(child);
            }
        }
        foreach (var section in document.Sections)
        {
            Walk(section);
        }
        return responses;
    }

    private static IEnumerable<string> UnknownMemberNames(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.StartsWith("x-", StringComparison.Ordinal))
                    {
                        yield return property.Name;
                    }
                    foreach (var nested in UnknownMemberNames(property.Value))
                    {
                        yield return nested;
                    }
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in UnknownMemberNames(item))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }
}
