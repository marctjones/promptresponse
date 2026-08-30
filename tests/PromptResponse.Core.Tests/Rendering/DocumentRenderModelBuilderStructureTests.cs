using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies document traversal into headings and ordinary fields.
/// </summary>
public class DocumentRenderModelBuilderStructureTests
{
    private readonly DocumentRenderModelBuilder _builder = DocumentRenderModelBuilderTestFactory.CreateBuilder();

    [Fact]
    public void Build_CarriesDocumentTitleAndDescription()
    {
        var model = _builder.Build(DocumentRenderModelBuilderTestFactory.CreateDocument(), RenderOptions.Default);

        model.Title.Should().Be("My Form");
        model.Description.Should().Be("A test form");
    }

    [Fact]
    public void Build_TopLevelSection_IsHeadingLevelOne()
    {
        var model = _builder.Build(
            DocumentRenderModelBuilderTestFactory.CreateDocument(new Section { Id = "s1", Title = "Personal" }),
            RenderOptions.Default);

        model.Blocks.Should().ContainSingle()
            .Which.Should().BeOfType<HeadingBlock>()
            .Which.Should().BeEquivalentTo(new { Level = 1, Text = "Personal" });
    }

    [Fact]
    public void Build_NestedSections_IncreaseHeadingLevel()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1",
            Title = "Outer",
            Sections = [new Section { Id = "s1_1", Title = "Inner" }],
        });

        var model = _builder.Build(doc, RenderOptions.Default);

        var headings = model.Blocks.OfType<HeadingBlock>().ToList();
        headings.Should().HaveCount(2);
        headings[0].Should().BeEquivalentTo(new { Level = 1, Text = "Outer" });
        headings[1].Should().BeEquivalentTo(new { Level = 2, Text = "Inner" });
    }

    [Fact]
    public void Build_PromptsBecomeFields_InOrderAfterHeading()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1",
            Title = "Contact",
            Prompts =
            [
                new Prompt { Id = "name", Label = "Name", Response = "Ada" },
                new Prompt { Id = "email", Label = "Email", Response = "ada@x.io" },
            ],
        });

        var model = _builder.Build(doc, RenderOptions.Default);

        model.Blocks[0].Should().BeOfType<HeadingBlock>();
        var fields = model.Blocks.OfType<FieldBlock>().ToList();
        fields.Should().HaveCount(2);
        fields[0].Should().BeEquivalentTo(new { Label = "Name", Value = "Ada", HasResponse = true });
        fields[1].Label.Should().Be("Email");
    }
}
