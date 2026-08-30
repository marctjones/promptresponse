namespace PromptResponse.AccessibilityTests;

internal static class KeyboardNavigationTestFiles
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    public static string DesktopPath(string relativePath) => Path.Combine(ProjectRoot, "src", "PromptResponse.Desktop", relativePath);

    public static string? ReadDesktopFile(string relativePath) => ReadIfPresent(DesktopPath(relativePath));

    public static string? ReadDocumentation() => ReadIfPresent(Path.Combine(ProjectRoot, "docs", "UX_ACCESSIBILITY.md"));

    private static string? ReadIfPresent(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
