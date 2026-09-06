using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>The advisory validator's type list is the type registry's, or it fails.</summary>
public class RegisteredTypesTests
{
    [Fact]
    public void RegisteredTypes_MatchTheTypeRegistry()
    {
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
        var registry = Path.Combine(root, "schemas", "apr-types-1.0.json");
        File.Exists(registry).Should().BeTrue($"the type registry must exist at {registry}");

        using var document = JsonDocument.Parse(File.ReadAllText(registry));
        var declared = document.RootElement
            .GetProperty("expectedDataType").GetProperty("types")
            .EnumerateArray()
            .Select(type => type.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        // Core reads no file at run time, so its list is a copy. A copy that nothing
        // compares is a copy that drifts: this one shipped without `color` and
        // `password`, and every document declaring either was told its type was not
        // registered.
        var mirrored = typeof(Core.Validation.DocumentValidator).Assembly
            .GetType("PromptResponse.Core.Validation.AdvisoryVocabulary")!
            .GetField("Registered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null) as HashSet<string>;

        mirrored.Should().BeEquivalentTo(declared,
            "the advisory validator's type list mirrors schemas/apr-types-1.0.json");
    }
}
