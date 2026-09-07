using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;
using TestResult = NetArchTest.Rules.TestResult;

namespace PromptResponse.Architecture.Tests;

/// <summary>
/// Fitness-function tests over the dependency directions <c>docs/ARCHITECTURE.md</c>
/// decides. That document is prose a person reads; this file is the same claim checked
/// against the built assemblies. A violation here means the document and the code have
/// drifted, not that the test is wrong — fix the code (or, if the boundary itself is
/// changing on purpose, update the document in the same change).
/// </summary>
public class DependencyDirectionTests
{
    private static readonly Assembly CoreAssembly =
        typeof(PromptResponse.Core.AprFormat).Assembly;

    private static readonly Assembly HostAbstractionsAssembly =
        typeof(PromptResponse.Host.Abstractions.IDelivery).Assembly;

    private static readonly Assembly RenderingPdfAssembly =
        typeof(PromptResponse.Rendering.Pdf.PdfDocumentRenderer).Assembly;

    // -- Core --------------------------------------------------------------
    //
    // docs/ARCHITECTURE.md: Core owns "model, serialization, validation,
    // expressions, attestations, render model" and must not own "Avalonia, file
    // dialogs, network UI".

    [Fact]
    public void Core_does_not_depend_on_avalonia()
    {
        var result = Types.InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Avalonia")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            BecauseCoreMustStayPlatformNeutral(result, "Avalonia"));
    }

    [Fact]
    public void Core_does_not_depend_on_the_desktop_or_cli_hosts()
    {
        var result = Types.InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PromptResponse.Desktop", "PromptResponse.Cli")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Core is the format library; a host depending on Core is the only " +
            "direction docs/ARCHITECTURE.md allows. Types found depending on a host: " +
            DescribeFailures(result));
    }

    [Fact]
    public void Core_does_not_depend_on_a_windows_or_macos_specific_namespace()
    {
        var result = Types.InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("System.Windows", "Microsoft.Win32", "AppKit", "Foundation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            BecauseCoreMustStayPlatformNeutral(result, "a platform-specific namespace"));
    }

    // -- Host.Abstractions ---------------------------------------------------
    //
    // docs/ARCHITECTURE.md: "PromptResponse.Host.Abstractions — interfaces and result
    // types, no implementation, no dependency on Avalonia, a platform, or Core's
    // internals." This project also carries no ProjectReference at all today, so most
    // of this is redundant with the .csproj — the point of testing it here too is that
    // a future ProjectReference addition fails a named, readable test instead of only
    // being caught in review.

    [Fact]
    public void HostAbstractions_does_not_depend_on_avalonia()
    {
        var result = Types.InAssembly(HostAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Avalonia")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a host port must work with a fake adapter on any runner, which an " +
            "Avalonia dependency in the port project itself would rule out. " +
            DescribeFailures(result));
    }

    [Fact]
    public void HostAbstractions_does_not_depend_on_core_desktop_or_cli()
    {
        var result = Types.InAssembly(HostAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PromptResponse.Core", "PromptResponse.Desktop", "PromptResponse.Cli")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a port takes bytes and returns bytes; a port that understood the APR " +
            "model or a specific host would be a second implementation of it. " +
            DescribeFailures(result));
    }

    [Fact]
    public void HostAbstractions_does_not_depend_on_a_windows_or_macos_specific_namespace()
    {
        var result = Types.InAssembly(HostAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("System.Windows", "Microsoft.Win32", "AppKit", "Foundation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a port is a named seam an adapter implements per platform; the port " +
            "project itself must not already assume one. " + DescribeFailures(result));
    }

    // -- Rendering.Pdf ---------------------------------------------------
    //
    // docs/ARCHITECTURE.md: "Renderers/importers: derived PDF/HTML and controlled
    // import" — a renderer consumed by both Desktop and the CLI, so it must not itself
    // require a UI framework.

    [Fact]
    public void RenderingPdf_does_not_depend_on_avalonia()
    {
        var result = Types.InAssembly(RenderingPdfAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Avalonia")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the PDF renderer/importer is shared by the desktop client and the CLI; " +
            "an Avalonia dependency here would make it unusable from the CLI. " +
            DescribeFailures(result));
    }

    private static string BecauseCoreMustStayPlatformNeutral(TestResult result, string what) =>
        $"Core is the reference format implementation and must have no dependency on " +
        $"{what} per docs/ARCHITECTURE.md's layer table. " + DescribeFailures(result);

    private static string DescribeFailures(TestResult result) =>
        result.FailingTypes is { Count: > 0 }
            ? "Offending types: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName))
            : string.Empty;
}
