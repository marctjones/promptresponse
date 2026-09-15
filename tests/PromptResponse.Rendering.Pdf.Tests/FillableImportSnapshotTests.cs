using AwesomeAssertions;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Pins what <c>apr import</c> produces for every fillable form in the corpus.
/// </summary>
/// <remarks>
/// Milestone #49 unifies AcroForm import and visual conversion into one
/// pipeline. That refactor moves code every fillable PDF already depends on,
/// and its stated done-criterion is that a fillable PDF's import output is
/// unchanged by it. This is that criterion, made executable before the refactor
/// starts rather than asserted afterwards.
/// <para>
/// Snapshots are compared as serialized text through <see cref="AprJsonSerializer"/>
/// — the same path <c>apr import</c> writes through — rather than by comparing
/// object graphs, so a change in what reaches the file is caught even if the
/// in-memory model is unchanged.
/// </para>
/// <para>
/// <b>When this fails, that is the tool working.</b> A diff here means import
/// output moved; the question is whether that was intended. To accept a
/// deliberate change, rerun with <c>UPDATE_IMPORT_SNAPSHOTS=1</c> and commit the
/// diff as its own reviewable change — never edit a snapshot by hand to match,
/// which turns the pin into decoration.
/// </para>
/// </remarks>
public class FillableImportSnapshotTests
{
    /// <summary>
    /// Every corpus form carrying an AcroForm. Listed explicitly rather than
    /// globbed: a form silently dropping out of the corpus should break this,
    /// not quietly shrink its coverage.
    /// </summary>
    private static readonly string[] Forms =
    [
        "fed-w9", "fed-w4", "fed-ss4", "fed-8822", "fed-i9",
        "ct-w4", "ct-dmv-j23", "ct-dmv-b58ind", "ct-dmv-a25", "ct-dmv-a83", "ct-dmv-b225p",
    ];

    public static TheoryData<string> FillableForms() => [.. Forms];

    [Theory]
    [MemberData(nameof(FillableForms))]
    public void ImportOutputIsUnchanged(string id)
    {
        var pdf = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");
        File.Exists(pdf).Should().BeTrue($"{id} is a committed corpus form");

        // Title is passed explicitly so the snapshot does not encode the file path.
        var imported = new PdfFormImporter().Import(pdf, id);
        var actual = new AprJsonSerializer().Serialize(imported);

        var snapshot = Path.Combine(SnapshotDir, $"{id}.aprt");

        if (Environment.GetEnvironmentVariable("UPDATE_IMPORT_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(SnapshotDir);
            File.WriteAllText(snapshot, actual);
            return;
        }

        File.Exists(snapshot).Should().BeTrue(
            $"the snapshot for {id} is committed at {snapshot}; regenerate with UPDATE_IMPORT_SNAPSHOTS=1");

        var expected = File.ReadAllText(snapshot);
        if (expected == actual)
        {
            return;
        }

        // Report the first difference in APR terms rather than as a character
        // offset, because "prompt 12's label changed" is actionable and
        // "differs at index 4183" is not.
        Assert.Fail($"import output for {id} changed.\n{FirstDifference(expected, actual)}\n" +
                    "If this change is intended, rerun with UPDATE_IMPORT_SNAPSHOTS=1 and commit the diff.");
    }

    [Fact]
    public void EverySnapshotHasAForm_AndEveryFormASnapshot()
    {
        // A snapshot left behind for a form no longer in the corpus pins nothing,
        // and a form with no snapshot is silently unpinned. Both are the same
        // failure: coverage that looks complete and is not.
        Directory.Exists(SnapshotDir).Should().BeTrue();

        var onDisk = Directory.GetFiles(SnapshotDir, "*.aprt")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(x => x, StringComparer.Ordinal);
        var expected = Forms.AsEnumerable()
            .OrderBy(x => x, StringComparer.Ordinal);

        onDisk.Should().BeEquivalentTo(expected);
    }

    private static string FirstDifference(string expected, string actual)
    {
        var e = expected.Split('\n');
        var a = actual.Split('\n');
        for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
        {
            var left = i < e.Length ? e[i] : "<end of file>";
            var right = i < a.Length ? a[i] : "<end of file>";
            if (left != right)
            {
                return $"  line {i + 1}\n    was: {left.Trim()}\n    now: {right.Trim()}\n" +
                       $"  ({e.Length} lines before, {a.Length} now)";
            }
        }

        return "  files differ only in line endings";
    }

    private static string SnapshotDir => Path.Combine(
        RepoRoot, "tests", "Fixtures", "import-snapshots");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
