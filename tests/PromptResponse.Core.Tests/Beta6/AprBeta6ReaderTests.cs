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
            { "version":"1.0-beta.6", "metadata":{"title":"T"},
              "sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":""},],},], }
            """, AprRepresentation.Jsonc);

        form.Metadata.Title.Should().Be("T");
    }

    [Fact]
    public void Yaml_ReadsTheSameSemanticForm()
    {
        var form = _reader.ReadForm("""
            version: "1.0-beta.6"
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

    [Fact]
    public void Yaml_ResolvesPlainScalarsToTheJsonValueSpace()
    {
        var record = (AprFormRecord)_reader.ReadStream("""
            version: "1.0-beta.6"
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
            version: "1.0-beta.6"
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
        const string expected = "sha256:b2d48b3e183f16894e16b4c94f99f340d2c2fc5dcc32e68938f61bebcc404d0a";
        var jsonc = (AprFormRecord)_reader.ReadStream("""
            {"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"","com.example.canAddRows":true,"com.example.maxRows":5,"com.example.min":1996,"com.example.step":0.5,"com.example.scale":1e21,"com.example.epsilon":1e-7}]}]}
            """, AprRepresentation.Jsonc).Single();
        var yaml = (AprFormRecord)_reader.ReadStream("""
            version: "1.0-beta.6"
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
    public void Stream_PreservesAttestationAndAllFormOccurrences()
    {
        const string form = "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"\"}]}]}";
        const string attestation = "{\"recordType\":\"attestation\",\"version\":\"1.0-beta.6\",\"subject\":{\"digest\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"canonicalization\":\"jcs-sha256\"},\"scope\":{\"kind\":\"document\"},\"manifest\":{\"root\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"entries\":[]},\"proofs\":[],\"witnesses\":[]}";
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
            {"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":""}]}],"signatures":[]}
            """, AprRepresentation.Jsonc);

        read.Should().Throw<SerializationException>().WithMessage("*RETIRED_EMBEDDED_SIGNATURES*");
    }

    [Fact]
    public void JsoncDuplicateMember_IsRejectedBeforeSemanticParsing()
    {
        var read = () => _reader.ReadForm("""
            {"version":"1.0-beta.6","version":"1.0-beta.6","metadata":{"title":"T"},"sections":[]}
            """, AprRepresentation.Jsonc);

        read.Should().Throw<SerializationException>().WithMessage("*duplicate member*");
    }

    [Fact]
    public void Writer_RoundTripsAFormThroughYaml()
    {
        var form = _reader.ReadForm("""
            {"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}
            """, AprRepresentation.Jsonc);

        var yaml = _reader.WriteForm(form, AprRepresentation.Yaml);
        var roundTripped = _reader.ReadForm(yaml, AprRepresentation.Yaml);

        roundTripped.Sections.Single().Prompts.Single().Response.Should().Be("Ada");
    }

    [Fact]
    public void Writer_PreservesEveryStreamOccurrence()
    {
        const string form = "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"\"}]}]}";
        const string attestation = "{\"recordType\":\"attestation\",\"version\":\"1.0-beta.6\",\"subject\":{\"digest\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"canonicalization\":\"jcs-sha256\"},\"scope\":{\"kind\":\"document\"},\"manifest\":{\"root\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"entries\":[]},\"proofs\":[],\"witnesses\":[]}";
        var records = _reader.ReadStream("\u001e" + form + "\n\u001e" + attestation + "\n\u001e" + form, AprRepresentation.Jsonc);

        var written = _reader.WriteStream(records, AprRepresentation.Jsonc);
        var roundTripped = _reader.ReadStream(written, AprRepresentation.Jsonc);

        roundTripped.Should().HaveCount(3);
        roundTripped.Count(record => record is AprFormRecord).Should().Be(2);
    }
}
