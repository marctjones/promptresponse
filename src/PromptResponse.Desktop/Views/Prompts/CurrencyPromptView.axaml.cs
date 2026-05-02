using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.InputFormatters;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class CurrencyPromptView : UserControl
{
    private static readonly bool TraceEnabled =
        Environment.GetEnvironmentVariable("PROMPTRESPONSE_TRACE_INPUT_MASK") is { Length: > 0 } v
        && v != "0" && !v.Equals("false", System.StringComparison.OrdinalIgnoreCase);

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
        if (DataContext is not CurrencyPromptViewModel vm)
        {
            if (TraceEnabled) System.Console.WriteLine("[InputMask] CurrencyPromptView.RebindMask: DataContext not CurrencyPromptViewModel — skipping");
            return;
        }
        var tb = this.FindControl<TextBox>("ResponseTextBox");
        if (tb is null)
        {
            if (TraceEnabled) System.Console.WriteLine("[InputMask] CurrencyPromptView.RebindMask: ResponseTextBox not found — skipping");
            return;
        }
        if (TraceEnabled) System.Console.WriteLine($"[InputMask] CurrencyPromptView.RebindMask: attaching for prompt id='{vm.Id}' (commit-time)");
        // Currency formats only on commit: live reshaping of "12." → "$12.00"
        // would fight the user mid-keystroke.
        _maskAttachment = InputMaskBehavior.Attach(
            tb, new CurrencyInputFormatter(), vm.ProfileService, InputMaskBehavior.Trigger.OnCommit);
    }
}
