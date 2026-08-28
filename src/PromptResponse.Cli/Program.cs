using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Cli.Api;
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
                "fill" => await serviceProvider.GetRequiredService<FillCommand>().ExecuteAsync(commandArgs),
                "stats" => await serviceProvider.GetRequiredService<StatsCommand>().ExecuteAsync(commandArgs),
                "review" => await serviceProvider.GetRequiredService<ReviewCommand>().ExecuteAsync(commandArgs),
                "eval" => await serviceProvider.GetRequiredService<EvalCommand>().ExecuteAsync(commandArgs),
                "diff" => await serviceProvider.GetRequiredService<DiffCommand>().ExecuteAsync(commandArgs),
                "export" => await serviceProvider.GetRequiredService<ExportCommand>().ExecuteAsync(commandArgs),
                "import" => await serviceProvider.GetRequiredService<ImportCommand>().ExecuteAsync(commandArgs),
                "keygen" => await serviceProvider.GetRequiredService<KeygenCommand>().ExecuteAsync(commandArgs),
                "sign" => await serviceProvider.GetRequiredService<SignCommand>().ExecuteAsync(commandArgs),
                "verify" => await serviceProvider.GetRequiredService<VerifyCommand>().ExecuteAsync(commandArgs),
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

        // API services
        services.AddSingleton<FormFillingApi>();

        // Commands
        services.AddTransient<ValidateCommand>();
        services.AddTransient<InfoCommand>();
        services.AddTransient<NewCommand>();
        services.AddTransient<FillCommand>();
        services.AddTransient<StatsCommand>();
        services.AddTransient<ReviewCommand>();
        services.AddTransient<EvalCommand>();
        services.AddTransient<DiffCommand>();
        services.AddTransient<ExportCommand>();
        services.AddTransient<ImportCommand>();
        services.AddTransient<KeygenCommand>();
        services.AddTransient<SignCommand>();
        services.AddTransient<VerifyCommand>();
    }

    private static int ShowHelp()
    {
        Console.WriteLine("APR - Adaptive Prompt Response CLI");
        Console.WriteLine();
        Console.WriteLine("Usage: apr <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  validate <file>                Validate an APR file");
        Console.WriteLine("  info <file>                    Show information about an APR file");
        Console.WriteLine("  new <file>                     Create a new template");
        Console.WriteLine("  fill <template> [options]      Fill out a form (interactive or programmatic)");
        Console.WriteLine("  stats <file> [--json]          Show detailed statistics");
        Console.WriteLine("  eval <file> [--json]           Evaluate the form's own expressions");
        Console.WriteLine("  review <file> [--json] [--strict]");
        Console.WriteLine("                                 Report whether a submission can be");
        Console.WriteLine("                                 processed automatically. Exit 0 = yes,");
        Console.WriteLine("                                 2 = route to a person, 1 = unreadable");
        Console.WriteLine("  diff <file1> <file2>           Compare two APR files");
        Console.WriteLine("  export <file> [options]        Export responses to various formats");
        Console.WriteLine("  import <file.pdf> [options]    Import a fillable PDF into an APR template");
        Console.WriteLine("  keygen [options]               Generate a self-signed signing certificate (.pfx)");
        Console.WriteLine("  sign <file> [options]          Sign a document (publisher or filler) with an X.509 cert");
        Console.WriteLine("  verify <file> [options]        Verify signatures and report trust");
        Console.WriteLine("  help                           Show this help message");
        Console.WriteLine("  version                        Show version information");
        Console.WriteLine();
        Console.WriteLine("Fill Options:");
        Console.WriteLine("  --json-file=<file>             Fill from JSON file");
        Console.WriteLine("  --json=<json-string>           Fill from JSON string");
        Console.WriteLine("  --non-interactive              Fill from command-line args");
        Console.WriteLine("  --set-{promptId}=<value>       Set response (non-interactive mode)");
        Console.WriteLine("  --output=<file>                Output file (default: template.aprf)");
        Console.WriteLine("  --filled-by=<name>             Name of person filling form");
        Console.WriteLine("  --validate                     Validate after filling");
        Console.WriteLine();
        Console.WriteLine("Export Options:");
        Console.WriteLine("  --format=<csv|json|txt>        Output format (default: csv)");
        Console.WriteLine("  --output=<file>                Output file (default: stdout)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  apr validate form.apr");
        Console.WriteLine("  apr info employment-app.apr");
        Console.WriteLine("  apr new my-template.apr");
        Console.WriteLine("  apr fill template.aprt");
        Console.WriteLine("  apr fill template.aprt --json-file=responses.json");
        Console.WriteLine("  apr fill template.aprt --non-interactive --set-name=\"John Doe\"");
        Console.WriteLine("  apr stats form.apr --json");
        Console.WriteLine("  apr diff original.apr modified.apr");
        Console.WriteLine("  apr export form.apr --format=csv --output=responses.csv");
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
