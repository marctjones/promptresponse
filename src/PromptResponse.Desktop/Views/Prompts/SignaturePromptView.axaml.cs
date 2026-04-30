using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class SignaturePromptView : UserControl
{
    public SignaturePromptView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
