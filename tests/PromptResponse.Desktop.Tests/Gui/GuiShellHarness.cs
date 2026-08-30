using Avalonia.Controls;
using NSubstitute;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Creates the real desktop shell with deterministic platform preferences for
/// headless GUI tests. Individual tests may configure their file and dialog
/// substitutes after construction.
/// </summary>
internal static class GuiShellHarness
{
    internal static GuiShellFixture Create(ColorScheme colorScheme = ColorScheme.Light)
    {
        var files = Substitute.For<IFileService>();
        var dialogs = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new FixedAccessibilityProbe(), applyAffordanceDefaults: false);
        if (colorScheme != ColorScheme.Light)
        {
            profile.SetColorScheme(colorScheme);
        }

        var viewModel = new MainShellViewModel(
            files,
            dialogs,
            session,
            profile,
            new PromptViewModelFactory(profile));
        var view = new MainShellView { DataContext = viewModel };
        return new GuiShellFixture(view, viewModel, session, profile, files, dialogs);
    }

    private sealed class FixedAccessibilityProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }
}

internal sealed record GuiShellFixture(
    MainShellView View,
    MainShellViewModel ViewModel,
    IDocumentSessionService Session,
    IProfileService Profile,
    IFileService Files,
    IDialogService Dialogs);
