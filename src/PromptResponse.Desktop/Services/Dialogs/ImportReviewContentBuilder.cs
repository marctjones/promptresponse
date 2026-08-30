using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Desktop.Services.Dialogs;

/// <summary>
/// Builds the explanatory content of the PDF-import quality review dialog.
/// </summary>
internal static class ImportReviewContentBuilder
{
    public static Control Build(ImportQuality quality)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "PDF Import Needs Review", FontSize = 22, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = quality.Summary, TextWrapping = TextWrapping.Wrap });

        var readable = (int)Math.Round((1 - quality.CrypticLabelRatio) * 100);
        var tooltip = (int)Math.Round(quality.TooltipCoverage * 100);
        var duplicate = (int)Math.Round(quality.DuplicateLabelRatio * 100);
        panel.Children.Add(new TextBlock
        {
            Text = $"Score: {quality.Score}/100 ({quality.Grade})  -  Fields: {quality.FieldCount}  -  Readable labels: {readable}%  -  Tooltips: {tooltip}%  -  Duplicate labels: {duplicate}%",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = quality.Recommendation == ImportRecommendation.UseSkillInstead
                ? "Recommended next step: use the document-to-apr skill or the importer-to-skill hybrid workflow to enrich labels and sections while preserving imported field IDs."
                : "Recommended next step: open the template and review the flagged fields before sharing it.",
            TextWrapping = TextWrapping.Wrap,
        });

        var counts = quality.Flags.GroupBy(flag => flag.Kind).OrderBy(group => group.Key.ToString()).Select(group => $"{LabelFor(group.Key)}: {group.Count()}");
        panel.Children.Add(new TextBlock
        {
            Text = quality.Flags.Count == 0 ? "No field-level flags were reported." : "Flag summary: " + string.Join("  |  ", counts),
            TextWrapping = TextWrapping.Wrap,
        });
        AddFlagSamples(panel, quality.Flags);
        return panel;
    }

    private static void AddFlagSamples(StackPanel panel, IReadOnlyList<FieldFlag> flags)
    {
        if (flags.Count == 0) return;

        panel.Children.Add(new TextBlock { Text = "Sample fields to review", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var flag in flags.Take(12))
        {
            panel.Children.Add(new TextBlock { Text = $"{LabelFor(flag.Kind)} - {flag.Label} ({flag.PromptId}): {flag.Message}", TextWrapping = TextWrapping.Wrap });
        }
        if (flags.Count > 12)
        {
            panel.Children.Add(new TextBlock { Text = $"...and {flags.Count - 12} more flagged fields.", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        }
    }

    private static string LabelFor(FieldFlagKind kind) => kind switch
    {
        FieldFlagKind.CrypticLabel => "Cryptic label",
        FieldFlagKind.DuplicateLabel => "Duplicate label",
        FieldFlagKind.AmbiguousChoice => "Ambiguous choice",
        _ => kind.ToString(),
    };
}
