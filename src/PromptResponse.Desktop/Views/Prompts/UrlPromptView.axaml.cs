using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class UrlPromptView : UserControl
{
    public UrlPromptView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
