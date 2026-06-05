using System.Text;
using AwesomeAssertions;
using NSubstitute;
using PdfeDoc = Pdfe.Core.Document.PdfDocument;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Verifies the pdfe-backed PDF renderer: it produces a valid PDF, the content
/// is real (text-extractable on reopen), long forms paginate, and empty-field
/// options are honored. Round-trip assertions reopen the output with Pdfe.Core.
/// </summary>
public class PdfDocumentRendererTests
{
    private readonly PdfDocumentRenderer _renderer = new();

    private static AprDocument SampleDoc() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Contact Form", Description = "Please complete all fields" },
        Sections =
        [
            new Section
            {
                Id = "s1",
                Title = "Personal",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Full Name", Response = "Ada Lovelace" },
                    new Prompt { Id = "phone", Label = "Phone", Response = "" },
                ],
            },
        ],
    };

    [Fact]
    public void FormatMetadata_IsPdf()
    {
        _renderer.FormatId.Should().Be("pdf");
        _renderer.FileExtension.Should().Be(".pdf");
    }

    [Fact]
    public void Render_ProducesValidPdfEnvelope()
    {
        var bytes = _renderer.RenderToBytes(SampleDoc());

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        // %%EOF marker near the end.
        var tail = Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 32), Math.Min(32, bytes.Length));
        tail.Should().Contain("%%EOF");
    }

    [Fact]
    public void Render_RoundTrips_TitleAndAnsweredFieldAreExtractable()
    {
        var bytes = _renderer.RenderToBytes(SampleDoc());

        using var reopened = PdfeDoc.Open(bytes);
        reopened.Pages.Count.Should().BeGreaterThanOrEqualTo(1);

        var text = reopened.GetPage(1).Text;
        text.Should().Contain("Contact Form");
        text.Should().Contain("Personal");
        text.Should().Contain("Full Name");
        text.Should().Contain("Ada Lovelace");
    }

    [Fact]
    public void Render_LongForm_PaginatesOntoMultiplePages()
    {
        var prompts = Enumerable.Range(1, 120)
            .Select(i => new Prompt { Id = $"p{i}", Label = $"Question {i}", Response = $"Answer {i}" })
            .ToList();
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Long Form" },
            Sections = [new Section { Id = "s", Title = "Everything", Prompts = prompts }],
        };

        var bytes = _renderer.RenderToBytes(doc);

        using var reopened = PdfeDoc.Open(bytes);
        reopened.Pages.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Render_ExcludeEmptyFields_OmitsBlankField()
    {
        var bytes = _renderer.RenderToBytes(SampleDoc(), new RenderOptions { IncludeEmptyFields = false });

        using var reopened = PdfeDoc.Open(bytes);
        var text = reopened.GetPage(1).Text;
        text.Should().Contain("Full Name");
        text.Should().NotContain("Phone");
    }

    [Fact]
    public void Render_Table_EmitsHeadersAndCells()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Tax" },
            Sections =
            [
                new Section
                {
                    Id = "t",
                    Title = "Years",
                    TableLayout = new TableDefinition
                    {
                        Columns = [new TableColumn { Id = "rev", Label = "Revenue" }],
                        FixedRows = [new FixedRow { Id = "y2024", Label = "2024" }],
                    },
                    Sections =
                    [
                        new Section
                        {
                            Id = "y2024",
                            Title = "2024",
                            Prompts = [new Prompt { Id = "y2024.rev", Response = "5000" }],
                        },
                    ],
                },
            ],
        };

        var bytes = _renderer.RenderToBytes(doc);

        using var reopened = PdfeDoc.Open(bytes);
        var text = reopened.GetPage(1).Text;
        text.Should().Contain("Revenue");
        text.Should().Contain("2024");
        text.Should().Contain("5000");
    }

    [Fact]
    public void Render_GoesThroughTheSharedBuilder()
    {
        var builder = Substitute.For<IDocumentRenderModelBuilder>();
        builder.Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>())
            .Returns(new RenderModel("T", null, DocumentType.Template,
                [new FieldBlock("L", "V", true, null, null)]));
        var renderer = new PdfDocumentRenderer(builder);

        var bytes = renderer.RenderToBytes(SampleDoc());

        builder.Received(1).Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>());
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_NullArgs_Throw()
    {
        var act = () => _renderer.RenderToBytes(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
