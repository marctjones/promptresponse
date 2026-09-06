using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

public class AprBeta6ReaderTests
{
    private readonly AprBeta6Reader _reader = new();

    [Fact]
    public void Jsonc_CommentsAndTrailingCommas_AreSourceTrivia()
    {
        var form = _reader.ReadForm("""
            // a comment must not turn this into YAML
            { "aprVersion":"1.0-beta.6", "metadata":{"title":"T"},
              "sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":""},],},], }
            """, AprRepresentation.Jsonc);

        form.Metadata.Title.Should().Be("T");
    }

    [Fact]
    public void Yaml_ReadsTheSameSemanticForm()
    {
        var form = _reader.ReadForm("""
            aprVersion: "1.0-beta.6"
            metadata: { title: T }
            sections:
              - id: s
                title: S
                prompts:
                  - id: p
                    label: P
                    response: ""
            """, AprRepresentation.Yaml);

        form.Sections.Single().Prompts.Single().Id.Should().Be("p");
    }

    private const string YamlForm = """
        aprVersion: "1.0-beta.6"
        metadata:
          title: T
          "<<": not a merge key
        sections:
          - id: s
            title: S
            prompts:
              - id: p
                label: P
                hints:
                  exprValue: string(fee_count * 8.0)
                response: a * b & c! d

        """;

    [Fact]
    public void Yaml_IndicatorCharactersInsideAPlainScalar_AreOrdinaryContent()
    {
        // An anchor, alias or tag is a node property (specification 4.5); "&", "*"
        // and "!" inside a scalar's content are just characters of a string.
        var record = _reader.ReadStream(YamlForm, AprRepresentation.Yaml).Single().Should().BeOfType<AprFormRecord>().Subject;
        var prompt = record.Value.GetProperty("sections")[0].GetProperty("prompts")[0];
        prompt.GetProperty("hints").GetProperty("exprValue").GetString().Should().Be("string(fee_count * 8.0)");
        prompt.GetProperty("response").GetString().Should().Be("a * b & c! d");
    }

    [Theory]
    [InlineData("response: &r a * b & c! d", "anchors")]
    [InlineData("response: *r", "aliases")]
    [InlineData("response: !!str a", "tags")]
    [InlineData("response: ! a", "tags")]
    [InlineData("response: {<<: {b: 1}}", "merge keys")]
    public void Yaml_ExcludedNodeProperties_AreRejected(string response, string construct)
    {
        var source = YamlForm.Replace("response: a * b & c! d", response, StringComparison.Ordinal);
        var read = () => _reader.ReadStream(source, AprRepresentation.Yaml);
        read.Should().Throw<SerializationException>().WithMessage($"*{construct}*");
    }

    [Fact]
    public void Yaml_MergeKey_IsRejected()
    {
        var source = YamlForm.Replace("    title: S\n", "    title: S\n    <<: {description: merged}\n", StringComparison.Ordinal);
        var read = () => _reader.ReadStream(source, AprRepresentation.Yaml);
        read.Should().Throw<SerializationException>().WithMessage("*merge keys*");
    }

    [Theory]
    [InlineData("%YAML 1.2\n---\n")]
    [InlineData("%TAG !e! tag:example.com,2000:\n---\n")]
    public void Yaml_Directives_AreRejected(string directive)
    {
        var read = () => _reader.ReadStream(directive + YamlForm, AprRepresentation.Yaml);
        read.Should().Throw<SerializationException>().WithMessage("*directives*");
    }

    [Fact]
    public void Yaml_DirectiveOnALaterDocument_IsRejected()
    {
        // A directive belongs to the document that follows it, wherever that is in the stream.
        var source = YamlForm + "...\n%TAG !e! tag:example.com,2000:\n---\n" + YamlForm;
        var read = () => _reader.ReadStream(source, AprRepresentation.Yaml);
        read.Should().Throw<SerializationException>().WithMessage("*directives*");
    }

    [Fact]
    public void Yaml_ResolvesPlainScalarsToTheJsonValueSpace()
    {
        var record = (AprFormRecord)_reader.ReadStream("""
            aprVersion: "1.0-beta.6"
            metadata: { title: T }
            sections:
              - id: s
                title: S
                prompts:
                  - id: p
                    label: P
                    response: "5"
                    com.example.count: 5
                    com.example.negativeZero: -0
                    com.example.fraction: 1.5
                    com.example.exponent: 1e3
                    com.example.flag: true
                    com.example.nothing: ~
                    com.example.leadingZero: 012
                    com.example.sexagesimal: 1:30
                    com.example.yes: yes
            """, AprRepresentation.Yaml).Single();

        var prompt = record.Value.GetProperty("sections")[0].GetProperty("prompts")[0];
        prompt.GetProperty("response").ValueKind.Should().Be(JsonValueKind.String);
        prompt.GetProperty("com.example.count").GetRawText().Should().Be("5");
        prompt.GetProperty("com.example.negativeZero").ValueKind.Should().Be(JsonValueKind.Number);
        prompt.GetProperty("com.example.fraction").GetDouble().Should().Be(1.5);
        prompt.GetProperty("com.example.exponent").GetDouble().Should().Be(1000);
        prompt.GetProperty("com.example.flag").ValueKind.Should().Be(JsonValueKind.True);
        prompt.GetProperty("com.example.nothing").ValueKind.Should().Be(JsonValueKind.Null);
        prompt.GetProperty("com.example.leadingZero").GetString().Should().Be("012");
        prompt.GetProperty("com.example.sexagesimal").GetString().Should().Be("1:30");
        prompt.GetProperty("com.example.yes").GetString().Should().Be("yes");
    }

    [Fact]
    public void Yaml_RejectsANumberTooLargeForJson()
    {
        var read = () => _reader.ReadForm("""
            aprVersion: "1.0-beta.6"
            metadata: { title: T, com.example.big: 1e999 }
            sections: []
            """, AprRepresentation.Yaml);

        read.Should().Throw<SerializationException>().WithMessage("*non-finite*");
    }

    /// <summary>
    /// The same numeric extension members read from JSONC and from YAML digest
    /// identically, and to the value pinned across the Python, TypeScript and Java SDKs.
    /// </summary>
    [Fact]
    public void Digest_OfNumericExtensionMembers_IsRepresentationNeutral()
    {
        // Pinned against the SDK-free oracle in scripts/aprlib.py, which is itself held
        // to RFC 8785's published vectors by scripts/check-oracle.py. A hand-computed
        // constant would only prove this implementation agrees with itself.
        const string expected = "sha256:df7259065a4e63df08be70e66bfe7c85412e42a3af6d477ccab2d285a62c9fa8";
        var jsonc = (AprFormRecord)_reader.ReadStream("""
            {"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"","com.example.canAddRows":true,"com.example.maxRows":5,"com.example.min":1996,"com.example.step":0.5,"com.example.scale":1e21,"com.example.epsilon":1e-7}]}]}
            """, AprRepresentation.Jsonc).Single();
        var yaml = (AprFormRecord)_reader.ReadStream("""
            aprVersion: "1.0-beta.6"
            metadata: { title: T }
            sections:
              - id: s
                title: S
                prompts:
                  - id: p
                    label: P
                    response: ""
                    com.example.canAddRows: true
                    com.example.maxRows: 5
                    com.example.min: 1996.0
                    com.example.step: 0.5
                    com.example.scale: 1000000000000000000000
                    com.example.epsilon: 0.0000001
            """, AprRepresentation.Yaml).Single();

        AprSemanticDigest.Digest(jsonc.Value).Should().Be(expected);
        AprSemanticDigest.Digest(yaml.Value).Should().Be(expected);
    }

    [Fact]
    public void Digest_OfNonAsciiText_MatchesTheOracle()
    {
        // JCS escapes the JSON-mandated set and nothing else, so a non-ASCII character
        // stays literal in the canonical bytes and a control character becomes \u001f.
        // Two implementations can read RFC 8785 and disagree about that, so this is
        // settled against the oracle rather than by argument: the expectation comes from
        // scripts/aprlib.py, which check-oracle.py holds to the RFC's own vectors.
        const string expected = "sha256:373da0c93c482e4c159f227afab924b5334dbf547bf3543d2f6cd4253638df62";
        var record = (AprFormRecord)_reader.ReadStream(
            "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"Z\\u00fcrich \\u00e9 \\ud83d\\ude00 \\\" \\\\ /\"},"
            + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\","
            + "\"response\":\"caf\\u00e9 \\ud83c\\udf0d \\u0009tab \\u001f\"}]}]}",
            AprRepresentation.Jsonc).Single();

        AprSemanticDigest.Digest(record.Value).Should().Be(expected);
    }

    [Fact]
    public void Stream_PreservesAttestationAndAllFormOccurrences()
    {
        const string form = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"\"}]}]}";
        const string attestation = "{\"recordType\":\"attestation\",\"aprVersion\":\"1.0-beta.6\",\"subject\":{\"digest\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"canonicalization\":\"jcs-sha256\"},\"scope\":{\"kind\":\"document\"},\"manifest\":{\"root\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"entries\":[]},\"proofs\":[],\"witnesses\":[]}";
        var stream = "\u001e" + attestation + "\n\u001e" + form + "\n\u001e" + form;

        var records = _reader.ReadStream(stream, AprRepresentation.Jsonc);

        records.Should().HaveCount(3);
        records[0].Should().BeOfType<AprAttestationRecord>();
        records.Skip(1).Should().OnlyContain(record => record is AprFormRecord);
        var read = () => _reader.ReadForm(stream, AprRepresentation.Jsonc);
        read.Should().Throw<AprStreamRequiresIterationException>();
    }

    [Fact]
    public void Beta3EmbeddedSignatures_AreRejected()
    {
        var read = () => _reader.ReadForm("""
            {"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":""}]}],"signatures":[]}
            """, AprRepresentation.Jsonc);

        read.Should().Throw<SerializationException>()
            // The code is structured data now, not a substring of the prose:
            // a caller reports what the format names without parsing English.
            .Which.Code.Should().Be("RETIRED_EMBEDDED_SIGNATURES");
    }

    [Fact]
    public void JsoncDuplicateMember_IsRejectedBeforeSemanticParsing()
    {
        var read = () => _reader.ReadForm("""
            {"aprVersion":"1.0-beta.6","aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[]}
            """, AprRepresentation.Jsonc);

        read.Should().Throw<SerializationException>().WithMessage("*duplicate member*");
    }

    [Fact]
    public void Writer_RoundTripsAFormThroughYaml()
    {
        var form = _reader.ReadForm("""
            {"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}
            """, AprRepresentation.Jsonc);

        var yaml = _reader.WriteForm(form, AprRepresentation.Yaml);
        var roundTripped = _reader.ReadForm(yaml, AprRepresentation.Yaml);

        roundTripped.Sections.Single().Prompts.Single().Response.Should().Be("Ada");
    }

    [Fact]
    public void Writer_PreservesEveryStreamOccurrence()
    {
        const string form = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"\"}]}]}";
        const string attestation = "{\"recordType\":\"attestation\",\"aprVersion\":\"1.0-beta.6\",\"subject\":{\"digest\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"canonicalization\":\"jcs-sha256\"},\"scope\":{\"kind\":\"document\"},\"manifest\":{\"root\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"entries\":[]},\"proofs\":[],\"witnesses\":[]}";
        var records = _reader.ReadStream("\u001e" + form + "\n\u001e" + attestation + "\n\u001e" + form, AprRepresentation.Jsonc);

        var written = _reader.WriteStream(records, AprRepresentation.Jsonc);
        var roundTripped = _reader.ReadStream(written, AprRepresentation.Jsonc);

        roundTripped.Should().HaveCount(3);
        roundTripped.Count(record => record is AprFormRecord).Should().Be(2);
    }
}
