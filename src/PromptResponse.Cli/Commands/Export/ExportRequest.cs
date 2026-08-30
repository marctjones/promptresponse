using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Cli.Commands.Export;

/// <summary>
/// The command-line choices which determine one export operation.
/// Parsing them once keeps file validation and rendering independent of CLI syntax.
/// </summary>
internal sealed record ExportRequest(
    string InputPath,
    ExportFormat Format,
    string? OutputPath,
    bool ExcludeEmpty,
    bool Fillable,
    PdfPageSize PageSize,
    string? Banner,
    bool Archival)
{
    internal static bool TryParse(string[] args, out ExportRequest? request, out string? error)
    {
        var inputPath = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        if (string.IsNullOrEmpty(inputPath))
        {
            request = null;
            error = "Error: File path required";
            return false;
        }

        var formatValue = GetValue(args, "--format") ?? "csv";
        if (!ExportFormatExtensions.TryParse(formatValue, out var format))
        {
            request = null;
            error = $"Error: Unsupported format: {formatValue}";
            return false;
        }

        request = new ExportRequest(
            inputPath,
            format,
            GetValue(args, "--output"),
            args.Contains("--exclude-empty"),
            args.Contains("--fillable"),
            ParsePageSize(GetValue(args, "--page-size")),
            GetValue(args, "--banner"),
            args.Contains("--pdfa"));
        error = null;
        return true;
    }

    private static string? GetValue(IEnumerable<string> args, string prefix)
    {
        var argument = args.FirstOrDefault(value => value.StartsWith(prefix + "=", StringComparison.Ordinal));
        return argument?[((prefix.Length + 1)..)];
    }

    private static PdfPageSize ParsePageSize(string? value) => value?.ToLowerInvariant() switch
    {
        "a4" => PdfPageSize.A4,
        "legal" => PdfPageSize.Legal,
        _ => PdfPageSize.Letter,
    };
}

internal enum ExportFormat
{
    Csv,
    Json,
    Text,
    Html,
    Pdf,
}

internal static class ExportFormatExtensions
{
    internal static bool TryParse(string value, out ExportFormat format)
    {
        if (value.Equals("txt", StringComparison.OrdinalIgnoreCase))
        {
            format = ExportFormat.Text;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out format) && Enum.IsDefined(format);
    }
}
