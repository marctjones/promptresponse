using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Desktop.Services.Dialogs;

/// <summary>
/// Converts the layout-free render model into the in-app print-preview control tree.
/// </summary>
internal static class PrintPreviewContentBuilder
{
    public static Control Build(RenderModel model, bool includeEmptyFields)
    {
        var page = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(28),
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        page.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            page.Children.Add(new TextBlock
            {
                Text = model.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
            });
        }

        page.Children.Add(new TextBlock
        {
            Text = $"PDF export preview - Letter page size - {(includeEmptyFields ? "blank fields included" : "blank fields excluded")}",
            FontSize = 12,
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var block in model.Blocks)
        {
            AddBlock(page, block);
        }

        return page;
    }

    private static void AddBlock(StackPanel page, RenderBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                AddHeading(page, heading);
                break;
            case FieldBlock field:
                AddField(page, field);
                break;
            case TableBlock table:
                page.Children.Add(BuildTable(table));
                break;
            case SignatureBlock signatures:
                AddSignatures(page, signatures);
                break;
        }
    }

    private static void AddHeading(StackPanel page, HeadingBlock heading)
    {
        page.Children.Add(new TextBlock
        {
            Text = heading.Text,
            FontSize = heading.Level == 1 ? 18 : 15,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, heading.Level == 1 ? 14 : 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(heading.Description))
        {
            page.Children.Add(new TextBlock
            {
                Text = heading.Description,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private static void AddField(StackPanel page, FieldBlock field)
    {
        page.Children.Add(new TextBlock { Text = field.Label, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        page.Children.Add(new TextBlock
        {
            Text = field.Value,
            Margin = new Thickness(14, -6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = field.HasResponse ? Brushes.Black : Brushes.DimGray,
        });
        if (!string.IsNullOrWhiteSpace(field.HelpText))
        {
            page.Children.Add(new TextBlock
            {
                Text = field.HelpText,
                Margin = new Thickness(14, -8, 0, 0),
                FontSize = 12,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private static Control BuildTable(TableBlock table)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(new TextBlock
        {
            Text = table.ColumnHeaders.Count == 0 ? "(no columns)" : string.Join(" | ", table.ColumnHeaders),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var row in table.Rows)
        {
            var values = row.Cells.Select(cell => string.IsNullOrWhiteSpace(cell.Value) ? "[blank]" : cell.Value);
            panel.Children.Add(new TextBlock { Text = $"{row.Label}: {string.Join(" | ", values)}", TextWrapping = TextWrapping.Wrap });
        }
        return panel;
    }

    private static void AddSignatures(StackPanel page, SignatureBlock signatures)
    {
        page.Children.Add(new TextBlock { Text = "Signatures", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
        foreach (var signature in signatures.Signatures)
        {
            page.Children.Add(new TextBlock
            {
                Text = $"[{(signature.ContentValid ? "verified" : "INVALID")}] {signature.Role}: {signature.Signer} - {signature.Scope}",
                TextWrapping = TextWrapping.Wrap,
            });
            page.Children.Add(new TextBlock
            {
                Text = $"trust: {signature.Trust} - {signature.Status}",
                FontSize = 12,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }
}
