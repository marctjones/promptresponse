using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>Shared corpus location and service setup for conformance behavior suites.</summary>
public abstract class ConformanceCorpusTestBase
{
    protected AprJsonSerializer Serializer { get; } = new();
    protected DocumentValidator Validator { get; } = new();

    protected static string CorpusDir(string kind)
    {
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(projectRoot, "tests", "Conformance", "v1", kind);
    }

    protected static IEnumerable<object[]> CorpusFiles(string kind) =>
        Directory.GetFiles(CorpusDir(kind), "*.apr*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new object[] { path });
}
