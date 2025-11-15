using Avalonia;
using Microsoft.Extensions.Logging;
using System;

namespace PromptResponse.Desktop;

/// <summary>
/// Startup options parsed from command line.
/// </summary>
public class StartupOptions
{
    public string? FilePath { get; set; }
    public bool EditMode { get; set; }
}

class Program
{
    private static ILogger<Program>? _logger;
    public static StartupOptions? StartupOptions { get; private set; }

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

        // Parse command line arguments
        StartupOptions = ParseArguments(args);
        if (StartupOptions.FilePath != null)
        {
            _logger.LogInformation("Startup file: {File} (Edit mode: {EditMode})",
                StartupOptions.FilePath, StartupOptions.EditMode);
        }

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

    /// <summary>
    /// Parses command line arguments.
    /// </summary>
    /// <remarks>
    /// Supported options:
    /// --open &lt;file&gt;   - Open file for filling
    /// --edit &lt;file&gt;   - Open file for editing (template mode)
    /// &lt;file&gt;          - Open file for filling (same as --open)
    /// </remarks>
    private static StartupOptions ParseArguments(string[] args)
    {
        var options = new StartupOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--open" && i + 1 < args.Length)
            {
                options.FilePath = args[++i];
                options.EditMode = false;
                _logger?.LogDebug("Parsed --open argument: {File}", options.FilePath);
            }
            else if (arg == "--edit" && i + 1 < args.Length)
            {
                options.FilePath = args[++i];
                options.EditMode = true;
                _logger?.LogDebug("Parsed --edit argument: {File}", options.FilePath);
            }
            else if (!arg.StartsWith("--") && File.Exists(arg))
            {
                // Treat standalone file path as --open
                options.FilePath = arg;
                options.EditMode = false;
                _logger?.LogDebug("Parsed file path argument: {File}", options.FilePath);
            }
            else if (arg == "--help" || arg == "-h")
            {
                ShowHelp();
                Environment.Exit(0);
            }
        }

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("PromptResponse Desktop Application");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  PromptResponse.Desktop [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --open <file>    Open APR file for filling out");
        Console.WriteLine("  --edit <file>    Open APR template for editing");
        Console.WriteLine("  <file>           Open APR file for filling out (same as --open)");
        Console.WriteLine("  --help, -h       Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  PromptResponse.Desktop --open form.aprt");
        Console.WriteLine("  PromptResponse.Desktop --edit template.aprt");
        Console.WriteLine("  PromptResponse.Desktop myform.apr");
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
