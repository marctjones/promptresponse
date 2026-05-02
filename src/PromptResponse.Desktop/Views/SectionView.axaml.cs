using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views;

public partial class SectionView : UserControl
{
    /// <summary>
    /// Maps a section's nesting depth to its title font size: top-level (depth 0)
    /// gets 18pt, depth 1 → 16pt, depth 2 → 15pt, depth 3+ clamps at 14pt. Keeps
    /// the visual hierarchy readable without making deep sections microscopic.
    /// </summary>
    public static readonly IMultiValueConverter SectionTitleSizeConverter = new SectionDepthFontSizeConverter();

    public SectionView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private sealed class SectionDepthFontSizeConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var depth = values.FirstOrDefault() is int d ? d : 0;
            return depth switch
            {
                0 => 18.0,
                1 => 16.0,
                2 => 15.0,
                _ => 14.0,
            };
        }
    }
}
