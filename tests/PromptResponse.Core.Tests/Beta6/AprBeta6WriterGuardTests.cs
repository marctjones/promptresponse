using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>Tolerant of what arrives, strict about what leaves.</summary>
public class AprBeta6WriterGuardTests
{
    private readonly AprBeta6Reader _reader = new();

    private static AprDocument Form(Dictionary<string, JsonElement>? metadataExtensions = null) => new()
    {
        Version = AprFormat.CurrentVersion,
        Metadata = new Metadata { Title = "T", Extensions = metadataExtensions },
        Sections = [new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "p", Label = "P" }] }],
    };

    private static Dictionary<string, JsonElement> Member(string name) =>
        new() { [name] = JsonDocument.Parse("\"v\"").RootElement.Clone() };

    [Theory]
    [InlineData(AprRepresentation.Jsonc)]
    [InlineData(AprRepresentation.Yaml)]
    public void AWriter_WritesNoByteOrderMark(AprRepresentation representation)
    {
        // APR-REP-018.
        var written = _reader.WriteForm(Form(), representation);

        written.Should().NotBeEmpty();
        written[0].Should().NotBe('﻿');
    }

    [Fact]
    public void AMebibyteResponse_SurvivesARoundTrip()
    {
        // APR-MODEL-127: a response of at least 1 MiB of UTF-8.
        var response = new string('é', 1024 * 512);
        System.Text.Encoding.UTF8.GetByteCount(response).Should().Be(1024 * 1024);
        var form = Form();
        form.Sections![0].Prompts![0].Response = response;

        var again = _reader.ReadForm(_reader.WriteForm(form, AprRepresentation.Jsonc), AprRepresentation.Jsonc);

        again.Sections![0].Prompts![0].Response.Should().Be(response);
    }

    [Fact]
    public void AnUnprefixedExtensionMember_IsRefusedOnWrite()
    {
        var write = () => _reader.WriteForm(Form(Member("routing")), AprRepresentation.Jsonc);

        write.Should().Throw<SerializationException>(
                "unprefixed names are reserved to the specification, so a producer minting "
                + "one collides with every member a later version adds")
            .Which.Code.Should().Be("UNPREFIXED_MEMBER");
    }

    [Fact]
    public void APrefixedExtensionMember_IsWrittenWithoutComplaint()
    {
        var write = () => _reader.WriteForm(Form(Member("com.example.routing")), AprRepresentation.Jsonc);

        write.Should().NotThrow("an extension member named by its owner is exactly what the format asks for");
    }

    [Fact]
    public void AnUnprefixedMemberThatArrived_IsStillRead()
    {
        // Reading is tolerant: refusing here would lose a document over a name, and the
        // member is preserved and ignored. The validator warns about it instead.
        var read = () => _reader.ReadForm(
            "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\",\"routing\":\"desk-4\"},"
            + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}",
            AprRepresentation.Jsonc);

        read.Should().NotThrow();
    }

    [Fact]
    public void AnUnprefixedMemberThatArrived_IsWrittenBackUnchanged()
    {
        // A stream record is written from the value that was read, so nothing in it was
        // added. APR-MODEL-021 requires a member present on read to be present, unchanged,
        // on write; APR-MODEL-031 forbids only adding one.
        var records = _reader.ReadStream(
            "\u001e{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\","
            + "\"title\":\"S\",\"tableLayout\":{\"fixedRows\":2},\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}\n",
            AprRepresentation.Jsonc);

        var written = _reader.WriteStream(records, AprRepresentation.Jsonc);

        var section = _reader.ReadStream(written, AprRepresentation.Jsonc)
            .OfType<AprFormRecord>().Single().Value.GetProperty("sections")[0];
        section.GetProperty("tableLayout").GetProperty("fixedRows").GetInt32().Should().Be(2);
    }

    [Fact]
    public void AnUnprefixedMemberOnASectionOrPrompt_IsAlsoRefused()
    {
        var document = Form();
        document.Sections[0].Extensions = Member("routing");

        var write = () => _reader.WriteForm(document, AprRepresentation.Jsonc);

        write.Should().Throw<SerializationException>().Which.Code.Should().Be("UNPREFIXED_MEMBER");
    }

    [Fact]
    public void AnUnprefixedMemberOnHints_IsAlsoRefused()
    {
        var document = Form();
        document.Sections[0].Prompts[0].Hints = new PromptHints { Extensions = Member("weight") };

        var write = () => _reader.WriteForm(document, AprRepresentation.Jsonc);

        write.Should().Throw<SerializationException>().Which.Code.Should().Be("UNPREFIXED_MEMBER");
    }

    [Fact]
    public void AWrongVersion_IsRefusedOnWriteWithItsCode()
    {
        var document = Form();
        document.Version = "1.0-beta.3";

        var write = () => _reader.WriteForm(document, AprRepresentation.Jsonc);

        write.Should().Throw<SerializationException>().Which.Code.Should().Be("UNSUPPORTED_VERSION");
    }

    private const string CarriedEverywhere =
        "{\"aprVersion\":\"1.0-beta.6\",\"routing\":\"a\",\"metadata\":{\"title\":\"T\",\"routing\":\"b\"},"
        + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"tableLayout\":{\"fixedRows\":2},"
        + "\"sections\":[{\"id\":\"n\",\"title\":\"N\",\"routing\":\"c\",\"prompts\":[{\"id\":\"q\",\"label\":\"Q\"}]}],"
        + "\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"routing\":\"d\",\"hints\":{\"weight\":\"e\"}}]}]}";

    private const string CarriedEverywhereYaml = """
        aprVersion: "1.0-beta.6"
        routing: "a"
        metadata:
          title: "T"
          routing: "b"
        sections:
          - id: "s"
            title: "S"
            tableLayout:
              fixedRows: 2
            sections:
              - id: "n"
                title: "N"
                routing: "c"
                prompts:
                  - id: "q"
                    label: "Q"
            prompts:
              - id: "p"
                label: "P"
                routing: "d"
                hints:
                  weight: "e"
        """;

    [Theory]
    [InlineData(AprRepresentation.Jsonc)]
    [InlineData(AprRepresentation.Yaml)]
    public void UnprefixedMembersTheDocumentCarried_AreWrittenBack(AprRepresentation representation)
    {
        // A writer puts back what it read (APR-MODEL-021). APR-MODEL-031 forbids adding an
        // unprefixed member, and none of these was added. Every bag the guard checks
        // carries one: the document, metadata, a section, a nested section, a prompt, hints.
        var form = _reader.ReadForm(
            representation == AprRepresentation.Yaml ? CarriedEverywhereYaml : CarriedEverywhere,
            representation);

        var written = _reader.WriteForm(form, AprRepresentation.Jsonc);

        var root = _reader.ReadStream(written, AprRepresentation.Jsonc)
            .OfType<AprFormRecord>().Single().Value;
        var section = root.GetProperty("sections")[0];
        root.GetProperty("routing").GetString().Should().Be("a");
        root.GetProperty("metadata").GetProperty("routing").GetString().Should().Be("b");
        section.GetProperty("tableLayout").GetProperty("fixedRows").ToString().Should().Be("2");
        section.GetProperty("sections")[0].GetProperty("routing").GetString().Should().Be("c");
        section.GetProperty("prompts")[0].GetProperty("routing").GetString().Should().Be("d");
        section.GetProperty("prompts")[0].GetProperty("hints").GetProperty("weight").GetString()
            .Should().Be("e");
    }

    [Theory]
    [InlineData("weight")]
    [InlineData("TableLayout")]
    public void AnUnprefixedMemberAddedAfterReading_IsStillRefused(string added)
    {
        // What arrived narrows the guard; it does not switch it off. `TableLayout` is
        // refused although `tableLayout` arrived, because member names are case-sensitive.
        var form = _reader.ReadForm(CarriedEverywhere, AprRepresentation.Jsonc);
        form.Sections[0].Extensions![added] = JsonDocument.Parse("\"v\"").RootElement.Clone();

        var write = () => _reader.WriteForm(form, AprRepresentation.Jsonc);

        var refusal = write.Should().Throw<SerializationException>().Which;
        refusal.Code.Should().Be("UNPREFIXED_MEMBER");
        refusal.Message.Should().Contain($"'{added}'");
    }
}
