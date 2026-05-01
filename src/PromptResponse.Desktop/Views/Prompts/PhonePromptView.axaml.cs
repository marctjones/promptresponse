using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.InputFormatters;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class PhonePromptView : UserControl
{
    private IDisposable? _maskAttachment;

    public PhonePromptView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RebindMask();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RebindMask()
    {
        _maskAttachment?.Dispose();
        _maskAttachment = null;
        if (DataContext is not PhonePromptViewModel vm) return;
        var tb = this.FindControl<TextBox>("ResponseTextBox");
        if (tb is null) return;
        _maskAttachment = InputMaskBehavior.Attach(
            tb, new PhoneInputFormatter(), vm.ProfileService, InputMaskBehavior.Trigger.Live);
    }
}
