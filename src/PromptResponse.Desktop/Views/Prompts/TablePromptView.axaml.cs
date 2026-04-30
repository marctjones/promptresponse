using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class TablePromptView : UserControl
{
    public TablePromptView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
