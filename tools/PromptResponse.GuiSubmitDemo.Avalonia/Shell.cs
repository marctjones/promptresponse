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
/// models, the real "Submit via HTTPS" confirmation flow, and the real
/// <see cref="HttpsSubmissionService"/> wire behavior (PUT, no redirect, no retry) — is the
/// shipped code. Only the file picker, the dialog surfaces (this driver has no screen to put
/// them on, so it prints and auto-answers instead — see <see cref="AutoConfirmDialogs"/>),
/// and the HTTPS handler's certificate validation are supplied here. The last one exists
/// only to trust this demo's own throwaway MinIO certificate by its exact bytes — nothing
/// else, and nothing touches the OS trust store.
/// </remarks>
internal sealed record Shell(MainShellView View, MainShellViewModel ViewModel, IDocumentSessionService Session)
{
    internal static Shell Create(HttpMessageHandler httpsHandler)
    {
        var session = new DocumentSessionService();
        var profile = new ProfileService(new NoPreferences(), applyAffordanceDefaults: false);
        var viewModel = new MainShellViewModel(
            new NoFiles(), new AutoConfirmDialogs(), session, profile, new PromptViewModelFactory(profile),
            httpsSubmission: new HttpsSubmissionService(httpsHandler));
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
