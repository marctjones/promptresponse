using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Editor;

/// <summary>
/// Inline structural editor for a single prompt. Inverts the fill-mode
/// rendering: instead of an editor for the response, every field shown here
/// edits the prompt's authoring metadata (label, expected type, placeholder,
/// help text, validation pattern).
/// </summary>
/// <remarks>
/// The remove button walks up the visual tree to find the parent
/// <see cref="SectionEditorView"/>'s SectionViewModel and dispatches its
/// RemovePromptCommand. Routing it that way avoids needing an explicit
/// ItemsControl-binding gymnastic from XAML.
/// </remarks>
public partial class PromptEditorView : UserControl
{
    public PromptEditorView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PromptViewModelBase promptVm) return;
        var sectionVm = this.FindAncestorSectionViewModel();
        if (sectionVm?.RemovePromptCommand is { } cmd && cmd.CanExecute(promptVm))
        {
            cmd.Execute(promptVm);
        }
    }
}

internal static class PromptEditorRoutingExtensions
{
    /// <summary>Walks up the visual tree to find the nearest SectionEditorView's
    /// bound SectionViewModel. Used for routing remove commands that don't have
    /// a direct ItemsControl-parent binding path.</summary>
    public static SectionViewModel? FindAncestorSectionViewModel(this Control control)
    {
        foreach (var visual in control.GetVisualAncestors())
        {
            if (visual is Control c && c.DataContext is SectionViewModel section)
            {
                return section;
            }
        }
        return null;
    }
}
