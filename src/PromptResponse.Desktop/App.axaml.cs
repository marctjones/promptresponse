using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using IValidator = PromptResponse.Core.Validation.IValidator<PromptResponse.Core.Models.AprDocument>;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.Views;
using System;

namespace PromptResponse.Desktop;

public partial class App : Application
{
    private ILogger<App>? _logger;
    public static IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        Console.WriteLine("[App] Initialize() called - Loading XAML resources");
        AvaloniaXamlLoader.Load(this);
        Console.WriteLine("[App] XAML resources loaded successfully");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[App] OnFrameworkInitializationCompleted() called");
        Console.WriteLine("[App] Lifetime type: {0}", ApplicationLifetime?.GetType().Name ?? "null");

        try
        {
            // Setup dependency injection
            Console.WriteLine("[App] Setting up dependency injection container...");
            var services = new ServiceCollection();

            // Add logging first
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            ConfigureServices(services);

            Console.WriteLine("[App] Building service provider...");
            var serviceProvider = services.BuildServiceProvider();
            ServiceProvider = serviceProvider; // Store for view access

            _logger = serviceProvider.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Service provider built successfully");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _logger.LogInformation("Initializing ClassicDesktop lifetime");

                // Load settings
                var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
                _logger.LogDebug("Loading application settings...");
                settingsService.Load();
                _logger.LogInformation("Settings loaded successfully");

                _logger.LogDebug("Creating MainWindow...");

                var viewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
                _logger.LogDebug("MainWindowViewModel created: {Type}", viewModel.GetType().Name);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel
                };

                // Apply saved window settings
                var windowSettings = settingsService.Settings.Window;
                desktop.MainWindow.Width = windowSettings.Width;
                desktop.MainWindow.Height = windowSettings.Height;

                // Only restore window position if it's within screen bounds
                if (windowSettings.X.HasValue && windowSettings.Y.HasValue)
                {
                    var screens = desktop.MainWindow.Screens;
                    var allScreens = screens?.All ?? Array.Empty<Avalonia.Platform.Screen>();

                    // Check if the saved position is within any screen's bounds
                    bool isOnScreen = false;
                    foreach (var screen in allScreens)
                    {
                        var bounds = screen.WorkingArea;
                        if (windowSettings.X >= bounds.X &&
                            windowSettings.X < bounds.X + bounds.Width &&
                            windowSettings.Y >= bounds.Y &&
                            windowSettings.Y < bounds.Y + bounds.Height)
                        {
                            isOnScreen = true;
                            break;
                        }
                    }

                    if (isOnScreen)
                    {
                        desktop.MainWindow.Position = new Avalonia.PixelPoint(
                            (int)windowSettings.X.Value,
                            (int)windowSettings.Y.Value);
                        _logger.LogDebug("Window position restored to: {X},{Y}", windowSettings.X, windowSettings.Y);
                    }
                    else
                    {
                        _logger.LogWarning("Saved window position ({X},{Y}) is off-screen, using default",
                            windowSettings.X, windowSettings.Y);
                        // Let Avalonia position the window at default location (centered)
                    }
                }

                if (windowSettings.IsMaximized)
                {
                    desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Maximized;
                }

                _logger.LogInformation("MainWindow created and assigned");
                _logger.LogDebug("  Window Title: {Title}", desktop.MainWindow.Title);
                _logger.LogDebug("  Window Size: {Width}x{Height}", desktop.MainWindow.Width, desktop.MainWindow.Height);
                _logger.LogDebug("  Window Position: {X},{Y}", windowSettings.X, windowSettings.Y);

                // Apply saved theme
                viewModel.ApplyThemeFromSettings();

                // Check for startup file from command line
                if (Program.StartupOptions?.FilePath != null)
                {
                    _logger.LogInformation("Opening startup file: {File}", Program.StartupOptions.FilePath);
                    desktop.MainWindow.Opened += async (s, e) =>
                    {
                        await viewModel.OpenFileOnStartup(
                            Program.StartupOptions.FilePath,
                            Program.StartupOptions.EditMode);
                    };
                }

                // Hook up lifetime events
                desktop.ShutdownRequested += (s, e) =>
                {
                    _logger?.LogInformation("Application shutdown requested");

                    // Save window position and size
                    var window = desktop.MainWindow;
                    if (window != null)
                    {
                        var settings = settingsService.Settings.Window;
                        settings.IsMaximized = window.WindowState == Avalonia.Controls.WindowState.Maximized;

                        if (!settings.IsMaximized)
                        {
                            settings.Width = window.Width;
                            settings.Height = window.Height;
                            settings.X = window.Position.X;
                            settings.Y = window.Position.Y;
                        }
                    }

                    // Save settings
                    settingsService.Save();
                    _logger?.LogInformation("Settings saved on shutdown");
                };

                desktop.Exit += (s, e) =>
                {
                    _logger?.LogInformation("Application exiting with code: {ExitCode}", e.ApplicationExitCode);
                };
            }
            else
            {
                _logger.LogWarning("ApplicationLifetime is not ClassicDesktop type!");
            }

            base.OnFrameworkInitializationCompleted();
            _logger.LogInformation("Framework initialization completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] FATAL ERROR during initialization: {ex.Message}");
            Console.WriteLine($"[App] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        Console.WriteLine("[App] Configuring services...");

        // Core services
        Console.WriteLine("[App]   - Registering IAprSerializer -> AprJsonSerializer");
        services.AddSingleton<IAprSerializer, AprJsonSerializer>();

        Console.WriteLine("[App]   - Registering DocumentValidator");
        services.AddSingleton<DocumentValidator>();
        services.AddSingleton<IValidator>(sp => sp.GetRequiredService<DocumentValidator>());

        Console.WriteLine("[App]   - Registering DataTypeValidator");
        services.AddSingleton<DataTypeValidator>();

        // Desktop services
        Console.WriteLine("[App]   - Registering IFileService -> FileService");
        services.AddSingleton<IFileService, FileService>();

        Console.WriteLine("[App]   - Registering ISettingsService -> SettingsService");
        services.AddSingleton<ISettingsService, SettingsService>();

        Console.WriteLine("[App]   - Registering IDialogService -> DialogService");
        services.AddSingleton<IDialogService, DialogService>();

        Console.WriteLine("[App]   - Registering IPlatformFeatures -> PlatformFeatures");
        services.AddSingleton<IPlatformFeatures, PlatformFeatures>();

        // ViewModels
        Console.WriteLine("[App]   - Registering MainWindowViewModel");
        services.AddTransient<MainWindowViewModel>();

        Console.WriteLine("[App]   - Registering FormFillingViewModel");
        services.AddTransient<FormFillingViewModel>();

        Console.WriteLine("[App] Service configuration complete");
    }
}
