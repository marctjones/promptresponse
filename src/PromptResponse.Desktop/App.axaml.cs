using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
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
                _logger.LogDebug("Creating MainWindow...");

                var viewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
                _logger.LogDebug("MainWindowViewModel created: {Type}", viewModel.GetType().Name);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel
                };

                _logger.LogInformation("MainWindow created and assigned");
                _logger.LogDebug("  Window Title: {Title}", desktop.MainWindow.Title);
                _logger.LogDebug("  Window Size: {Width}x{Height}", desktop.MainWindow.Width, desktop.MainWindow.Height);

                // Hook up lifetime events
                desktop.ShutdownRequested += (s, e) =>
                {
                    _logger?.LogInformation("Application shutdown requested");
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

        Console.WriteLine("[App]   - Registering DataTypeValidator");
        services.AddSingleton<DataTypeValidator>();

        // Desktop services
        Console.WriteLine("[App]   - Registering IFileService -> FileService");
        services.AddSingleton<IFileService, FileService>();

        // ViewModels
        Console.WriteLine("[App]   - Registering MainWindowViewModel");
        services.AddTransient<MainWindowViewModel>();

        Console.WriteLine("[App]   - Registering FormFillingViewModel");
        services.AddTransient<FormFillingViewModel>();

        Console.WriteLine("[App] Service configuration complete");
    }
}
