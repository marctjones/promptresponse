using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// View-model wrapper around an <see cref="AprDocument.Metadata"/> for the
/// edit-mode metadata panel. Exposes every settable field as a two-way
/// property; setters write through to the underlying Metadata instance and
/// raise PropertyChanged so other parts of the shell (page header, sidebar)
/// can refresh when the title changes.
/// </summary>
/// <remarks>
/// When constructed with an <see cref="EditHistory"/>, every property setter
/// records its edit as a mergeable property-edit command so the user can
/// Ctrl+Z out of metadata field changes the same way they can out of any
/// other authoring edit.
/// </remarks>
public sealed class DocumentMetadataViewModel : INotifyPropertyChanged
{
    private readonly Metadata _metadata;
    private readonly EditHistory? _history;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever any field on this metadata wrapper changes.
    /// The shell uses this to mark the document dirty + refresh derived
    /// display properties (e.g. page header title).</summary>
    public event EventHandler? Changed;

    public DocumentMetadataViewModel(Metadata metadata, EditHistory? history = null)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _history = history;
    }

    public string Title
    {
        get => _metadata.Title;
        set => SetWithUndo(nameof(Title), () => _metadata.Title, v => _metadata.Title = v, value ?? string.Empty);
    }

    public string? Description
    {
        get => _metadata.Description;
        set => SetWithUndo(nameof(Description), () => _metadata.Description, v => _metadata.Description = v, value);
    }

    public string? Author
    {
        get => _metadata.Author;
        set => SetWithUndo(nameof(Author), () => _metadata.Author, v => _metadata.Author = v, value);
    }

    public string? TemplateId
    {
        get => _metadata.TemplateId;
        set => SetWithUndo(nameof(TemplateId), () => _metadata.TemplateId, v => _metadata.TemplateId = v, value);
    }

    public string? TemplateVersion
    {
        get => _metadata.TemplateVersion;
        set => SetWithUndo(nameof(TemplateVersion), () => _metadata.TemplateVersion, v => _metadata.TemplateVersion = v, value);
    }

    private void SetWithUndo<T>(string propertyName, Func<T> getter, Action<T> applySetter, T newValue)
    {
        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue)) return;

        if (_history?.IsApplying == true)
        {
            applySetter(newValue);
            Notify(propertyName);
            return;
        }
        if (_history != null)
        {
            _history.Execute(new PropertyEditCommand<T>(
                this, propertyName,
                v => { applySetter(v); Notify(propertyName); },
                oldValue, newValue));
        }
        else
        {
            applySetter(newValue);
            Notify(propertyName);
        }
    }

    private void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
