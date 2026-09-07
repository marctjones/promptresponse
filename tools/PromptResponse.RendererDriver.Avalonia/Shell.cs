using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.RendererDriver.Avalonia;

/// <summary>The real desktop shell, with the platform edges stubbed out.</summary>
/// <remarks>
/// Everything that decides what a person faces — the views, the view models, the profile
/// service, the expression workflow — is the shipped code. Only the file picker and the
/// dialogs are replaced, because a driver has no screen to put them on, and because a
/// renderer case is about what was rendered rather than about what was saved where.
/// </remarks>
internal sealed record Shell(MainShellView View, MainShellViewModel ViewModel,
                             IDocumentSessionService Session)
{
    internal static Shell Create()
    {
        var session = new DocumentSessionService();
        var profile = new ProfileService(new NoPreferences(), applyAffordanceDefaults: false);
        var viewModel = new MainShellViewModel(
            new NoFiles(), new NoDialogs(), session, profile, new PromptViewModelFactory(profile));
        return new Shell(new MainShellView { DataContext = viewModel }, viewModel, session);
    }

    private sealed class NoPreferences : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }
}
