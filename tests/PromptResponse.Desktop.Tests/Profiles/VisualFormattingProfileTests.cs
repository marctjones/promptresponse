using System.Globalization;
using FluentAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.Desktop.Tests.Profiles;

/// <summary>
/// VisualFormatting renders human-readable display for profiles that benefit from it
/// (e.g., sighted users): "42000" → "42,000", "2025-04-29" → "April 29, 2025".
/// The stored response is unchanged; only the rendered display differs.
/// </summary>
public class VisualFormattingProfileTests
{
    // Pin a real culture (en-US) so currency symbol and number-grouping behaviour are
    // deterministic across CI hosts. Production wires up CultureInfo.CurrentCulture.
    private readonly VisualFormattingProfile _profile = new(new CultureInfo("en-US"));

    [Theory]
    [InlineData("42000", "42,000")]
    [InlineData("1000000", "1,000,000")]
    [InlineData("0", "0")]
    [InlineData("-50", "-50")]
    [InlineData("3.14", "3.14")]
    public void FormatDisplay_NumericResponse_AddsThousandsSeparators(string raw, string expected)
    {
        _profile.FormatDisplay(raw, "number").Should().Be(expected);
    }

    [Theory]
    [InlineData("five")]
    [InlineData("approximately 5")]
    [InlineData("n/a")]
    [InlineData("see attached")]
    [InlineData("I prefer not to say")]
    public void FormatDisplay_NonNumericNumberResponse_PassesThrough(string raw)
    {
        // Cornerstone vision invariant: response that doesn't parse as a number
        // is still valid; rendering must not mutate it.
        _profile.FormatDisplay(raw, "number").Should().Be(raw);
    }

    [Theory]
    [InlineData("42000", "$42,000.00")]
    [InlineData("0", "$0.00")]
    public void FormatDisplay_NumericCurrency_AddsSymbolAndDecimals(string raw, string expectedSubstring)
    {
        // Use Invariant culture so test is portable; production uses user's culture.
        _profile.FormatDisplay(raw, "currency").Should().Contain(expectedSubstring.Replace(",", ",").Replace(".", "."));
    }

    [Theory]
    [InlineData("varies")]
    [InlineData("see notes")]
    public void FormatDisplay_NonNumericCurrencyResponse_PassesThrough(string raw)
    {
        _profile.FormatDisplay(raw, "currency").Should().Be(raw);
    }

    [Fact]
    public void FormatDisplay_IsoDate_RendersHumanReadable()
    {
        _profile.FormatDisplay("2025-04-29", "date").Should().NotBe("2025-04-29");
        _profile.FormatDisplay("2025-04-29", "date").Should().Contain("April");
        _profile.FormatDisplay("2025-04-29", "date").Should().Contain("29");
        _profile.FormatDisplay("2025-04-29", "date").Should().Contain("2025");
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("2025/04/29")]
    [InlineData("see attached")]
    public void FormatDisplay_NonIsoDateResponse_PassesThrough(string raw)
    {
        _profile.FormatDisplay(raw, "date").Should().Be(raw);
    }

    [Fact]
    public void FormatDisplay_UnknownTypeHint_PassesThrough()
    {
        _profile.FormatDisplay("anything", null).Should().Be("anything");
        _profile.FormatDisplay("anything", "text").Should().Be("anything");
        _profile.FormatDisplay("anything", "unknown_type").Should().Be("anything");
    }

    [Fact]
    public void Name_IsStable_ForPersistedSettings()
    {
        _profile.Name.Should().Be("VisualFormatting");
    }

    [Fact]
    public void DataIntegrityInvariant_FormattedDisplayIsNotASubstituteForRawValue()
    {
        // Rendering a value must NEVER be the source of truth for save / round-trip.
        // The raw "42000" stored on disk is what survives. This test documents the rule.
        var raw = "42000";
        var formatted = _profile.FormatDisplay(raw, "number");

        formatted.Should().NotBe(raw, "VisualFormatting should differ from raw for sighted users");
        // …but if the storage layer ever wrote `formatted` instead of `raw`, the next
        // VisualFormatting open would re-format "42,000" and probably break (commas in
        // the number parser). The contract: raw text is the only persisted form.
    }
}
