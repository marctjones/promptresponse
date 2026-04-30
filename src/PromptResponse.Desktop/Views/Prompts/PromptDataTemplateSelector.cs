using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

/// <summary>
/// Picks the right view at runtime for each prompt VM. The form-filling host wires
/// this up via a DataTemplate; each VM type gets its dedicated, individually
/// accessibility-tested rendering.
/// </summary>
public sealed class PromptDataTemplateSelector : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        BooleanPromptViewModel       => new BooleanPromptView(),
        SelectPromptViewModel        => new SelectPromptView(),
        MultilinePromptViewModel     => new MultilinePromptView(),
        DatePromptViewModel          => new DatePromptView(),
        NumberPromptViewModel        => new NumberPromptView(),
        // Fallback: every other typed VM (Text, Email, Url, Phone, Currency,
        // Signature, File, Multichoice, Table) renders as a single-line text input
        // until its dedicated view ships in subsequent Phase 3 follow-ups. The
        // base class already exposes Label / Response / Placeholder / HelpText;
        // TextPromptView reads them all.
        PromptViewModelBase => new TextPromptView(),
        _ => null,
    };

    public bool Match(object? data) => data is PromptViewModelBase;
}
