using AwesomeAssertions;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Signing;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>
/// Guards the shared APR conformance corpus. Other SDKs should run equivalent
/// checks against the same files so format compatibility is testable.
/// </summary>
public class ConformanceCorpusTests
{
    private readonly AprJsonSerializer _serializer = new();
    private readonly DocumentValidator _validator = new();

    public static IEnumerable<object[]> ValidCorpusFiles() =>
        Directory.GetFiles(CorpusDir("valid"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> InvalidCorpusFiles() =>
        Directory.GetFiles(CorpusDir("invalid"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> MalformedCorpusFiles() =>
        Directory.GetFiles(CorpusDir("malformed"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> TamperedSignatureFiles() =>
        Directory.GetFiles(CorpusDir("signatures"), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(ValidCorpusFiles))]
    public void ValidCorpus_Deserializes_Validates_AndRoundTrips(string path)
    {
        var originalJson = File.ReadAllText(path);

        var document = _serializer.Deserialize(originalJson);
        var result = _validator.Validate(document);

        result.IsValid.Should().BeTrue($"{Path.GetFileName(path)} is a valid conformance fixture");
        result.Errors.Should().BeEmpty();
        document.Sections.Should().NotBeEmpty();

        var roundTripJson = _serializer.Serialize(document);
        var roundTripped = _serializer.Deserialize(roundTripJson);
        var roundTripResult = _validator.Validate(roundTripped);

        roundTripResult.IsValid.Should().BeTrue($"{Path.GetFileName(path)} must stay valid after serialize/deserialize");
        roundTripResult.Errors.Should().BeEmpty();

        var model = new DocumentRenderModelBuilder().Build(roundTripped, RenderOptions.Default);
        model.Blocks.Should().NotBeEmpty($"{Path.GetFileName(path)} should produce renderable output");

        // Responses are user data: a round-trip MUST NOT alter a single one of them.
        // This is what makes "any string is a valid response" a guarantee rather than
        // an aspiration, and it is the assertion most likely to catch an over-eager
        // sanitizer or a type-hint that has quietly started enforcing something.
        ResponsesById(document).Should().Equal(ResponsesById(roundTripped),
            $"{Path.GetFileName(path)} must preserve every response byte-for-byte across a round-trip");
    }

    [Theory]
    [MemberData(nameof(InvalidCorpusFiles))]
    public void InvalidCorpus_Deserializes_ButDoesNotValidate(string path)
    {
        var json = File.ReadAllText(path);

        var document = _serializer.Deserialize(json);
        var result = _validator.Validate(document);

        result.IsValid.Should().BeFalse($"{Path.GetFileName(path)} is intentionally invalid");
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// Files under <c>malformed/</c> violate the format at the JSON layer — a response
    /// given as a JSON number or boolean, a structurally wrong shape, truncated bytes.
    /// They MUST be rejected at parse time, not merely reported as invalid, because a
    /// reader that silently coerced them would break the strings-only guarantee.
    /// </summary>
    [Theory]
    [MemberData(nameof(MalformedCorpusFiles))]
    public void MalformedCorpus_IsRejectedAtParseTime(string path)
    {
        var json = File.ReadAllText(path);

        var parse = () => _serializer.Deserialize(json);

        parse.Should().Throw<SerializationException>(
            $"{Path.GetFileName(path)} is malformed and must not be silently coerced");
    }

    /// <summary>
    /// Structurally valid documents whose signatures no longer verify because the
    /// covered content was altered. Structural validation MUST still pass — tampering
    /// is a verification result, not a schema error.
    /// </summary>
    [Theory]
    [MemberData(nameof(TamperedSignatureFiles))]
    public void TamperedSignatures_ValidateStructurally_ButFailVerification(string path)
    {
        var document = _serializer.Deserialize(File.ReadAllText(path));

        _validator.Validate(document).IsValid.Should().BeTrue(
            $"{Path.GetFileName(path)} is structurally valid; only its signature is broken");

        var results = AprVerifier.VerifyAll(document);

        results.Should().NotBeEmpty($"{Path.GetFileName(path)} carries at least one signature");
        results.Should().Contain(r => !r.ContentValid,
            $"{Path.GetFileName(path)} was tampered with and must fail verification");
    }

    /// <summary>
    /// The signed fixture in <c>valid/</c> must verify as shipped, so the corpus proves
    /// the canonical payload is reproducible rather than merely well-formed.
    /// </summary>
    [Fact]
    public void SignedFixture_VerifiesAsShipped()
    {
        var path = Path.Combine(CorpusDir("valid"), "signed-template.aprt");
        var document = _serializer.Deserialize(File.ReadAllText(path));

        var results = AprVerifier.VerifyAll(document);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.ContentValid,
            "the shipped signed fixture must verify over its own unmodified content");
    }

    /// <summary>
    /// A type hint never rewrites a response. The url/email hints once triggered strict
    /// character stripping at the serialization boundary, which made the stored bytes
    /// depend on a hint the AUTHOR chose rather than on what the FILLER typed — a hint
    /// enforcing something, which the format forbids.
    /// </summary>
    [Fact]
    public void TypeHints_NeverRewriteAResponse()
    {
        var path = Path.Combine(CorpusDir("valid"), "hidden-characters-preserved.aprf");
        var originalJson = File.ReadAllText(path);

        var responses = ResponsesById(_serializer.Deserialize(
            _serializer.Serialize(_serializer.Deserialize(originalJson))));

        const string zeroWidthSpace = "\u200b";
        responses["url_hint"].Should().Contain(zeroWidthSpace,
            "a url hint describes what the author hoped for; it does not license editing the answer");
        responses["email_hint"].Should().Contain(zeroWidthSpace);
        responses["text_hint"].Should().Contain(zeroWidthSpace);
        responses["persian_zwnj"].Should().Contain("\u200c", "legitimate ZWNJ must survive too");
    }

    /// <summary>
    /// The strictness that used to be spent on filler answers belongs on the submission
    /// URL instead: authored, machine-consumed, and bound into the publisher signature.
    /// It is reported and blocks signing rather than being silently cleaned — choosing a
    /// replacement host is the author's decision.
    /// </summary>
    [Fact]
    public void SubmissionUrlWithHiddenCharacters_IsReportedAndBlocksSigning()
    {
        var document = _serializer.Deserialize(
            File.ReadAllText(Path.Combine(CorpusDir("valid"), "signed-template.aprt")));
        document.Signatures = null;
        document.Metadata.SubmissionUrls = ["https://bloomfield\u200bct.gov/submit"];

        // Still a valid document — this is an advisory, not a structural error.
        _validator.Validate(document).IsValid.Should().BeTrue();

        new HiddenCharacterAdvisor().Validate(document).Warnings
            .Should().Contain(w => w.WarningCode == "SUBMISSION_URL_HIDDEN_CHARS");

        using var cert = SignatureCertificates.CreateSelfSigned(
            "Conformance", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var sign = () => AprSigner.SignTemplate(document, cert, DateTime.UtcNow);

        sign.Should().Throw<InvalidOperationException>(
            "binding a URL that renders as one host and resolves as another defeats the binding");
    }

    /// <summary>
    /// The published type registry must agree with the implementation.
    /// </summary>
    /// <remarks>
    /// The vocabulary of a format is exactly the kind of fact that ends up stated in
    /// several places and then drifts. This asserts that every type the registry names
    /// maps to the CEL type it claims, and that its canonical boolean forms are the ones
    /// the code actually writes.
    /// </remarks>
    [Fact]
    public void TypeRegistry_AgreesWithTheImplementation()
    {
        var registryPath = Path.GetFullPath(Path.Combine(
            CorpusDir("valid"), "..", "..", "..", "..", "schemas", "apr-types-1.0.json"));
        using var registry = System.Text.Json.JsonDocument.Parse(File.ReadAllText(registryPath));

        var mismatches = new List<string>();
        foreach (var type in registry.RootElement.GetProperty("expectedDataType").GetProperty("types").EnumerateArray())
        {
            var id = type.GetProperty("id").GetString()!;
            var claimed = type.GetProperty("celType").GetString()!;

            // Build a prompt with this hint and see what the binding layer declares.
            var document = new Core.Models.AprDocument
            {
                Metadata = new Core.Models.Metadata { Title = "T" },
                Sections =
                [
                    new Core.Models.Section
                    {
                        Id = "s", Title = "S",
                        Prompts = [new Core.Models.Prompt
                        {
                            Id = "field", Label = "Field",
                            Hints = new Core.Models.PromptHints { ExpectedDataType = id },
                        }],
                    },
                ],
            };
            var actual = Core.Expressions.FormExpressions.BuildContext(document).DeclaredTypeOf("field");

            // CEL spells timestamp as its protobuf name; compare on the meaningful part.
            var normalised = actual.ToLowerInvariant().Split('.').Last();
            var expected = claimed.ToLowerInvariant().Split('<').First();
            if (!normalised.Contains(expected) && !expected.Contains(normalised))
            {
                mismatches.Add($"{id}: registry says {claimed}, implementation declares {actual}");
            }
        }

        mismatches.Should().BeEmpty("the type registry is the published vocabulary and must match the code");
    }

    /// <summary>
    /// The published expression binding vectors.
    /// </summary>
    /// <remarks>
    /// The language is CEL and is conformance-tested by cel-spec's own suite, which this
    /// project does not maintain. What is APR-specific is the binding: how
    /// expectedDataType becomes a type environment, what happens to a response that will
    /// not bind, and how a result marshals back to a stored string. That is what these
    /// vectors pin, and they are what another SDK ports against.
    /// </remarks>
    [Fact]
    public void ExpressionBinding_MatchesThePublishedVectors()
    {
        var path = Path.Combine(CorpusDir("expressions"), "vectors.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        var failures = new List<string>();
        foreach (var testCase in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = testCase.GetProperty("name").GetString()!;
            var expression = testCase.GetProperty("expr").GetString()!;
            var expected = testCase.GetProperty("expect").ValueKind == System.Text.Json.JsonValueKind.Null
                ? null
                : testCase.GetProperty("expect").GetString();

            var section = new Core.Models.Section { Id = "s", Title = "S" };
            foreach (var field in testCase.GetProperty("fields").EnumerateArray())
            {
                section.Prompts.Add(new Core.Models.Prompt
                {
                    Id = field.GetProperty("id").GetString()!,
                    Label = field.GetProperty("id").GetString()!,
                    Response = field.GetProperty("response").GetString() ?? string.Empty,
                    Hints = new Core.Models.PromptHints { ExpectedDataType = field.GetProperty("type").GetString() },
                });
            }
            // The prompt carrying the expression is an ordinary string field.
            var subject = new Core.Models.Prompt
            {
                Id = "_subject", Label = "Subject",
                Hints = new Core.Models.PromptHints { ExprValue = expression },
            };
            section.Prompts.Add(subject);

            var document = new Core.Models.AprDocument
            {
                Metadata = new Core.Models.Metadata { Title = "Vectors" },
                Sections = [section],
            };

            var context = Core.Expressions.FormExpressions.BuildContext(document);
            var actual = Core.Expressions.FormExpressions.ComputeValue(subject, context);

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failures.Add($"{name}: `{expression}` expected {expected ?? "<degrade>"} but got {actual ?? "<degrade>"}");
            }
        }

        failures.Should().BeEmpty("every published binding vector must reproduce exactly");
    }

    /// <summary>
    /// The published apr-sig-v2 vectors are the contract every other SDK ports against.
    /// If this drifts, signatures produced here stop verifying elsewhere — silently,
    /// because the CMS layer is still perfectly correct. Regenerating the vectors to
    /// make this pass is only valid alongside a deliberate canonicalization change.
    /// </summary>
    [Theory]
    [InlineData("formDefinition")]
    [InlineData("publisherPayload")]
    [InlineData("fillerPayload")]
    public void CanonicalPayload_MatchesThePublishedVector(string vectorName)
    {
        var dir = CorpusDir("canonicalization");
        using var vectors = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(dir, "vectors.json")));
        var parameters = vectors.RootElement.GetProperty("parameters");
        var expected = vectors.RootElement.GetProperty("vectors").GetProperty(vectorName);

        var document = _serializer.Deserialize(File.ReadAllText(Path.Combine(dir, "input.aprt")));
        var signedAt = parameters.GetProperty("signedAt").GetString()!;
        var fields = parameters.GetProperty("fillerFields")
            .EnumerateArray().Select(f => f.GetString()!).ToArray();

        var actual = vectorName switch
        {
            "formDefinition" => AprCanonicalizer.FormDefinition(document),
            "publisherPayload" => AprCanonicalizer.PublisherPayload(document, signedAt),
            "fillerPayload" => AprCanonicalizer.FillerPayload(document, fields, signedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(vectorName)),
        };

        // Compare the text first: a hash mismatch says "wrong", the text says "where".
        System.Text.Encoding.UTF8.GetString(actual).Should().Be(
            expected.GetProperty("canonicalText").GetString(),
            $"the {vectorName} canonical payload is a published interop contract");

        actual.Length.Should().Be(expected.GetProperty("byteLength").GetInt32());

        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual))
            .ToLowerInvariant()
            .Should().Be(expected.GetProperty("sha256").GetString());
    }

    /// <summary>
    /// Unknown members MUST survive a round-trip, not merely be tolerated on read.
    /// Without this, every additive format change is destructive: a document written
    /// by a newer minor version loses its new members the first time an older reader
    /// opens and saves it. This assertion is what makes the newer-minor rule usable.
    /// </summary>
    [Theory]
    [InlineData("unknown-fields.aprt")]
    [InlineData("newer-minor-accepted.aprt")]
    public void UnknownMembers_SurviveARoundTrip(string fixture)
    {
        var path = Path.Combine(CorpusDir("valid"), fixture);
        var originalJson = File.ReadAllText(path);

        var roundTripJson = _serializer.Serialize(_serializer.Deserialize(originalJson));

        using var original = System.Text.Json.JsonDocument.Parse(originalJson);
        using var roundTripped = System.Text.Json.JsonDocument.Parse(roundTripJson);

        var originalUnknown = UnknownMemberNames(original.RootElement).ToList();
        originalUnknown.Should().NotBeEmpty($"{fixture} is meant to carry unknown members");

        UnknownMemberNames(roundTripped.RootElement).Should().BeEquivalentTo(originalUnknown,
            $"{fixture} must preserve every unrecognised member across a round-trip");
    }

    /// <summary>Collects every "x-"-prefixed member name at any depth.</summary>
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

    /// <summary>
    /// A newer minor version is readable, and says so: it validates, and reports an
    /// advisory warning rather than an error.
    /// </summary>
    [Fact]
    public void NewerMinorVersion_ValidatesWithAnAdvisoryWarning()
    {
        var path = Path.Combine(CorpusDir("valid"), "newer-minor-accepted.aprt");

        var result = _validator.Validate(_serializer.Deserialize(File.ReadAllText(path)));

        result.IsValid.Should().BeTrue("a newer minor version is readable, not invalid");
        result.Warnings.Should().Contain(w => w.WarningCode == "NEWER_MINOR_VERSION");
    }

    /// <summary>
    /// The declared documentType wins over the filename, always.
    /// </summary>
    /// <remarks>
    /// This inverts the v0.2 draft, and it is the rule the web and mobile story rests
    /// on: a filename does not exist in an HTTP body, a database column, a clipboard
    /// paste, or a share intent. Under the old rule a browser reader and a desktop
    /// reader would reach different conclusions about identical bytes.
    /// </remarks>
    [Fact]
    public void DocumentType_IsAuthoritative_OverTheFileExtension()
    {
        var path = Path.Combine(CorpusDir("valid"), "documenttype-beats-extension.aprt");

        var document = _serializer.Deserialize(File.ReadAllText(path));

        Path.GetExtension(path).Should().Be(".aprt", "the fixture is deliberately misnamed");
        document.DocumentType.Should().Be(Core.Models.DocumentType.FilledForm,
            "documentType in the file decides, never the filename");
        _validator.Validate(document).IsValid.Should().BeTrue(
            "a filled form named .aprt is unusual, not invalid");
    }

    /// <summary>
    /// Presentation order is normative (specification section 10.2): a section's own
    /// prompts render BEFORE its child sections. A corpus fixture alone cannot pin
    /// this — the round-trip test only checks that blocks exist — so the sequence is
    /// asserted here. Two renderers that disagree about order show two different forms.
    /// </summary>
    [Fact]
    public void SectionOrdering_OwnPromptsRenderBeforeChildSections()
    {
        var path = Path.Combine(CorpusDir("valid"), "section-ordering.aprt");
        var document = _serializer.Deserialize(File.ReadAllText(path));

        var model = new DocumentRenderModelBuilder().Build(document, RenderOptions.Default);

        var fieldIds = model.Blocks.OfType<FieldBlock>().Select(b => b.Id).ToList();

        fieldIds.Should().Equal(
            new[] { "own_first", "own_second", "child_first", "child_second" },
            "a section's own prompts must precede its child sections, in array order");
    }

    private static SortedDictionary<string, string> ResponsesById(Core.Models.AprDocument document)
    {
        var responses = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Walk(Core.Models.Section section)
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

    private static string CorpusDir(string kind)
    {
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(projectRoot, "tests", "Conformance", "v1", kind);
    }
}
