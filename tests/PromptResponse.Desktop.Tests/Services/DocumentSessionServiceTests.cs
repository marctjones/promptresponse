using FluentAssertions;
using Moq;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// DocumentSessionService owns the active document's lifecycle: open / save /
/// close / dirty state / events. Pulled out of MainWindowViewModel to keep that
/// shell thin and to make the document-state contract testable in isolation.
/// </summary>
public class DocumentSessionServiceTests
{
    private static AprDocument MakeDocument(DocumentType type = DocumentType.Template, string title = "T") =>
        new()
        {
            Version = "1.0",
            DocumentType = type,
            Metadata = new Metadata { Title = title },
            Sections = new List<Section> { new() { Id = "s1", Title = "S" } },
        };

    [Fact]
    public void NewSession_HasNoDocument_AndIsNotDirty()
    {
        var session = new DocumentSessionService();

        session.CurrentDocument.Should().BeNull();
        session.IsDirty.Should().BeFalse();
        session.HasDocument.Should().BeFalse();
    }

    [Fact]
    public void Set_NewDocument_RaisesDocumentChanged_AndExposesIt()
    {
        var session = new DocumentSessionService();
        AprDocument? observed = null;
        session.DocumentChanged += (_, doc) => observed = doc;

        var doc = MakeDocument();
        session.Set(doc, filePath: null, dirty: false);

        session.CurrentDocument.Should().BeSameAs(doc);
        session.HasDocument.Should().BeTrue();
        observed.Should().BeSameAs(doc);
    }

    [Fact]
    public void MarkDirty_RaisesDirtyChangedEvent_AndUpdatesProperty()
    {
        var session = new DocumentSessionService();
        session.Set(MakeDocument(), filePath: null, dirty: false);

        var raised = 0;
        session.DirtyChanged += (_, _) => raised++;

        session.MarkDirty();

        session.IsDirty.Should().BeTrue();
        raised.Should().Be(1);
    }

    [Fact]
    public void MarkDirty_TwiceWithoutClean_RaisesEventOnlyOnce()
    {
        var session = new DocumentSessionService();
        session.Set(MakeDocument(), filePath: null, dirty: false);
        var raised = 0;
        session.DirtyChanged += (_, _) => raised++;

        session.MarkDirty();
        session.MarkDirty();

        raised.Should().Be(1, "only the transition clean→dirty is interesting");
    }

    [Fact]
    public void MarkClean_AfterDirty_TogglesIsDirty_BackToFalse()
    {
        var session = new DocumentSessionService();
        session.Set(MakeDocument(), filePath: null, dirty: true);
        session.IsDirty.Should().BeTrue();

        session.MarkClean();

        session.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Close_ClearsDocument_AndRaisesDocumentChangedWithNull()
    {
        var session = new DocumentSessionService();
        session.Set(MakeDocument(), filePath: "/tmp/x.aprt", dirty: true);

        AprDocument? observed = MakeDocument();
        session.DocumentChanged += (_, doc) => observed = doc;

        session.Close();

        session.CurrentDocument.Should().BeNull();
        session.HasDocument.Should().BeFalse();
        session.IsDirty.Should().BeFalse();
        session.CurrentFilePath.Should().BeNull();
        observed.Should().BeNull("DocumentChanged carries null on close");
    }

    [Fact]
    public void Set_WithFilePath_TracksFilePath_AndClearsOnClose()
    {
        var session = new DocumentSessionService();

        session.Set(MakeDocument(), filePath: "/tmp/example.aprt", dirty: false);

        session.CurrentFilePath.Should().Be("/tmp/example.aprt");

        session.Close();
        session.CurrentFilePath.Should().BeNull();
    }

    [Fact]
    public void Set_WithDirtyTrue_StartsInDirtyState()
    {
        var session = new DocumentSessionService();

        session.Set(MakeDocument(), filePath: null, dirty: true);

        session.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Mode_ReportsDocumentTypeFromCurrentDocument()
    {
        var session = new DocumentSessionService();
        session.Set(MakeDocument(DocumentType.Template), null);
        session.Mode.Should().Be(DocumentMode.EditingTemplate);

        session.Set(MakeDocument(DocumentType.FilledForm), null);
        session.Mode.Should().Be(DocumentMode.FillingForm);

        session.Close();
        session.Mode.Should().Be(DocumentMode.None);
    }

    [Fact]
    public void Title_ComposesUserVisibleString_FromMetadataAndDirtyMarker()
    {
        var session = new DocumentSessionService();
        var doc = MakeDocument(title: "Customer Survey");
        session.Set(doc, filePath: null, dirty: false);

        session.Title.Should().Contain("Customer Survey");

        session.MarkDirty();
        session.Title.Should().Contain("•", "convention: bullet marker indicates unsaved changes");
    }

    [Fact]
    public void Title_OnEmptySession_IsAppName()
    {
        var session = new DocumentSessionService();

        session.Title.Should().Be("PromptResponse");
    }
}
