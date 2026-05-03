using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using PromptResponse.Desktop.Views;
using Xunit;

namespace PromptResponse.Desktop.Tests.Views;

/// <summary>
/// Drift guard for the About dialog's open source acknowledgements list.
///
/// The list in <see cref="AboutDialog.Acknowledgements"/> is hand-maintained
/// — adding (or transitively pulling in) a new runtime NuGet without
/// updating the dialog would mean we ship a binary with under-disclosed
/// third-party code. These tests fail loudly on that drift, AT BUILD TIME,
/// so the omission is caught before release.
///
/// We assert against each runtime project's resolved package closure
/// (<c>obj/project.assets.json</c>), not just the direct
/// <c>PackageReference</c> entries — so a transitive bump that pulls in a
/// brand-new package also fails the build until the dialog catches up.
///
/// Test-only dependencies (xUnit, NSubstitute, etc.) and packages flagged
/// <c>developmentDependency=true</c> in their <c>.nuspec</c> (build-time
/// helpers like <c>Avalonia.BuildServices</c>) don't ship in the
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

    /// <summary>
    /// Reads the full resolved NuGet closure from a project's
    /// <c>project.assets.json</c> (the lockfile written by <c>dotnet restore</c>),
    /// skipping packages flagged as <c>developmentDependency</c> in their
    /// nuspec — those are build-time helpers and never ship at runtime.
    /// </summary>
    private static IEnumerable<(string Name, string Version)> ResolvedRuntimeClosure(string projectRelativeDir)
    {
        var assetsPath = Path.Combine(RepoRoot, projectRelativeDir, "obj", "project.assets.json");
        File.Exists(assetsPath).Should().BeTrue(
            $"{projectRelativeDir}/obj/project.assets.json must exist — " +
            "run `dotnet restore` (or `dotnet build`) before running this test");

        using var doc = JsonDocument.Parse(File.ReadAllText(assetsPath));
        if (!doc.RootElement.TryGetProperty("libraries", out var libraries))
        {
            yield break;
        }

        var nugetCache = Path.Combine(
            Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"));

        foreach (var lib in libraries.EnumerateObject())
        {
            if (!lib.Value.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "package")
            {
                continue;
            }

            var key = lib.Name; // "PackageId/Version"
            var slash = key.IndexOf('/');
            if (slash < 0) continue;
            var name = key[..slash];
            var version = key[(slash + 1)..];

            if (IsDevelopmentDependency(nugetCache, name, version))
            {
                continue;
            }

            yield return (name, version);
        }
    }

    private static bool IsDevelopmentDependency(string nugetCache, string name, string version)
    {
        // .nuspec lives at $NUGET_PACKAGES/<name-lower>/<version>/<name-lower>.nuspec
        var nuspec = Path.Combine(nugetCache, name.ToLowerInvariant(), version, $"{name.ToLowerInvariant()}.nuspec");
        if (!File.Exists(nuspec)) return false;

        try
        {
            var x = XDocument.Load(nuspec);
            var ns = x.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var dev = x.Descendants(ns + "developmentDependency").FirstOrDefault()?.Value;
            return string.Equals(dev, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<(string Name, string Version)> EveryRuntimeProjectsClosure() =>
        ResolvedRuntimeClosure("src/PromptResponse.Desktop")
            .Concat(ResolvedRuntimeClosure("src/PromptResponse.Core"))
            .Concat(ResolvedRuntimeClosure("src/PromptResponse.Cli"));

    [Fact]
    public void EveryRuntimePackage_HasAnAcknowledgementEntry()
    {
        var declared = AboutDialog.Acknowledgements
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var shipped = EveryRuntimeProjectsClosure()
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missing = shipped.Where(s => !declared.Contains(s)).ToArray();

        missing.Should().BeEmpty(
            "every runtime NuGet package shipping in the desktop binary must appear in AboutDialog.Acknowledgements — " +
            "otherwise the user-visible About dialog under-discloses third-party code. " +
            "Update src/PromptResponse.Desktop/Views/AboutDialog.axaml.cs to add the missing entries: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void EveryAcknowledgementEntry_RefersToAShippingPackage()
    {
        var shipped = EveryRuntimeProjectsClosure()
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Version, StringComparer.OrdinalIgnoreCase);

        foreach (var ack in AboutDialog.Acknowledgements)
        {
            // .NET Runtime is special — it's not a NuGet PackageReference, it's the SDK.
            if (string.Equals(ack.Name, ".NET Runtime", StringComparison.OrdinalIgnoreCase)) continue;

            shipped.TryGetValue(ack.Name, out var actualVersion)
                .Should().BeTrue($"acknowledgement entry '{ack.Name}' refers to a package that no longer ships at runtime — remove it from AboutDialog");

            // Major-version match keeps the dialog stable across patch bumps without micro-managing it.
            // For pre-release / dotted versions like "3.119.4-preview.1.1" we just compare the major.
            actualVersion.Should().StartWith(ack.Version[..ack.Version.IndexOf('.')],
                $"acknowledgement '{ack.Name}' is pinned to {ack.Version} but the resolved package is {actualVersion} — the major-version mismatch suggests the dialog is stale");
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
