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
        DataContextChanged += (_, _) => { RebindMask(); ApplyTypePresentation(); };
    }

    // Types whose answer is short by nature: a time, a range value, a colour code, or a
    // masked identifier. Width is presentation only (App.axaml); the stored text is untouched.
    private static readonly HashSet<string> ShortTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "time", "range", "color", "ssn", "ein", "zip", "percentage",
    };

    private TextBox? _swatchBox;
    private EventHandler<TextChangedEventArgs>? _swatchHandler;

    private void ApplyTypePresentation()
    {
        if (_swatchBox is not null && _swatchHandler is not null) _swatchBox.TextChanged -= _swatchHandler;
        _swatchBox = null;
        _swatchHandler = null;
        var type = (DataContext as PromptViewModelBase)?.ExpectedDataType;

        if (type is not null && ShortTypes.Contains(type)) Classes.Add("field-short");
        else Classes.Remove("field-short");

        var swatch = this.FindControl<Border>("ColorSwatch");
        var box = this.FindControl<TextBox>("ResponseTextBox");
        if (swatch is null || box is null) return;
        var isColor = string.Equals(type, "color", StringComparison.OrdinalIgnoreCase);
        swatch.IsVisible = isColor;
        if (!isColor) return;

        void Update() => swatch.Background =
            Avalonia.Media.Color.TryParse(box.Text?.Trim() ?? "", out var color)
                ? new Avalonia.Media.SolidColorBrush(color)
                : Avalonia.Media.Brushes.Transparent;
        Update();
        _swatchBox = box;
        _swatchHandler = (_, _) => Update();
        box.TextChanged += _swatchHandler;
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
