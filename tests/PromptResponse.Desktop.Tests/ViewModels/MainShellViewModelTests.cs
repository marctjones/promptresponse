using NSubstitute;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>Shared fixture for focused MainShellViewModel workflow suites.</summary>
public partial class MainShellViewModelTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static MainShellViewModel CreateShell(
        IFileService? fileService = null,
        IDialogService? dialogService = null,
        IDocumentSessionService? session = null,
        IProfileService? profile = null,
        IRecentFilesService? recentFiles = null,
        ITemplateCatalogService? templateCatalog = null)
    {
        fileService ??= Substitute.For<IFileService>();
        dialogService ??= Substitute.For<IDialogService>();
        session ??= new DocumentSessionService();
        profile ??= new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        return new MainShellViewModel(fileService, dialogService, session, profile, factory,
            recentFiles: recentFiles, templateCatalog: templateCatalog);
    }

    private static AprDocument MakeTemplate() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Test Template" },
        Sections = new List<Section>
        {
            new()
            {
                Id = "s1", Title = "Section 1",
                Prompts = new List<Prompt>
                {
                    new() { Id = "p1", Label = "Name" },
                    new() { Id = "p2", Label = "Age", Hints = new PromptHints { ExpectedDataType = "number" } },
                },
            },
        },
    };
}
