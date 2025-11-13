using Avalonia;
using Microsoft.Extensions.Logging;
using System;

namespace PromptResponse.Desktop;

class Program
{
    private static ILogger<Program>? _logger;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Setup logging first
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug);
        });

        _logger = loggerFactory.CreateLogger<Program>();

        _logger.LogInformation("=".PadRight(80, '='));
        _logger.LogInformation("PromptResponse Desktop Application Starting");
        _logger.LogInformation("=".PadRight(80, '='));
        _logger.LogInformation("Platform: {Platform}", Environment.OSVersion);
        _logger.LogInformation("Runtime: .NET {Version}", Environment.Version);
        _logger.LogInformation("Working Directory: {Directory}", Environment.CurrentDirectory);
        _logger.LogInformation("Command Line Args: {Args}", string.Join(" ", args));
        _logger.LogInformation("");

        try
        {
            _logger.LogDebug("Building Avalonia application...");
            var app = BuildAvaloniaApp();

            _logger.LogDebug("Starting application with ClassicDesktop lifetime...");
            app.StartWithClassicDesktopLifetime(args);

            _logger.LogInformation("Application shutdown complete");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error during application execution");
            Console.WriteLine($"\n\nFATAL ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        _logger?.LogDebug("Configuring Avalonia AppBuilder");
        _logger?.LogDebug("  - Platform detection enabled");
        _logger?.LogDebug("  - Inter font family loaded");
        _logger?.LogDebug("  - Trace logging enabled");

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
