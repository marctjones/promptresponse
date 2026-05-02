using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views.Editor;

/// <summary>
/// Recursive structural editor for a section. Renders title/description as
/// inline editors, prompts via <see cref="PromptEditorView"/>, and child
/// sections via this same view recursively. Add/remove buttons are wired to
/// <see cref="SectionViewModel"/> commands.
/// </summary>
/// <remarks>
/// The remove-section button works for any depth: it walks up the visual tree
/// to find the parent SectionViewModel (for nested sections) or falls back to
/// the shell's RemoveTopLevelSectionCommand (for depth-0 sections that have no
/// parent SectionViewModel).
/// </remarks>
public partial class SectionEditorView : UserControl
{
    public SectionEditorView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnRemoveColumnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctrl) return;
        if (ctrl.DataContext is not PromptResponse.Core.Models.TableColumn column) return;
        if (DataContext is not SectionViewModel section) return;
        if (section.RemoveColumnCommand.CanExecute(column))
        {
            section.RemoveColumnCommand.Execute(column);
        }
    }

    private void OnRemoveFixedRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control ctrl) return;
        if (ctrl.DataContext is not SectionViewModel rowVm) return;
        if (DataContext is not SectionViewModel section) return;
        if (section.RemoveFixedRowCommand.CanExecute(rowVm))
        {
            section.RemoveFixedRowCommand.Execute(rowVm);
        }
    }

    private void OnRemoveSectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SectionViewModel selfVm) return;

        var parentSection = FindParentSectionViewModel(selfVm);
        if (parentSection?.RemoveNestedSectionCommand is { } childCmd && childCmd.CanExecute(selfVm))
        {
            childCmd.Execute(selfVm);
            return;
        }

        // No parent SectionViewModel — this is a top-level section. Dispatch via
        // the shell's RemoveTopLevelSectionCommand.
        var shell = FindAncestorMainShellViewModel();
        if (shell?.RemoveTopLevelSectionCommand is { } shellCmd && shellCmd.CanExecute(selfVm))
        {
            shellCmd.Execute(selfVm);
        }
    }

    private SectionViewModel? FindParentSectionViewModel(SectionViewModel self)
    {
        foreach (var visual in this.GetVisualAncestors())
        {
            if (visual is Control c
                && c.DataContext is SectionViewModel candidate
                && !ReferenceEquals(candidate, self))
            {
                return candidate;
            }
        }
        return null;
    }

    private MainShellViewModel? FindAncestorMainShellViewModel()
    {
        foreach (var visual in this.GetVisualAncestors())
        {
            if (visual is Control c && c.DataContext is MainShellViewModel shell)
            {
                return shell;
            }
        }
        return null;
    }
}
