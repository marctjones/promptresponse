using System.Text;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies the HTML renderer: a self-contained, accessible page that escapes
/// all dynamic text (an XSS boundary, since responses are arbitrary input).
/// </summary>
public class HtmlDocumentRendererTests
{
    private readonly HtmlDocumentRenderer _renderer = new();

    private static AprDocument SampleDoc() => new()
    {
        Metadata = new Metadata { Title = "Contact Form", Description = "Please complete" },
        Sections =
        [
            new Section
            {
                Id = "s1", Title = "Personal",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Full Name", Response = "Ada", Hints = new PromptHints { HelpText = "As on ID" } },
                    new Prompt { Id = "phone", Label = "Phone", Response = "" },
                ],
            },
        ],
    };

    private static string Render(HtmlDocumentRenderer r, AprDocument doc, RenderOptions? o = null) =>
        Encoding.UTF8.GetString(r.RenderToBytes(doc, o));

    [Fact]
    public void FormatMetadata_IsHtml()
    {
        _renderer.FormatId.Should().Be("html");
        _renderer.FileExtension.Should().Be(".html");
    }

    [Fact]
    public void Render_ProducesAccessibleHtmlEnvelope()
    {
        var html = Render(_renderer, SampleDoc());

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<meta charset=\"utf-8\">");
        html.Should().Contain("<title>Contact Form</title>");
        html.Should().Contain("<h1>Contact Form</h1>");
        html.Should().Contain("Full Name");
        html.Should().Contain("Ada");
        html.Should().Contain("As on ID");
        html.TrimEnd().Should().EndWith("</html>");
    }

    [Fact]
    public void Render_EscapesDynamicText_NoRawScript()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections = [new Section { Id = "s", Title = "S", Prompts = [
                new Prompt { Id = "x", Label = "Comment", Response = "<script>alert('xss')</script>" }] }],
        };

        var html = Render(_renderer, doc);

        html.Should().NotContain("<script>alert", "responses must be HTML-encoded");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Render_Table_UsesRealTableMarkupWithHeaders()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Tax" },
            Sections = [new Section
            {
                Id = "t", Title = "Years",
                TableLayout = new TableDefinition
                {
                    Columns = [new TableColumn { Id = "rev", Label = "Revenue" }],
                    FixedRows = [new FixedRow { Id = "y2024", Label = "2024" }],
                },
                Sections = [new Section { Id = "y2024", Title = "2024", Prompts = [new Prompt { Id = "y2024.rev", Response = "5000" }] }],
            }],
        };

        var html = Render(_renderer, doc);

        html.Should().Contain("<table>");
        html.Should().Contain("<th scope=\"col\">Revenue</th>");
        html.Should().Contain("<th scope=\"row\">2024</th>");
        html.Should().Contain("<td>5000</td>");
    }

    [Fact]
    public void Render_ExcludeEmptyFields_OmitsBlank()
    {
        var html = Render(_renderer, SampleDoc(), new RenderOptions { IncludeEmptyFields = false });
        html.Should().Contain("Full Name");
        html.Should().NotContain("Phone");
    }

    [Fact]
    public void Render_GoesThroughTheSharedBuilder()
    {
        var builder = Substitute.For<IDocumentRenderModelBuilder>();
        builder.Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>())
            .Returns(new RenderModel("T", null, DocumentType.Template, [new FieldBlock("L", "V", true, null, null)]));
        var renderer = new HtmlDocumentRenderer(builder);

        var html = Render(renderer, SampleDoc());

        builder.Received(1).Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>());
        html.Should().Contain("L").And.Contain("V");
    }
}
