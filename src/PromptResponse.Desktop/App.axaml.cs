using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using IValidator = PromptResponse.Core.Validation.IValidator<PromptResponse.Core.Models.AprDocument>;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;
using System;

namespace PromptResponse.Desktop;

public partial class App : Application
{
    private ILogger<App>? _logger;
    public static IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            ServiceProvider = serviceProvider;

            _logger = serviceProvider.GetRequiredService<ILogger<App>>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
                settingsService.Load();

                var shellVm = serviceProvider.GetRequiredService<MainShellViewModel>();

                var window = new MainWindow { DataContext = shellVm };
                desktop.MainWindow = window;

                ApplyWindowSettings(window, settingsService);
                HookShutdown(desktop, window, settingsService);

                _logger.LogInformation("MainWindow shown with new MainShellViewModel.");
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] FATAL: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core
        services.AddSingleton<IAprSerializer, AprJsonSerializer>();
        services.AddSingleton<DocumentValidator>();
        services.AddSingleton<IValidator>(sp => sp.GetRequiredService<DocumentValidator>());
        services.AddSingleton<DataTypeValidator>();

        // Desktop infra
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPlatformFeatures, PlatformFeatures>();

        // Phase 4 — focused services
        services.AddSingleton<IDocumentSessionService, DocumentSessionService>();

        // Phase 2 — rendering profile system
        services.AddSingleton<IOsAccessibilityProbe, OsAccessibilityProbe>();
        services.AddSingleton<IProfileService, ProfileService>();

        // Phase 3 — polymorphic prompt VM factory
        services.AddSingleton<PromptViewModelFactory>();

        // Phase 4d — thin shell composing everything
        services.AddTransient<MainShellViewModel>();

        // Display Preferences VM (Phase 2)
        services.AddTransient<DisplayPreferencesViewModel>();
    }

    private static void ApplyWindowSettings(Avalonia.Controls.Window window, ISettingsService settingsService)
    {
        var ws = settingsService.Settings.Window;
        window.Width = ws.Width;
        window.Height = ws.Height;

        if (ws.X.HasValue && ws.Y.HasValue)
        {
            var screens = window.Screens;
            var allScreens = screens?.All ?? Array.Empty<Avalonia.Platform.Screen>();

            bool isOnScreen = false;
            foreach (var screen in allScreens)
            {
                var bounds = screen.WorkingArea;
                if (ws.X >= bounds.X && ws.X < bounds.X + bounds.Width &&
                    ws.Y >= bounds.Y && ws.Y < bounds.Y + bounds.Height)
                {
                    isOnScreen = true;
                    break;
                }
            }

            if (isOnScreen)
            {
                window.Position = new PixelPoint((int)ws.X.Value, (int)ws.Y.Value);
            }
        }

        if (ws.IsMaximized)
        {
            window.WindowState = Avalonia.Controls.WindowState.Maximized;
        }
    }

    private void HookShutdown(
        IClassicDesktopStyleApplicationLifetime desktop,
        Avalonia.Controls.Window window,
        ISettingsService settingsService)
    {
        desktop.ShutdownRequested += (_, _) =>
        {
            var ws = settingsService.Settings.Window;
            ws.IsMaximized = window.WindowState == Avalonia.Controls.WindowState.Maximized;
            if (!ws.IsMaximized)
            {
                ws.Width = window.Width;
                ws.Height = window.Height;
                ws.X = window.Position.X;
                ws.Y = window.Position.Y;
            }
            settingsService.Save();
            _logger?.LogInformation("Settings saved on shutdown.");
        };
    }
}
