using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

namespace PromptResponse.Cli;

/// <summary>
/// APR CLI - Command-line tool for working with Adaptive Prompt Response files.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Parse command
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "validate" => await serviceProvider.GetRequiredService<ValidateCommand>().ExecuteAsync(commandArgs),
                "info" => await serviceProvider.GetRequiredService<InfoCommand>().ExecuteAsync(commandArgs),
                "new" => await serviceProvider.GetRequiredService<NewCommand>().ExecuteAsync(commandArgs),
                "help" or "--help" or "-h" => ShowHelp(),
                "version" or "--version" or "-v" => ShowVersion(),
                _ => ShowUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // Core services
        services.AddSingleton<IAprSerializer, AprJsonSerializer>();
        services.AddSingleton<DocumentValidator>();
        services.AddSingleton<DataTypeValidator>();

        // Commands
        services.AddTransient<ValidateCommand>();
        services.AddTransient<InfoCommand>();
        services.AddTransient<NewCommand>();
    }

    private static int ShowHelp()
    {
        Console.WriteLine("APR - Adaptive Prompt Response CLI");
        Console.WriteLine();
        Console.WriteLine("Usage: apr <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  validate <file>      Validate an APR file");
        Console.WriteLine("  info <file>          Show information about an APR file");
        Console.WriteLine("  new <file>           Create a new template");
        Console.WriteLine("  help                 Show this help message");
        Console.WriteLine("  version              Show version information");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  apr validate form.apr");
        Console.WriteLine("  apr info employment-app.apr");
        Console.WriteLine("  apr new my-template.apr");
        Console.WriteLine();
        return 0;
    }

    private static int ShowVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version;
        Console.WriteLine($"APR CLI version {version}");
        Console.WriteLine("APR Format version 1.0");
        return 0;
    }

    private static int ShowUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'apr help' for usage information.");
        return 1;
    }
}
