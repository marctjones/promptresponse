namespace PromptResponse.AccessibilityTests;

/// <summary>Locates the desktop sources these tests assert against.</summary>
/// <remarks>
/// Reading returns the file or throws. It used to return null when the file was
/// absent, and every caller opened with "if (xamlContent is null) return;" so CI
/// would not break. FormFillingView.axaml was then deleted, and the guard did
/// exactly what it was written to do: twelve tests kept passing while asserting
/// nothing, in the suite CI calls non-negotiable.
///
/// A missing file is now a failure, and views are discovered from disk rather
/// than named by hand, so a rename cannot silence a test and a new view is
/// covered the day it is added.
/// </remarks>
internal static class KeyboardNavigationTestFiles
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    public static string DesktopPath(string relativePath) => Path.Combine(ProjectRoot, "src", "PromptResponse.Desktop", relativePath);

    public static string ReadDesktopFile(string relativePath) => Read(DesktopPath(relativePath));

    public static string ReadDocumentation() => Read(Path.Combine(ProjectRoot, "docs", "UX_ACCESSIBILITY.md"));

    /// <summary>Every desktop view on disk, as (repository-relative path, contents).</summary>
    public static IEnumerable<object[]> AllViews()
    {
        var directory = DesktopPath("Views");
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.axaml", SearchOption.AllDirectories).OrderBy(f => f).ToArray()
            : [];
        if (files.Length == 0)
            throw new InvalidOperationException(
                $"no .axaml views found under {directory}; these tests assert against them, "
                + "and finding none must fail rather than yield no cases");
        foreach (var file in files)
            yield return [Path.GetRelativePath(ProjectRoot, file)];
    }

    public static string ReadView(string repositoryRelativePath) => Read(Path.Combine(ProjectRoot, repositoryRelativePath));

    private static string Read(string path) =>
        File.Exists(path)
            ? File.ReadAllText(path)
            : throw new FileNotFoundException(
                $"{path} does not exist. A test whose subject is missing must fail, "
                + "not pass by skipping itself.", path);
}
