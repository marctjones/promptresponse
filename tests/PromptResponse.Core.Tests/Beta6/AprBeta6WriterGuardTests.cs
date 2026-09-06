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

    [Fact]
    public void ARetiredMember_IsDroppedOnRead_NotRefused()
    {
        // Retirement means the member goes, not that the document does. `signatures` is
        // the one exception, because it carried a cryptographic claim.
        var form = _reader.ReadForm(
            "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},"
            + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\","
            + "\"responseMetadata\":{\"source\":\"computed\"}}]}]}",
            AprRepresentation.Jsonc);

        form.Sections[0].Prompts[0].Extensions.Should().BeNullOrEmpty(
            "responseMetadata was retired in beta.6 and is dropped rather than preserved");
        var write = () => _reader.WriteForm(form, AprRepresentation.Jsonc);
        write.Should().NotThrow("the retired member is gone, so nothing unprefixed remains");
    }
}
