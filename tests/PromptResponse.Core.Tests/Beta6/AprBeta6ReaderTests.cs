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

    private const string YamlForm = """
        version: "1.0-beta.6"
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
