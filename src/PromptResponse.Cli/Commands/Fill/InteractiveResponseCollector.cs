using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Commands.Fill;

/// <summary>Collects the authored answers for the fill command's interactive mode.</summary>
internal static class InteractiveResponseCollector
{
    internal static (Dictionary<string, string> Responses, string FilledBy) Collect(
        AprDocument template, string? suppliedFilledBy)
    {
        Console.WriteLine("=== Interactive Form Filling ===");
        Console.WriteLine("(Press Enter to skip a field, Ctrl+C to cancel)");
        Console.WriteLine();

        var responses = new Dictionary<string, string>();
        var filledBy = suppliedFilledBy ?? PromptForValue("Filled by", Environment.UserName);
        foreach (var section in template.Sections) CollectSection(section, responses, 0);
        return (responses, filledBy);
    }

    private static void CollectSection(Section section, Dictionary<string, string> responses, int depth)
    {
        var indent = new string(' ', depth * 2);
        var marker = depth == 0 ? "---" : "--";
        Console.WriteLine($"\n{indent}{marker} {section.Title} {marker}");
        if (!string.IsNullOrWhiteSpace(section.Description)) Console.WriteLine($"{indent}    {section.Description}");
        Console.WriteLine();

        foreach (var prompt in section.Prompts)
        {
            var response = PromptForResponse(prompt);
            if (!string.IsNullOrEmpty(response)) responses[prompt.Id] = response;
        }
        foreach (var child in section.Sections) CollectSection(child, responses, depth + 1);
    }

    private static string PromptForResponse(Prompt prompt)
    {
        Console.Write($"{prompt.Label}: ");
        var placeholder = prompt.Hints?.Placeholder;
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{placeholder}] ");
            Console.ResetColor();
        }
        if (!string.IsNullOrWhiteSpace(prompt.Hints?.HelpText))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  ℹ {prompt.Hints.HelpText}");
            Console.ResetColor();
            Console.Write("  > ");
        }
        var response = Console.ReadLine() ?? string.Empty;
        return string.IsNullOrWhiteSpace(response) ? string.Empty : response.Trim();
    }

    private static string PromptForValue(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var value = Console.ReadLine() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }
}
