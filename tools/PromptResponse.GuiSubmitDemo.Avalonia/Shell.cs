using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.GuiSubmitDemo.Avalonia;

/// <summary>The real desktop shell, with only the platform edges and the HTTPS transport
/// replaced.</summary>
/// <remarks>
/// Everything that decides what a person faces and what gets sent — the views, the view
/// models, the real "Submit via HTTPS" confirmation flow, the real
/// <see cref="HttpsSubmissionService"/> wire behavior (PUT, no redirect, no retry), and the
/// real <see cref="FileService"/> that a "Save" action would use — is the shipped code. Only
/// the dialog surfaces (this driver has no screen to put them on, so it prints and
/// auto-answers instead — see <see cref="AutoConfirmDialogs"/>) and the HTTPS handler's
/// certificate validation are supplied here. The real file service is safe to use as-is: its
/// load/save-by-path members do plain file I/O with no picker or window dependency, and the
/// picker-based members this driver never calls are the only ones that would need one.
/// </remarks>
internal sealed record Shell(
    MainShellView View, MainShellViewModel ViewModel, IDocumentSessionService Session, IFileService Files)
{
    internal static Shell Create(HttpMessageHandler httpsHandler)
    {
        var session = new DocumentSessionService();
        var files = new FileService();
        var profile = new ProfileService(new NoPreferences(), applyAffordanceDefaults: false);
        var viewModel = new MainShellViewModel(
            files, new AutoConfirmDialogs(), session, profile, new PromptViewModelFactory(profile),
            httpsSubmission: new HttpsSubmissionService(httpsHandler));
        return new Shell(new MainShellView { DataContext = viewModel }, viewModel, session, files);
    }

    private sealed class NoPreferences : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }
}
