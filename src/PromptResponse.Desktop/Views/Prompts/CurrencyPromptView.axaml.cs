using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.InputFormatters;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class CurrencyPromptView : UserControl
{
    private IDisposable? _maskAttachment;

    public CurrencyPromptView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RebindMask();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RebindMask()
    {
        _maskAttachment?.Dispose();
        _maskAttachment = null;
        if (DataContext is not CurrencyPromptViewModel vm) return;
        var tb = this.FindControl<TextBox>("ResponseTextBox");
        if (tb is null) return;
        // Currency formats only on commit: live reshaping of "12." → "$12.00"
        // would fight the user mid-keystroke.
        _maskAttachment = InputMaskBehavior.Attach(
            tb, new CurrencyInputFormatter(), vm.ProfileService, InputMaskBehavior.Trigger.OnCommit);
    }
}
