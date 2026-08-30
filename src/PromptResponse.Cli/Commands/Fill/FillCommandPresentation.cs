using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;

namespace PromptResponse.Cli.Commands.Fill;

/// <summary>Owns the stable console presentation for the fill command.</summary>
internal static class FillCommandPresentation
{
    internal static void WriteTemplate(AprDocument template)
    {
        Console.WriteLine($"Template: {template.Metadata?.Title ?? "Untitled"}");
        Console.WriteLine($"Template ID: {template.Metadata?.TemplateId ?? "N/A"}");
        Console.WriteLine();
    }

    internal static void WriteCompletion(double completion, string outputPath)
    {
        Console.WriteLine();
        Console.WriteLine("Form filling complete!");
        Console.WriteLine($"Completion: {completion:F1}%");
        Console.WriteLine($"Saved to: {outputPath}");
    }

    internal static void WriteValidation(ValidationResult validation)
    {
        Console.WriteLine();
        Console.WriteLine("Validating...");
        if (validation.IsValid) { Console.WriteLine("✓ Validation passed"); return; }
        Console.WriteLine("⚠ Validation warnings:");
        foreach (var error in validation.Errors) Console.WriteLine($"  - {error.Message}");
    }
}
