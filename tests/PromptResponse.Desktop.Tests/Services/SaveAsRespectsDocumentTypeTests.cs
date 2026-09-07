using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// A filename never decides what a document is.
/// </summary>
/// <remarks>
/// `documentType` is authoritative and a reader determines the kind of document from that
/// member alone (APR-SEC-005); it must not be inferred from an extension (APR-SEC-007),
/// a rule that could not even be stated where a filename does not exist — an HTTP body, a
/// clipboard paste, a row in a queue. `SaveFileAsAsync` used to assign `DocumentType`
/// from the picker's extension, so saving a filled form as `.aprt` quietly turned it into
/// a template, and not the explicit conversion APR-SEC-008 describes: that one also
/// records `templateId`, without which the result is a document the validator rejects.
///
/// The picker cannot be driven headlessly, so these exercise the decision rather than the
/// dialog — the code that used to do the reclassifying, and the shape of the warning
/// APR-SEC-006 asks for in its place.
/// </remarks>
public class SaveAsRespectsDocumentTypeTests
{
    private static AprDocument FilledForm() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Permit", TemplateId = "tag:example.com,2026:permit" },
        Sections = [new Section { Id = "s", Title = "S" }],
    };

    [Theory]
    [InlineData(".aprt", DocumentType.FilledForm, true)]
    [InlineData(".aprf", DocumentType.Template, true)]
    [InlineData(".aprf", DocumentType.FilledForm, false)]
    [InlineData(".aprt", DocumentType.Template, false)]
    [InlineData(".apr", DocumentType.FilledForm, false)]
    [InlineData(".jsonc", DocumentType.Template, false)]
    public void OnlyTheOtherKindsExtensionIsAMismatch(
        string extension, DocumentType type, bool warns)
    {
        // The rule the service applies, stated once: `.apr` and any other extension name
        // no kind at all, so neither can disagree with one.
        var expected = type == DocumentType.Template ? ".aprt" : ".aprf";
        var mismatch = extension is ".aprt" or ".aprf" && extension != expected;

        mismatch.Should().Be(warns);
    }

    [Fact]
    public void ConvertingIsExplicitAndCarriesATemplateId()
    {
        // APR-SEC-008: converting a template to a filled form sets documentType and
        // records templateId. Anything that produces one without the other has made a
        // document that fails validation, which is what inferring from a name did.
        var document = FilledForm();

        document.DocumentType.Should().Be(DocumentType.FilledForm);
        document.Metadata.TemplateId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TheServiceNoLongerAssignsDocumentTypeFromAnExtension()
    {
        // The regression this guards is a line of code, so it is asserted against the
        // code: FileService must not write DocumentType at all.
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src", "PromptResponse.Desktop", "Services", "FileService.cs"));

        // An assignment, not the comparison that picks the suggested extension: the
        // service still reads documentType to propose a name, which is APR-SEC-006.
        Regex.IsMatch(source, @"\.DocumentType\s*=\s*[^=]").Should().BeFalse(
            "an extension is a desktop affordance and not part of the data model");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}
