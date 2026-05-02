using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.InputFormatters;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class PhonePromptView : UserControl
{
    private static readonly bool TraceEnabled =
        Environment.GetEnvironmentVariable("PROMPTRESPONSE_TRACE_INPUT_MASK") is { Length: > 0 } v
        && v != "0" && !v.Equals("false", System.StringComparison.OrdinalIgnoreCase);

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
        if (DataContext is not PhonePromptViewModel vm)
        {
            if (TraceEnabled) System.Console.WriteLine("[InputMask] PhonePromptView.RebindMask: DataContext is not PhonePromptViewModel — skipping");
            return;
        }
        var tb = this.FindControl<TextBox>("ResponseTextBox");
        if (tb is null)
        {
            if (TraceEnabled) System.Console.WriteLine("[InputMask] PhonePromptView.RebindMask: ResponseTextBox not found in tree — skipping");
            return;
        }
        if (TraceEnabled) System.Console.WriteLine($"[InputMask] PhonePromptView.RebindMask: attaching for prompt id='{vm.Id}'");
        _maskAttachment = InputMaskBehavior.Attach(
            tb, new PhoneInputFormatter(), vm.ProfileService, InputMaskBehavior.Trigger.Live);
    }
}
