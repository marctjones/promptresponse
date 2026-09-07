using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Xunit;
using PromptResponse.Core;
using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Every rendered section and prompt says which member of the document it is.
/// </summary>
/// <remarks>
/// The automation tree carries names, roles and focusability, and until now nothing that
/// identified the document member behind a control. A renderer conformance driver reports
/// one node per element with the RFC 6901 pointer to what it renders — that is what lets
/// `docs/RENDERER_CONFORMANCE.md` say *which* field is unnamed rather than that something
/// is. Matching accessible names back to labels cannot do it: a table repeats "Amount" in
/// every row, and a renderer that got a name wrong is exactly the case APR-RENDER-001
/// exists to catch.
///
/// `AutomationProperties.AutomationId` is the platform's own answer and reaches UIA,
/// AT-SPI and NSAccessibility. Ids are unique document-wide, so an id plus the document
/// recovers the pointer, which is the join the TypeScript driver already makes through
/// `data-apr-prompt`.
/// </remarks>
public class AutomationIdTests
{
    private static AprDocument Document(DocumentType type) => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = type,
        Metadata = new Metadata { Title = "Automation ids", TemplateId = "tag:example.com,2026:ids" },
        Sections =
        [
            new Section
            {
                Id = "applicant",
                Title = "Applicant",
                Prompts =
                [
                    new Prompt { Id = "full_name", Label = "Full name" },
                    new Prompt { Id = "email", Label = "Email", Hints = new PromptHints { ExpectedDataType = "email" } },
                    new Prompt { Id = "notes", Label = "Notes", Hints = new PromptHints { ExpectedDataType = "multiline" } },
                    new Prompt { Id = "agreed", Label = "Agreed", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                ],
                Sections = [new Section { Id = "employment", Title = "Employment",
                    Prompts = [new Prompt { Id = "employer", Label = "Current employer" }] }],
            },
        ],
    };

    // Both modes, because they are different views. `documentType` decides which:
    // a template opens in SectionEditorView and PromptEditorView, a filled form in
    // SectionView and the per-type prompt views, and a driver has to find the member
    // behind a control either way.
    [AvaloniaTheory]
    [InlineData(DocumentType.Template)]
    [InlineData(DocumentType.FilledForm)]
    public void EverySectionAndPromptViewReportsItsDocumentId(DocumentType type)
    {
        var shell = GuiShellHarness.Create();
        shell.Session.Set(Document(type), filePath: null);
        shell.View.ShowInWindow(width: 1200, height: 900);

        var found = shell.View.GetVisualDescendants().OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);

        found.Should().Contain(
            ["applicant", "employment", "full_name", "email", "notes", "agreed", "employer"],
            "a driver joins a control to the member it renders through this id, and a "
            + "section nested inside another is reached the same way");
    }

    [AvaloniaTheory]
    [InlineData(DocumentType.Template)]
    [InlineData(DocumentType.FilledForm)]
    public void NoTwoRenderedElementsClaimTheSameId(DocumentType type)
    {
        // Ids are unique document-wide, and the join is only sound while the rendering
        // keeps them so. Two controls answering to one id would make a driver's snapshot
        // silently describe whichever it walked last.
        var shell = GuiShellHarness.Create();
        shell.Session.Set(Document(type), filePath: null);
        shell.View.ShowInWindow(width: 1200, height: 900);

        var ids = shell.View.GetVisualDescendants().OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
    }
}
