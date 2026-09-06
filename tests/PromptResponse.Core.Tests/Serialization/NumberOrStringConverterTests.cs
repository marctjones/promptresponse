using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

/// <summary>A bound keeps whichever spelling the document used.</summary>
/// <remarks>
/// `min` and `max` are a number on an ordered numeric field and a canonical-form string
/// on a temporal one. Both are the format's own spellings, so neither may be rewritten
/// into the other: writing 5 back as "5" changes the document's semantic model and
/// therefore its digest, which would break every attestation over it.
/// </remarks>
public class NumberOrStringConverterTests
{
    private static readonly JsonSerializerOptions Options =
        new() { Converters = { new NumberOrStringConverter() } };

    private static string RoundTrip(string json)
    {
        var value = JsonSerializer.Deserialize<object?>(json, Options);
        return JsonSerializer.Serialize(value, Options);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("10.5")]
    [InlineData("\"2026-01-01\"")]
    [InlineData("\"09:00\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    public void ABoundRoundTripsInTheSpellingItArrivedIn(string json)
    {
        RoundTrip(json).Should().Be(json,
            "rewriting one spelling into the other changes the document's digest");
    }

    [Fact]
    public void AWholeNumberStaysWhole()
    {
        // Not "5.0". A bound written as an integer is an integer, and the semantic model
        // is what a digest is taken over.
        RoundTrip("5").Should().Be("5");
    }

    [Theory]
    // A model built in code carries the CLR type the author wrote, not the one the
    // reader produces. Each is written as the number it is.
    [InlineData(5, "5")]
    [InlineData(-1, "-1")]
    public void AnIntBoundSetInCode_IsWrittenAsANumber(int value, string expected)
    {
        JsonSerializer.Serialize<object?>(value, Options).Should().Be(expected);
    }

    [Fact]
    public void ADecimalBoundSetInCode_IsWrittenAsANumber()
    {
        JsonSerializer.Serialize<object?>(10.5m, Options).Should().Be("10.5");
    }

    [Fact]
    public void AValueOfSomeOtherType_IsWrittenAsItsText()
    {
        // The last resort. Nothing the format defines reaches it, and writing the text
        // is better than writing nothing when something does.
        JsonSerializer.Serialize<object?>(new Uri("https://example.gov"), Options)
            .Should().Be("\"https://example.gov/\"");
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("{\"a\":1}")]
    [InlineData("true")]
    public void AValueThatIsNeitherNumberNorString_IsRefused(string json)
    {
        var read = () => JsonSerializer.Deserialize<object?>(json, Options);

        read.Should().Throw<JsonException>(
            "the member table declares a number or a string, and an array is neither");
    }
}
