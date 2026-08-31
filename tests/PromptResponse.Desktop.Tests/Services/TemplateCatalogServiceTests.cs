using AwesomeAssertions;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Verifies the starter-template catalog reads bundled <c>.aprt</c> files,
/// titles them from metadata, and sorts them.
/// </summary>
public class TemplateCatalogServiceTests
{
    private readonly AprJsonSerializer _serializer = new();

    private static string WriteTemplate(string dir, string fileName, string title)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path,
            $$"""
            { "version": "1.0-beta.6", "documentType": "template",
              "metadata": { "title": "{{title}}", "description": "d" },
              "sections": [ { "id": "s", "title": "S", "prompts": [] } ] }
            """);
        return path;
    }

    [Fact]
    public void Templates_AreLoadedAndSortedByTitle()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            WriteTemplate(dir, "b.aprt", "Zebra Form");
            WriteTemplate(dir, "a.aprt", "Apple Form");

            var catalog = new TemplateCatalogService(_serializer, dir);

            catalog.Templates.Select(t => t.Title).Should().Equal("Apple Form", "Zebra Form");
            catalog.Templates[0].Path.Should().EndWith("a.aprt");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MissingDirectory_YieldsEmptyCatalog()
    {
        var catalog = new TemplateCatalogService(_serializer, Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid()));
        catalog.Templates.Should().BeEmpty();
    }

    [Fact]
    public void MalformedTemplate_IsSkipped_NotCrashing()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "bad.aprt"), "{ not json");
            WriteTemplate(dir, "good.aprt", "Good Form");

            var catalog = new TemplateCatalogService(_serializer, dir);

            catalog.Templates.Should().ContainSingle().Which.Title.Should().Be("Good Form");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
