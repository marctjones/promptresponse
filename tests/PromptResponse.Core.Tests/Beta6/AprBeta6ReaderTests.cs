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
