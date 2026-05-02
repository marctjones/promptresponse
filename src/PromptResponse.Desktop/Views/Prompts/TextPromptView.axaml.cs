using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.InputFormatters;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class TextPromptView : UserControl
{
    private static readonly bool TraceEnabled =
        Environment.GetEnvironmentVariable("PROMPTRESPONSE_TRACE_INPUT_MASK") is { Length: > 0 } v
        && v != "0" && !v.Equals("false", System.StringComparison.OrdinalIgnoreCase);

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
        if (DataContext is not PromptViewModelBase vm)
        {
            if (TraceEnabled) System.Console.WriteLine("[InputMask] TextPromptView.RebindMask: DataContext not PromptViewModelBase — skipping");
            return;
        }
        var formatter = InputFormatterRegistry.ForHint(vm.ExpectedDataType);
        if (formatter is null)
        {
            if (TraceEnabled) System.Console.WriteLine($"[InputMask] TextPromptView.RebindMask: hint='{vm.ExpectedDataType}' has no formatter — skipping");
            return;
        }
        var tb = this.FindControl<TextBox>("ResponseTextBox");
        if (tb is null)
        {
            if (TraceEnabled) System.Console.WriteLine($"[InputMask] TextPromptView.RebindMask: ResponseTextBox not found for hint='{vm.ExpectedDataType}' — skipping");
            return;
        }

        // Percentage masks are commit-time (trailing "%" interferes with typing);
        // digit masks (SSN/EIN/ZIP) shape live on natural boundaries.
        var trigger = formatter is PercentageInputFormatter or CurrencyInputFormatter
            ? InputMaskBehavior.Trigger.OnCommit
            : InputMaskBehavior.Trigger.Live;

        if (TraceEnabled) System.Console.WriteLine($"[InputMask] TextPromptView.RebindMask: attaching {formatter.GetType().Name} for prompt id='{vm.Id}' hint='{vm.ExpectedDataType}' trigger={trigger}");
        _maskAttachment = InputMaskBehavior.Attach(tb, formatter, vm.ProfileService, trigger);
    }
}
