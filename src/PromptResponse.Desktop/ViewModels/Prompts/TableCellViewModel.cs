using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Compact view-model for one table cell. Wraps the underlying cell
/// <see cref="PromptViewModelBase"/> (which is a normal Prompt VM materialized
/// from the row sub-Section's Prompts list) so the cell editor can two-way bind
/// to <see cref="Value"/> without having to render the full prompt template.
/// </summary>
/// <remarks>
/// Vision invariants:
///   * <see cref="Value"/> is always a string — type hints are advisory.
///   * Setting <see cref="Value"/> goes through the underlying prompt VM so the
///     shell's Response-change subscription fires (progress + advisories refresh).
///   * Setting <see cref="Value"/> from outside (e.g., autofill) propagates back
///     here via the wrapped VM's PropertyChanged event.
/// </remarks>
public sealed partial class TableCellViewModel : ObservableObject, IDisposable
{
    private readonly PromptViewModelBase _vm;
    private bool _disposed;

    public TableCellViewModel(PromptViewModelBase wrappedPromptVm, TableColumn column)
    {
        _vm = wrappedPromptVm ?? throw new ArgumentNullException(nameof(wrappedPromptVm));
        ColumnId = column.Id;
        ColumnLabel = column.Label;
        ColumnType = column.Type;
        Placeholder = column.Placeholder;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    /// <summary>Stable column identifier — declares which column this cell belongs to.</summary>
    public string ColumnId { get; }

    /// <summary>User-visible column header label.</summary>
    public string ColumnLabel { get; }

    /// <summary>Type-hint for the column ("text", "number", "currency", "date", "boolean", ...).
    /// Used for advisories / formatting; never enforced.</summary>
    public string ColumnType { get; }

    /// <summary>Optional placeholder text for empty cells.</summary>
    public string? Placeholder { get; }

    /// <summary>The cell's current value. Always a string. Free text accepted.</summary>
    public string Value
    {
        get => _vm.Response;
        set
        {
            var v = value ?? string.Empty;
            if (_vm.Response == v) return;
            _vm.Response = v;
            // Notification comes back via OnVmPropertyChanged, but raise here too
            // so a same-frame two-way binding settles immediately.
            OnPropertyChanged();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptViewModelBase.Response))
        {
            OnPropertyChanged(nameof(Value));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}
