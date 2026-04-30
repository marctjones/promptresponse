using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class CurrencyPromptView : UserControl
{
    public CurrencyPromptView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
