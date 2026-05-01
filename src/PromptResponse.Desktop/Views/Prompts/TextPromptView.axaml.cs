using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.InputFormatters;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class TextPromptView : UserControl
{
    private IDisposable? _maskAttachment;

    public TextPromptView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RebindMask();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RebindMask()
    {
        _maskAttachment?.Dispose();
        _maskAttachment = null;
        if (DataContext is not PromptViewModelBase vm) return;
        var formatter = InputFormatterRegistry.ForHint(vm.ExpectedDataType);
        if (formatter is null) return;
        var tb = this.FindControl<TextBox>("ResponseTextBox");
        if (tb is null) return;

        // Percentage masks are commit-time (trailing "%" interferes with typing);
        // digit masks (SSN/EIN/ZIP) shape live on natural boundaries.
        var trigger = formatter is PercentageInputFormatter or CurrencyInputFormatter
            ? InputMaskBehavior.Trigger.OnCommit
            : InputMaskBehavior.Trigger.Live;
        _maskAttachment = InputMaskBehavior.Attach(tb, formatter, vm.ProfileService, trigger);
    }
}
