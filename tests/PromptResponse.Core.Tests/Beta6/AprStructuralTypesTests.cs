using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>
/// A structural member carries the JSON type its member table declares, or WRONG_TYPE.
/// </summary>
/// <remarks>
/// The specification separates two conditions a typed deserializer runs together: a
/// wrongly typed <em>response</em> is a parse failure (section 7.3), and a wrongly typed
/// <em>structural member</em> is the validation error WRONG_TYPE (section 7.1). Checking
/// the parsed value before the typed model is built is what lets the right code be
/// reported, and these hold each member table to its declared types.
/// </remarks>
public class AprStructuralTypesTests
{
    private readonly AprBeta6Reader _reader = new();

    private static string Document(string sectionMembers = "", string promptMembers = "",
                                   string hintMembers = "", string metadataMembers = "",
                                   string rootMembers = "")
    {
        var hints = hintMembers.Length > 0 ? $",\"hints\":{{{hintMembers}}}" : "";
        return "{\"aprVersion\":\"1.0-beta.6\""
            + rootMembers
            + ",\"metadata\":{\"title\":\"T\"" + metadataMembers + "}"
            + ",\"sections\":[{\"id\":\"s\",\"title\":\"S\"" + sectionMembers
            + ",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"" + promptMembers + hints + "}]}]}";
    }

    private string? CodeFor(string document)
    {
        try
        {
            _reader.ReadForm(document, AprRepresentation.Jsonc);
            return null;
        }
        catch (SerializationException exception)
        {
            return exception.Code ?? "PARSE_ERROR";
        }
    }

    [Theory]
    // Section members.
    [InlineData(",\"canAddRows\":\"true\"", "", "", "", "")]
    [InlineData(",\"maxRows\":\"5\"", "", "", "", "")]
    [InlineData(",\"kind\":5", "", "", "", "")]
    // Prompt members. A response is always a string.
    [InlineData("", ",\"response\":42", "", "", "")]
    [InlineData("", ",\"response\":true", "", "", "")]
    [InlineData("", ",\"role\":[]", "", "", "")]
    [InlineData("", ",\"hints\":\"none\"", "", "", "")]
    // Hint members.
    [InlineData("", "", "\"step\":\"1\"", "", "")]
    [InlineData("", "", "\"exprValue\":5", "", "")]
    [InlineData("", "", "\"exprExpected\":42", "", "")]
    [InlineData("", "", "\"suggestedValues\":[1,2]", "", "")]
    // Metadata members.
    [InlineData("", "", "", ",\"submissionUrls\":\"https://x\"", "")]
    [InlineData("", "", "", ",\"regarding\":[1]", "")]
    // Root members.
    [InlineData("", "", "", "", ",\"documentType\":5")]
    public void AMistypedStructuralMember_IsWrongType(
        string section, string prompt, string hints, string metadata, string root)
    {
        CodeFor(Document(section, prompt, hints, metadata, root))
            .Should().Be("WRONG_TYPE",
                "the document parses; it says something the format does not allow");
    }

    [Theory]
    [InlineData(",\"canAddRows\":true", "", "", "", "")]
    [InlineData(",\"maxRows\":5", "", "", "", "")]
    [InlineData("", ",\"response\":\"42\"", "", "", "")]
    [InlineData("", "", "\"step\":0.5", "", "")]
    [InlineData("", "", "\"suggestedValues\":[\"a\"]", "", "")]
    [InlineData("", "", "", ",\"regarding\":[\"sha256:ab\"]", "")]
    // An unknown member is preserved and ignored whatever it holds, which is what makes
    // additive change to the format safe.
    [InlineData("", ",\"com.example.priority\":2", "", "", "")]
    public void AWellTypedMember_IsAccepted(
        string section, string prompt, string hints, string metadata, string root)
    {
        CodeFor(Document(section, prompt, hints, metadata, root)).Should().BeNull();
    }

    [Theory]
    // `min` and `max` are "number or string" across the format, but for one field they
    // are one or the other: a bound must be comparable in the space the field lives in.
    [InlineData("number", "\"min\":1", null)]
    [InlineData("number", "\"min\":\"1\"", "WRONG_TYPE")]
    [InlineData("currency", "\"max\":10.5", null)]
    [InlineData("range", "\"max\":\"10\"", "WRONG_TYPE")]
    [InlineData("date", "\"min\":\"2026-01-01\"", null)]
    [InlineData("date", "\"min\":2026", "WRONG_TYPE")]
    [InlineData("time", "\"max\":9", "WRONG_TYPE")]
    [InlineData("datetime", "\"min\":\"2026-01-01T00:00:00Z\"", null)]
    // A type with no ordered space imposes neither spelling.
    [InlineData("text", "\"min\":1", null)]
    [InlineData("text", "\"min\":\"a\"", null)]
    public void ABoundIsTypedByTheFieldItBounds(string declared, string bound, string? expected)
    {
        CodeFor(Document(hintMembers: $"\"expectedDataType\":\"{declared}\",{bound}"))
            .Should().Be(expected,
                "a number on a date field is not an early date, it is a different kind of thing");
    }

    [Theory]
    // Written out rather than composed, because the helper appends and these three
    // replace: appending a second `sections` makes a duplicate member, which is a
    // parse failure that fires first and would hide what this is testing.
    [InlineData("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":5},\"sections\":[]}")]
    [InlineData("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":{}}")]
    [InlineData("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},"
        + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":{}}]}")]
    public void AMistypedContainer_IsWrongType(string document)
    {
        CodeFor(document).Should().Be("WRONG_TYPE");
    }

    [Fact]
    public void AnExplicitNullIsNotAWrongType()
    {
        // Null is absence as far as a member table's type goes. Whether null may appear
        // at all is APR-REP-014's question, and it answers with a parse failure.
        CodeFor(Document(promptMembers: ",\"role\":null")).Should().NotBe("WRONG_TYPE");
    }
}
