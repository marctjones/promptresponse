using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Exporting must not change the document it exported.
/// </summary>
/// <remarks>
/// <para>
/// Specification section 10.3: export layout MUST NOT be written back into the APR
/// document. The format's central claim is that content and presentation are separate -
/// which is what lets the same file open in a phone reader, a web page and a PDF without
/// any of them inheriting another's layout. A renderer that stashed a page break, a
/// computed width or a font choice on the way past would quietly end that.
/// </para>
/// <para>
/// The trap this guards is not a renderer that deliberately writes back, but one that
/// mutates the model as scratch space - filling in a default, normalising a response,
/// caching a measured height on the object it was handed. Comparing the serialized bytes
/// before and after catches all of those without needing to know which one happened.
/// </para>
/// </remarks>
public class ExportImmutabilityTests
{
    private static readonly AprJsonSerializer Serializer = new();

    private static AprDocument Form() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata
        {
            Title = "Expense Report",
            TemplateId = "expense",
            TemplateVersion = "1.0",
        },
        Sections =
        [
            new Section
            {
                Id = "claimant", Title = "Claimant",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Name", Response = "Ada Lovelace" },
                    // Deliberately awkward: a response that no widget can represent, a
                    // blank one, and a hint a renderer might be tempted to "resolve".
                    new Prompt { Id = "amount", Label = "Amount", Response = "about forty quid",
                        Hints = new PromptHints { ExpectedDataType = "currency" } },
                    new Prompt { Id = "notes", Label = "Notes", Response = string.Empty },
                ],
                Sections =
                [
                    new Section
                    {
                        Id = "approval", Title = "Approval",
                        Prompts = [new Prompt { Id = "approver", Label = "Approver" }],
                    },
                ],
            },
        ],
    };

    public static IEnumerable<object[]> Renderers() =>
    [
        ["fillable PDF", (Func<AprDocument, byte[]>)(d => Render(new FillablePdfDocumentRenderer(), d))],
        ["flat PDF", (Func<AprDocument, byte[]>)(d => Render(new PdfDocumentRenderer(), d))],
    ];

    private static byte[] Render(IDocumentRenderer renderer, AprDocument document)
    {
        using var output = new MemoryStream();
        renderer.Render(document, RenderOptions.Default, output);
        return output.ToArray();
    }

    /// <summary>Control: two serializations with nothing in between must match.</summary>
    /// <remarks>
    /// If this fails, the export tests below are measuring the serializer, not the
    /// renderer, and their diagnosis would be wrong.
    /// </remarks>
    [Fact]
    public void Control_SerializingTwiceWithNoExport_IsStable()
    {
        var document = Form();
        var first = Serializer.Serialize(document);
        Serializer.Serialize(document).Should().Be(first,
            "serializing must not itself change the document");
    }

    [Theory]
    [MemberData(nameof(Renderers))]
    public void Export_LeavesTheSourceDocumentByteIdentical(string name, Func<AprDocument, byte[]> export)
    {
        var document = Form();
        var before = Serializer.Serialize(document);

        var pdf = export(document);
        pdf.Should().NotBeEmpty($"the {name} renderer must actually produce output");

        Serializer.Serialize(document).Should().Be(before,
            $"exporting to {name} must not write anything back into the APR document " +
            "(specification section 10.3); content and presentation staying separate is " +
            "what lets one file render anywhere without inheriting another renderer's layout");
    }

    /// <summary>Exporting twice must produce the same document state as exporting once.</summary>
    /// <remarks>
    /// A renderer that mutates on first pass but is idempotent afterwards would satisfy a
    /// single-export check on its second run and hide the first-pass write.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Renderers))]
    public void RepeatedExport_DoesNotAccumulateState(string name, Func<AprDocument, byte[]> export)
    {
        var document = Form();
        var before = Serializer.Serialize(document);

        for (var i = 0; i < 3; i++)
        {
            export(document);
        }

        Serializer.Serialize(document).Should().Be(before,
            $"three {name} exports must leave the document exactly as one did");
    }
}
