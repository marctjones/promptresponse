using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

/// <summary>
/// Picks the right view at runtime for each prompt VM. Each typed VM gets its own
/// dedicated, individually accessibility-tested rendering. Unknown VM types fall
/// back to <see cref="TextPromptView"/>.
/// </summary>
public sealed class PromptDataTemplateSelector : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        BooleanPromptViewModel       => new BooleanPromptView(),
        SelectPromptViewModel        => new SelectPromptView(),
        MultichoicePromptViewModel   => new MultichoicePromptView(),
        MultilinePromptViewModel     => new MultilinePromptView(),
        DatePromptViewModel          => new DatePromptView(),
        NumberPromptViewModel        => new NumberPromptView(),
        CurrencyPromptViewModel      => new CurrencyPromptView(),
        EmailPromptViewModel         => new EmailPromptView(),
        UrlPromptViewModel           => new UrlPromptView(),
        PhonePromptViewModel         => new PhonePromptView(),
        SignaturePromptViewModel     => new SignaturePromptView(),
        FilePromptViewModel          => new FilePromptView(),
        PromptViewModelBase          => new TextPromptView(),
        _ => null,
    };

    public bool Match(object? data) => data is PromptViewModelBase;
}
