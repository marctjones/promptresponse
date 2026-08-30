using PromptResponse.Cli.Api;
using PromptResponse.Cli.Commands.Fill;
using PromptResponse.Core.Models;
using Microsoft.Extensions.Logging;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Command to fill out APR forms interactively or programmatically.
/// </summary>
public class FillCommand : ICommand
{
    private readonly FormFillingApi _api;
    private readonly ILogger<FillCommand> _logger;

    public FillCommand(
        FormFillingApi api,
        ILogger<FillCommand> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 1;
        }

        try
        {
            var templatePath = args[0];
            var options = FillCommandOptions.Parse(args.Skip(1));

            // Load template
            var template = await _api.LoadTemplateAsync(templatePath);

            FillCommandPresentation.WriteTemplate(template);

            AprDocument filledForm;

            if (options.JsonFilePath is not null)
            {
                // Programmatic mode: Fill from JSON file
                filledForm = await FillFromJsonFileAsync(template, options.JsonFilePath, options);
            }
            else if (options.Json is not null)
            {
                // Programmatic mode: Fill from JSON string
                filledForm = FillFromJsonString(template, options.Json, options);
            }
            else if (options.IsNonInteractive)
            {
                // Non-interactive mode with command-line args
                filledForm = FillFromCommandLine(template, options);
            }
            else
            {
                // Interactive mode (default)
                filledForm = await FillInteractiveAsync(template, options);
            }

            // Output file
            var outputPath = options.OutputPath ??
                             Path.ChangeExtension(templatePath, ".aprf");

            await _api.SaveFilledFormAsync(filledForm, outputPath);

            // Show completion stats
            var completion = _api.GetCompletionPercentage(filledForm);
            FillCommandPresentation.WriteCompletion(completion, outputPath);

            // Validate if requested
            if (options.Validate)
            {
                var validation = _api.ValidateFilledForm(filledForm);
                FillCommandPresentation.WriteValidation(validation);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            _logger.LogError(ex, "Fill command failed");
            return 1;
        }
    }

    private async Task<AprDocument> FillInteractiveAsync(
        AprDocument template,
        FillCommandOptions options)
    {
        var input = InteractiveResponseCollector.Collect(template, options.FilledBy);
        return _api.FillForm(template, input.Responses, input.FilledBy);
    }

    private async Task<AprDocument> FillFromJsonFileAsync(
        AprDocument template,
        string jsonFilePath,
        FillCommandOptions options)
    {
        Console.WriteLine($"Loading responses from: {jsonFilePath}");

        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");
        }

        var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
        var filledBy = options.FilledBy;

        return _api.FillFormFromJson(template, jsonContent, filledBy);
    }

    private AprDocument FillFromJsonString(
        AprDocument template,
        string json,
        FillCommandOptions options)
    {
        Console.WriteLine("Filling form from JSON string");

        var filledBy = options.FilledBy;
        return _api.FillFormFromJson(template, json, filledBy);
    }

    private AprDocument FillFromCommandLine(
        AprDocument template,
        FillCommandOptions options)
    {
        Console.WriteLine("Filling form from command-line arguments");

        var responses = CommandLineResponseCollector.Collect(options.Values);

        if (responses.Count == 0)
        {
            Console.WriteLine("Warning: No responses provided. Use --set-{promptId}=value");
        }

        var filledBy = options.FilledBy;
        return _api.FillForm(template, responses, filledBy);
    }


    private void ShowHelp()
    {
        Console.WriteLine("Usage: apr fill <template> [options]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  Interactive (default)          Walk through form step-by-step");
        Console.WriteLine("  --json-file=<file>             Fill from JSON file");
        Console.WriteLine("  --json=<json-string>           Fill from JSON string");
        Console.WriteLine("  --non-interactive              Fill from command-line args");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --output=<file>                Output file (default: template.aprf)");
        Console.WriteLine("  --filled-by=<name>             Name of person filling form");
        Console.WriteLine("  --validate                     Validate after filling");
        Console.WriteLine("  --set-{promptId}=<value>       Set response (non-interactive mode)");
        Console.WriteLine();
        Console.WriteLine("JSON Format:");
        Console.WriteLine("  {");
        Console.WriteLine("    \"prompt_001\": \"John Doe\",");
        Console.WriteLine("    \"prompt_002\": \"john@example.com\",");
        Console.WriteLine("    \"prompt_003\": \"2025-11-15\"");
        Console.WriteLine("  }");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Interactive mode");
        Console.WriteLine("  apr fill template.aprt");
        Console.WriteLine();
        Console.WriteLine("  # From JSON file");
        Console.WriteLine("  apr fill template.aprt --json-file=responses.json --output=filled.aprf");
        Console.WriteLine();
        Console.WriteLine("  # From command line");
        Console.WriteLine("  apr fill template.aprt --non-interactive \\");
        Console.WriteLine("    --set-prompt_001=\"John Doe\" \\");
        Console.WriteLine("    --set-prompt_002=\"john@example.com\" \\");
        Console.WriteLine("    --filled-by=\"John Doe\"");
        Console.WriteLine();
        Console.WriteLine("  # From JSON string");
        Console.WriteLine("  apr fill template.aprt --json='{\"prompt_001\":\"Test\"}' --validate");
        Console.WriteLine();
    }
}
