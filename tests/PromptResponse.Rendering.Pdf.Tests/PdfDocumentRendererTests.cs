using System.Text;
using AwesomeAssertions;
using NSubstitute;
using PdfeDoc = Excise.Core.Document.PdfDocument;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Verifies the pdfe-backed PDF renderer: it produces a valid PDF, the content
/// is real (text-extractable on reopen), long forms paginate, and empty-field
/// options are honored. Round-trip assertions reopen the output with Excise.Core.
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
                    Kind = "table",
                    Sections =
                    [
                        new Section
                        {
                            Id = "y2024",
                            Title = "2024",
                            Prompts = [new Prompt { Id = "y2024.rev", Label = "Revenue", Response = "5000" }],
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
    public void Render_StampsDocumentMetadata_OntoThePdf()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Intake Form", Author = "Ada Lovelace", Description = "Please complete" },
            Sections = [new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "p", Label = "Name" }] }],
        };

        var bytes = _renderer.RenderToBytes(doc);

        using var reopened = PdfeDoc.Open(bytes);
        reopened.Title.Should().Be("Intake Form");
        reopened.Author.Should().Be("Ada Lovelace");
        reopened.Subject.Should().Be("Please complete");
    }

    [Fact]
    public void Render_NullArgs_Throw()
    {
        var act = () => _renderer.RenderToBytes(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Render_SignedDocument_ShowsSignaturesSection()
    {
        var doc = SampleDoc();
        doc.Metadata.TemplateId = "permit";
        using var cert = PromptResponse.Core.Signing.SignatureCertificates.CreateSelfSigned(
            "Town of Bloomfield", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        doc.Signatures = [PromptResponse.Core.Signing.AprSigner.SignTemplate(doc, cert, DateTime.UtcNow)];

        var bytes = _renderer.RenderToBytes(doc);

        using var pdf = PdfeDoc.Open(bytes);
        var text = string.Concat(Enumerable.Range(1, pdf.PageCount).Select(i => pdf.GetPage(i).Text));
        text.Should().Contain("Signatures").And.Contain("Town of Bloomfield").And.Contain("verified");
    }

    [Fact]
    public void Render_DrawsRunningFooter_WithPageNumbersAndGeneratedDate()
    {
        var renderer = new PdfDocumentRenderer(print: new PdfRenderOptions { GeneratedOn = "2026-01-01" });

        var bytes = renderer.RenderToBytes(SampleDoc());

        using var doc = PdfeDoc.Open(bytes);
        var text = doc.GetPage(1).Text;
        text.Should().Contain("Page 1 of 1");
        text.Should().Contain("Generated 2026-01-01");
    }

    [Fact]
    public void Render_A4_ProducesA4SizedPages()
    {
        var renderer = new PdfDocumentRenderer(print: new PdfRenderOptions { PageSize = PdfPageSize.A4 });

        var bytes = renderer.RenderToBytes(SampleDoc());

        using var doc = PdfeDoc.Open(bytes);
        doc.GetPage(1).Width.Should().BeApproximately(595.28, 1.0, "A4 is 595.28 pt wide");
    }

    [Fact]
    public void Render_FooterDisabled_OmitsPageNumbers()
    {
        var renderer = new PdfDocumentRenderer(print: new PdfRenderOptions { ShowFooter = false });

        var bytes = renderer.RenderToBytes(SampleDoc());

        using var doc = PdfeDoc.Open(bytes);
        doc.GetPage(1).Text.Should().NotContain("Page 1 of");
    }

    [Fact]
    public void Render_Archival_EmbedsFont_AndAddsPdfAStructures()
    {
        var renderer = new PdfDocumentRenderer(print: new PdfRenderOptions { Archival = true });

        var bytes = renderer.RenderToBytes(SampleDoc());
        var raw = System.Text.Encoding.Latin1.GetString(bytes);

        // PDF/A markers: pdfaid XMP + sRGB OutputIntent.
        raw.Should().Contain("pdfaid:part");
        raw.Should().Contain("/OutputIntents");
        // The Unicode font is embedded; no non-embedded base-14 leaks in.
        raw.Should().Contain("DejaVuSans");
        raw.Should().NotContain("Helvetica", "PDF/A forbids the non-embedded base-14 fonts");
        // Reopens as a valid PDF.
        using var doc = PdfeDoc.Open(bytes);
        doc.PageCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Render_Banner_AppearsTopAndBottomOfEveryPage()
    {
        var renderer = new PdfDocumentRenderer(print: new PdfRenderOptions
        {
            BannerText = "CONTROLLED UNCLASSIFIED INFORMATION",
            ShowFooter = false,
        });

        var bytes = renderer.RenderToBytes(SampleDoc());

        using var doc = PdfeDoc.Open(bytes);
        var text = doc.GetPage(1).Text;
        // Drawn at both the top and bottom margin → appears twice.
        System.Text.RegularExpressions.Regex.Matches(text, "CONTROLLED UNCLASSIFIED INFORMATION")
            .Count.Should().Be(2);
    }
}
