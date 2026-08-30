using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies field metadata and unanswered-response rendering policy.
/// </summary>
public class DocumentRenderModelBuilderFieldTests
{
    private readonly DocumentRenderModelBuilder _builder = DocumentRenderModelBuilderTestFactory.CreateBuilder();

    [Fact]
    public void Build_FieldCarriesHintsAndDataType()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts =
            [
                new Prompt
                {
                    Id = "dob",
                    Label = "Date of birth",
                    Hints = new PromptHints { HelpText = "As on ID", ExpectedDataType = "date" },
                },
            ],
        });

        var field = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<FieldBlock>().Single();

        field.HasResponse.Should().BeFalse();
        field.Value.Should().Be(RenderOptions.Default.EmptyFieldText);
        field.HelpText.Should().Be("As on ID");
        field.ExpectedDataType.Should().Be("date");
    }

    [Fact]
    public void Build_Field_CarriesPromptIdAndSuggestedValuesAsChoices()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts =
            [
                new Prompt
                {
                    Id = "color",
                    Label = "Colour",
                    Hints = new PromptHints { SuggestedValues = ["Red", "Green"] },
                },
            ],
        });

        var field = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<FieldBlock>().Single();

        field.Id.Should().Be("color");
        field.Choices.Should().Equal("Red", "Green");
    }

    [Fact]
    public void Build_Field_WithNoSuggestedValues_HasNullChoices()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1", Title = "S",
            Prompts = [new Prompt { Id = "name", Label = "Name", Response = "x" }],
        });

        _builder.Build(doc, RenderOptions.Default).Blocks.OfType<FieldBlock>().Single()
            .Choices.Should().BeNull();
    }

    [Fact]
    public void Build_ExcludeEmptyFields_DropsUnansweredPrompts()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts =
            [
                new Prompt { Id = "a", Label = "Answered", Response = "yes" },
                new Prompt { Id = "b", Label = "Blank", Response = "" },
            ],
        });

        var model = _builder.Build(doc, new RenderOptions { IncludeEmptyFields = false });

        var fields = model.Blocks.OfType<FieldBlock>().ToList();
        fields.Should().ContainSingle().Which.Label.Should().Be("Answered");
    }

    [Fact]
    public void Build_IncludeEmptyFields_UsesPlaceholderForUnanswered()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts = [new Prompt { Id = "b", Label = "Blank", Response = "" }],
        });

        var field = _builder
            .Build(doc, new RenderOptions { IncludeEmptyFields = true, EmptyFieldText = "—" })
            .Blocks.OfType<FieldBlock>().Single();

        field.HasResponse.Should().BeFalse();
        field.Value.Should().Be("—");
    }
}
