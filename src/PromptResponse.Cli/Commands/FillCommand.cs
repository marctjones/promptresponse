using PromptResponse.Cli.Api;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Command to fill out APR forms interactively or programmatically.
/// </summary>
public class FillCommand : ICommand
{
    private readonly FormFillingApi _api;
    private readonly IAprSerializer _serializer;
    private readonly ILogger<FillCommand> _logger;

    public FillCommand(
        FormFillingApi api,
        IAprSerializer serializer,
        ILogger<FillCommand> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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
            var options = ParseOptions(args.Skip(1).ToArray());

            // Load template
            var template = await _api.LoadTemplateAsync(templatePath);

            Console.WriteLine($"Template: {template.Metadata?.Title ?? "Untitled"}");
            Console.WriteLine($"Template ID: {template.Metadata?.TemplateId ?? "N/A"}");
            Console.WriteLine();

            AprDocument filledForm;

            if (options.ContainsKey("--json-file"))
            {
                // Programmatic mode: Fill from JSON file
                filledForm = await FillFromJsonFileAsync(template, options["--json-file"], options);
            }
            else if (options.ContainsKey("--json"))
            {
                // Programmatic mode: Fill from JSON string
                filledForm = FillFromJsonString(template, options["--json"], options);
            }
            else if (options.ContainsKey("--non-interactive"))
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
            var outputPath = options.GetValueOrDefault("--output") ??
                             Path.ChangeExtension(templatePath, ".aprf");

            await _api.SaveFilledFormAsync(filledForm, outputPath);

            // Show completion stats
            var completion = _api.GetCompletionPercentage(filledForm);
            Console.WriteLine();
            Console.WriteLine($"Form filling complete!");
            Console.WriteLine($"Completion: {completion:F1}%");
            Console.WriteLine($"Saved to: {outputPath}");

            // Validate if requested
            if (options.ContainsKey("--validate"))
            {
                Console.WriteLine();
                Console.WriteLine("Validating...");
                var validation = _api.ValidateFilledForm(filledForm);
                if (validation.IsValid)
                {
                    Console.WriteLine("✓ Validation passed");
                }
                else
                {
                    Console.WriteLine("⚠ Validation warnings:");
                    foreach (var error in validation.Errors)
                    {
                        Console.WriteLine($"  - {error.Message}");
                    }
                }
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
        Dictionary<string, string> options)
    {
        Console.WriteLine("=== Interactive Form Filling ===");
        Console.WriteLine("(Press Enter to skip a field, Ctrl+C to cancel)");
        Console.WriteLine();

        var responses = new Dictionary<string, string>();
        var filledBy = options.GetValueOrDefault("--filled-by") ??
                       PromptForValue("Filled by", Environment.UserName);

        foreach (var section in template.Sections)
        {
            Console.WriteLine($"\n--- {section.Title} ---");
            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                Console.WriteLine($"    {section.Description}");
            }
            Console.WriteLine();

            // Section-level prompts
            foreach (var prompt in section.Prompts)
            {
                var response = PromptForResponse(prompt);
                if (!string.IsNullOrEmpty(response))
                {
                    responses[prompt.Id] = response;
                }
            }

            // Subsection prompts
            foreach (var subsection in section.Subsections)
            {
                Console.WriteLine($"\n  -- {subsection.Title} --");
                if (!string.IsNullOrWhiteSpace(subsection.Description))
                {
                    Console.WriteLine($"     {subsection.Description}");
                }
                Console.WriteLine();

                foreach (var prompt in subsection.Prompts)
                {
                    var response = PromptForResponse(prompt);
                    if (!string.IsNullOrEmpty(response))
                    {
                        responses[prompt.Id] = response;
                    }
                }
            }
        }

        return _api.FillForm(template, responses, filledBy);
    }

    private async Task<AprDocument> FillFromJsonFileAsync(
        AprDocument template,
        string jsonFilePath,
        Dictionary<string, string> options)
    {
        Console.WriteLine($"Loading responses from: {jsonFilePath}");

        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");
        }

        var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
        var filledBy = options.GetValueOrDefault("--filled-by");

        return _api.FillFormFromJson(template, jsonContent, filledBy);
    }

    private AprDocument FillFromJsonString(
        AprDocument template,
        string json,
        Dictionary<string, string> options)
    {
        Console.WriteLine("Filling form from JSON string");

        var filledBy = options.GetValueOrDefault("--filled-by");
        return _api.FillFormFromJson(template, json, filledBy);
    }

    private AprDocument FillFromCommandLine(
        AprDocument template,
        Dictionary<string, string> options)
    {
        Console.WriteLine("Filling form from command-line arguments");

        var responses = new Dictionary<string, string>();

        // Extract all --set-{promptId}=value options
        foreach (var (key, value) in options)
        {
            if (key.StartsWith("--set-"))
            {
                var promptId = key.Substring(6); // Remove "--set-" prefix
                responses[promptId] = value;
            }
        }

        if (responses.Count == 0)
        {
            Console.WriteLine("Warning: No responses provided. Use --set-{promptId}=value");
        }

        var filledBy = options.GetValueOrDefault("--filled-by");
        return _api.FillForm(template, responses, filledBy);
    }

    private string PromptForResponse(Prompt prompt)
    {
        Console.Write($"{prompt.Label}: ");

        // Show placeholder if available
        var placeholder = prompt.Hints?.Placeholder;
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{placeholder}] ");
            Console.ResetColor();
        }

        // Show help text if available
        if (!string.IsNullOrWhiteSpace(prompt.Hints?.HelpText))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  ℹ {prompt.Hints.HelpText}");
            Console.ResetColor();
            Console.Write("  > ");
        }

        var response = Console.ReadLine() ?? string.Empty;

        // If empty and placeholder exists, ask if user wants to use placeholder
        if (string.IsNullOrWhiteSpace(response) && !string.IsNullOrWhiteSpace(placeholder))
        {
            return string.Empty; // User skipped
        }

        return response.Trim();
    }

    private string PromptForValue(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var value = Console.ReadLine() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>();

        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                var parts = arg.Split('=', 2);
                if (parts.Length == 2)
                {
                    options[parts[0]] = parts[1];
                }
                else
                {
                    options[parts[0]] = "true";
                }
            }
        }

        return options;
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
