using System.IO;
using System.Linq;
using System.Xml.Linq;
using AwesomeAssertions;
using PromptResponse.Desktop.Views;
using Xunit;

namespace PromptResponse.Desktop.Tests.Views;

/// <summary>
/// Drift guard for the About dialog's open source acknowledgements list.
///
/// The list in <see cref="AboutDialog.Acknowledgements"/> is hand-maintained
/// — adding a new runtime dependency to <c>PromptResponse.Desktop.csproj</c>
/// or <c>PromptResponse.Core.csproj</c> without updating the dialog would
/// mean we ship a binary with under-disclosed third-party code. These tests
/// fail loudly on that drift, AT BUILD TIME, so the omission is caught
/// before release.
///
/// Test-only dependencies (xUnit, NSubstitute, etc.) don't ship in the
/// user-facing binary and are excluded from this check on purpose.
/// </summary>
public class AboutDialogAcknowledgementsTests
{
    private static readonly string RepoRoot = LocateRepoRoot();

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PromptResponse.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root from " + System.AppContext.BaseDirectory);
    }

    private static IEnumerable<(string Name, string Version)> RuntimePackageReferences(string csprojRelative)
    {
        var path = Path.Combine(RepoRoot, csprojRelative);
        File.Exists(path).Should().BeTrue($"{csprojRelative} must exist for the parity check to be meaningful");

        var doc = XDocument.Load(path);
        return doc.Descendants("PackageReference")
            .Select(pr => (
                Name: pr.Attribute("Include")?.Value ?? string.Empty,
                Version: pr.Attribute("Version")?.Value ?? string.Empty))
            .Where(p => !string.IsNullOrEmpty(p.Name));
    }

    [Fact]
    public void EveryRuntimePackageReference_HasAnAcknowledgementEntry()
    {
        var declared = AboutDialog.Acknowledgements
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var runtimeRefs = RuntimePackageReferences("src/PromptResponse.Desktop/PromptResponse.Desktop.csproj")
            .Concat(RuntimePackageReferences("src/PromptResponse.Core/PromptResponse.Core.csproj"))
            .Concat(RuntimePackageReferences("src/PromptResponse.Cli/PromptResponse.Cli.csproj"))
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missing = runtimeRefs.Where(r => !declared.Contains(r)).ToArray();

        missing.Should().BeEmpty(
            "every runtime package shipping in the desktop binary must appear in AboutDialog.Acknowledgements — " +
            "otherwise the user-visible About dialog under-discloses third-party code. " +
            "Update src/PromptResponse.Desktop/Views/AboutDialog.axaml.cs to add the missing entries: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void EveryAcknowledgementEntry_VersionMatchesActualPackageReference()
    {
        var runtimeRefs = RuntimePackageReferences("src/PromptResponse.Desktop/PromptResponse.Desktop.csproj")
            .Concat(RuntimePackageReferences("src/PromptResponse.Core/PromptResponse.Core.csproj"))
            .Concat(RuntimePackageReferences("src/PromptResponse.Cli/PromptResponse.Cli.csproj"))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Version, StringComparer.OrdinalIgnoreCase);

        foreach (var ack in AboutDialog.Acknowledgements)
        {
            // .NET Runtime is special — it's not a NuGet PackageReference, it's the SDK.
            if (string.Equals(ack.Name, ".NET Runtime", StringComparison.OrdinalIgnoreCase)) continue;

            runtimeRefs.TryGetValue(ack.Name, out var actualVersion)
                .Should().BeTrue($"acknowledgement entry '{ack.Name}' refers to a package that no longer ships at runtime — remove it from AboutDialog");

            // We accept a version mismatch only if the .csproj uses a major-prefix that matches.
            // That keeps the dialog stable across patch bumps without micro-managing it.
            actualVersion.Should().StartWith(ack.Version[..ack.Version.IndexOf('.')],
                $"acknowledgement '{ack.Name}' is pinned to {ack.Version} but .csproj uses {actualVersion} — the major-version mismatch suggests the dialog is stale");
        }
    }

    [Fact]
    public void EveryAcknowledgementEntry_HasANonEmptyLicenseString()
    {
        foreach (var ack in AboutDialog.Acknowledgements)
        {
            ack.License.Should().NotBeNullOrWhiteSpace(
                $"acknowledgement '{ack.Name}' must declare a license — that is the entire point of the entry");
        }
    }
}
