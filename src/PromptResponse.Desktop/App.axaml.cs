using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
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

                // Restore the user's persisted capability profile if one exists. On a
                // fresh install (settings.Profile == null) ProfileService's
                // ApplyOsDefaults already enabled the visual affordances for sighted
                // users — that branch is what gives the auto-formatters a working
                // first-launch experience without forcing the user to dig into the
                // Display Preferences panel.
                var profileService = serviceProvider.GetRequiredService<IProfileService>();
                if (settingsService.Settings.Profile != null)
                {
                    profileService.Restore(settingsService.Settings.Profile);
                    _logger?.LogInformation("Restored capability profile from settings: scheme={Scheme}, flags={Flags}",
                        settingsService.Settings.Profile.ColorScheme,
                        string.Join(",", settingsService.Settings.Profile.ActiveFlags));
                }
                else
                {
                    _logger?.LogInformation("First launch — keeping ProfileService's OS-defaults composition (sighted user → Excellent Vision affordances on)");
                }

                // Persist the profile on every change so the next launch gets the
                // same setup — checkbox toggles, preset clicks, color-scheme swaps.
                profileService.ProfileChanged += (_, _) =>
                {
                    settingsService.Settings.Profile = profileService.Snapshot();
                };

                var shellVm = serviceProvider.GetRequiredService<MainShellViewModel>();

                // Pin the Application-level theme variant to the active profile so
                // every Window (main + dialogs) and every Control template uses
                // FluentTheme system colors that contrast with our profile palette.
                // Without this, RadioButton/CheckBox/MenuItem render invisible.
                RequestedThemeVariant = shellVm.ActiveThemeVariant;
                shellVm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainShellViewModel.ActiveThemeVariant))
                    {
                        RequestedThemeVariant = shellVm.ActiveThemeVariant;
                    }
                };

                var window = new MainWindow { DataContext = shellVm };
                desktop.MainWindow = window;

                ApplyWindowSettings(window, settingsService);
                HookShutdown(desktop, window, settingsService);

                // Honour command-line "--open <path>" by loading the file once the
                // window is shown. Errors are logged but don't prevent the empty
                // state from showing, so the user can still use the app.
                if (Program.StartupOptions?.FilePath is { } startupFile)
                {
                    window.Opened += async (_, _) =>
                    {
                        try
                        {
                            await shellVm.OpenFromPath(startupFile);
                            _logger?.LogInformation("Auto-opened startup file: {File}", startupFile);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to auto-open {File}", startupFile);
                        }
                    };
                }
                if (Program.StartupOptions?.WizardMode == true)
                {
                    // Toggle wizard mode on at startup. Honors --wizard as a
                    // testing/demo flag without persisting; the user's saved
                    // profile state would otherwise win.
                    window.Opened += (_, _) =>
                    {
                        if (!shellVm.IsWizardMode) shellVm.ToggleWizardModeCommand.Execute(null);
                        _logger?.LogInformation("Started in wizard mode (--wizard flag)");
                    };
                }

                _logger?.LogInformation("MainWindow shown with new MainShellViewModel.");
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
        services.AddSingleton<DataTypeValidator>();

        // Desktop infrastructure
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRecentFilesService>(sp =>
            new RecentFilesService(sp.GetRequiredService<ISettingsService>()));
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IDocumentSessionService, DocumentSessionService>();

        // Rendering profile system
        services.AddSingleton<IOsAccessibilityProbe, OsAccessibilityProbe>();
        services.AddSingleton<IProfileService, ProfileService>();

        // View-models
        services.AddSingleton<PromptViewModelFactory>();
        services.AddTransient<MainShellViewModel>();
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
