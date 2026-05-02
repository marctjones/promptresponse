using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// View-model wrapper around an <see cref="AprDocument.Metadata"/> for the
/// edit-mode metadata panel. Exposes every settable field as a two-way
/// property; setters write through to the underlying Metadata instance and
/// raise PropertyChanged so other parts of the shell (page header, sidebar)
/// can refresh when the title changes.
/// </summary>
public sealed class DocumentMetadataViewModel : INotifyPropertyChanged
{
    private readonly Metadata _metadata;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever any field on this metadata wrapper changes.
    /// The shell uses this to mark the document dirty + refresh derived
    /// display properties (e.g. page header title).</summary>
    public event EventHandler? Changed;

    public DocumentMetadataViewModel(Metadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public string Title
    {
        get => _metadata.Title;
        set
        {
            var v = value ?? string.Empty;
            if (_metadata.Title == v) return;
            _metadata.Title = v;
            Notify();
        }
    }

    public string? Description
    {
        get => _metadata.Description;
        set
        {
            if (_metadata.Description == value) return;
            _metadata.Description = value;
            Notify();
        }
    }

    public string? Author
    {
        get => _metadata.Author;
        set
        {
            if (_metadata.Author == value) return;
            _metadata.Author = value;
            Notify();
        }
    }

    public string? TemplateId
    {
        get => _metadata.TemplateId;
        set
        {
            if (_metadata.TemplateId == value) return;
            _metadata.TemplateId = value;
            Notify();
        }
    }

    public string? TemplateVersion
    {
        get => _metadata.TemplateVersion;
        set
        {
            if (_metadata.TemplateVersion == value) return;
            _metadata.TemplateVersion = value;
            Notify();
        }
    }

    private void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
