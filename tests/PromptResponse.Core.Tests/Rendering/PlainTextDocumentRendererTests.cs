using System.Text;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies the reference plain-text renderer that anchors the
/// <see cref="IDocumentRenderer"/> seam: it consumes the shared
/// <see cref="RenderModel"/> and produces readable UTF-8 output.
/// </summary>
public class PlainTextDocumentRendererTests
{
    private readonly PlainTextDocumentRenderer _renderer = new();

    private static AprDocument SampleDoc() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Contact Form" },
        Sections =
        [
            new Section
            {
                Id = "s1",
                Title = "Personal",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Name", Response = "Ada Lovelace" },
                    new Prompt { Id = "phone", Label = "Phone", Response = "" },
                ],
            },
        ],
    };

    private static string Read(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void FormatMetadata_IsTextAndTxt()
    {
        _renderer.FormatId.Should().Be("text");
        _renderer.FileExtension.Should().Be(".txt");
    }

    [Fact]
    public void Render_IncludesTitleSectionHeadingAndAnsweredField()
    {
        var output = Read(_renderer.RenderToBytes(SampleDoc()));

        output.Should().Contain("Contact Form");
        output.Should().Contain("# Personal");
        output.Should().Contain("Name: Ada Lovelace");
    }

    [Fact]
    public void Render_IncludeEmptyFields_ShowsPlaceholder()
    {
        var output = Read(_renderer.RenderToBytes(SampleDoc(), RenderOptions.Default));

        output.Should().Contain($"Phone: {RenderOptions.Default.EmptyFieldText}");
    }

    [Fact]
    public void Render_ExcludeEmptyFields_OmitsBlankField()
    {
        var output = Read(_renderer.RenderToBytes(SampleDoc(), new RenderOptions { IncludeEmptyFields = false }));

        output.Should().Contain("Name: Ada Lovelace");
        output.Should().NotContain("Phone:");
    }

    [Fact]
    public void Render_LeavesOutputStreamOpen()
    {
        using var stream = new MemoryStream();

        _renderer.Render(SampleDoc(), RenderOptions.Default, stream);

        // If the writer had closed the stream this would throw.
        var act = () => stream.WriteByte(0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Render_UsesTheSharedBuilder()
    {
        // Proves the renderer goes through the seam rather than re-walking the tree.
        var builder = Substitute.For<IDocumentRenderModelBuilder>();
        builder.Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>())
            .Returns(new RenderModel("T", null, DocumentType.Template,
                [new FieldBlock("L", "V", true, null, null)]));
        var renderer = new PlainTextDocumentRenderer(builder);

        var output = Read(renderer.RenderToBytes(SampleDoc()));

        builder.Received(1).Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>());
        output.Should().Contain("L: V");
    }
}
