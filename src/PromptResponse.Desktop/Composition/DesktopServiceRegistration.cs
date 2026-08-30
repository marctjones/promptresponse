using Microsoft.Extensions.DependencyInjection;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Composition;

/// <summary>Registers the desktop application's production dependency graph.</summary>
internal static class DesktopServiceRegistration
{
    internal static IServiceCollection AddPromptResponseDesktop(this IServiceCollection services)
    {
        services.AddSingleton<IAprSerializer, AprJsonSerializer>(); services.AddSingleton<DocumentValidator>(); services.AddSingleton<DataTypeValidator>();
        services.AddSingleton<IFileService, FileService>(); services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRecentFilesService>(provider => new RecentFilesService(provider.GetRequiredService<ISettingsService>()));
        services.AddSingleton<ITemplateCatalogService>(provider => new TemplateCatalogService(provider.GetRequiredService<IAprSerializer>()));
        services.AddSingleton<IDialogService, DialogService>(); services.AddSingleton<IMailHandoffService, MailHandoffService>(); services.AddSingleton<IHttpsSubmissionService, HttpsSubmissionService>(); services.AddSingleton<IDocumentSessionService, DocumentSessionService>();
        services.AddSingleton<IOsAccessibilityProbe, OsAccessibilityProbe>(); services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<PromptViewModelFactory>(); services.AddTransient<MainShellViewModel>(); services.AddTransient<DisplayPreferencesViewModel>();
        return services;
    }
}
