using PromptResponse.Core.Models;
using System.Collections.ObjectModel;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for filling out a form.
/// </summary>
public class FormFillingViewModel : ViewModelBase
{
    private readonly AprDocument _document;
    private bool _hasUnsavedChanges;
    private string _statusMessage = string.Empty;
    private string _mode = "Filling Form";

    public FormFillingViewModel(AprDocument document)
    {
        _document = document;
        Sections = new ObservableCollection<SectionViewModel>(
            document.Sections.Select(s => new SectionViewModel(s)));

        // Subscribe to property changes to track unsaved changes
        foreach (var section in Sections)
        {
            foreach (var prompt in section.Prompts)
            {
                prompt.PropertyChanged += (s, e) => HasUnsavedChanges = true;
            }
            foreach (var subsection in section.Subsections)
            {
                foreach (var prompt in subsection.Prompts)
                {
                    prompt.PropertyChanged += (s, e) => HasUnsavedChanges = true;
                }
            }
        }

        UpdateStatusMessage();
    }

    public string Title => _document.Metadata.Title;
    public string? Description => _document.Metadata.Description;
    public ObservableCollection<SectionViewModel> Sections { get; }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (SetProperty(ref _hasUnsavedChanges, value))
            {
                UpdateStatusMessage();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public string SaveStateText => HasUnsavedChanges ? "● Modified" : "Saved";

    private void UpdateStatusMessage()
    {
        if (HasUnsavedChanges)
        {
            StatusMessage = "You have unsaved changes";
        }
        else
        {
            StatusMessage = "Ready";
        }
        OnPropertyChanged(nameof(SaveStateText));
    }

    public void SetStatusMessage(string message)
    {
        StatusMessage = message;
    }

    public void MarkAsSaved()
    {
        HasUnsavedChanges = false;
        UpdateStatusMessage();
    }

    /// <summary>
    /// Updates the document with current ViewModel values.
    /// </summary>
    public void UpdateDocument()
    {
        // ViewModels update the model in real-time through two-way binding
        // This method is here for future explicit save operations if needed
        _document.Metadata.Modified = DateTime.UtcNow;
    }
}

/// <summary>
/// ViewModel for a section.
/// </summary>
public class SectionViewModel : ViewModelBase
{
    private readonly Section _section;
    private bool _isExpanded = true;

    public SectionViewModel(Section section)
    {
        _section = section;
        Subsections = new ObservableCollection<SubsectionViewModel>(
            section.Subsections.Select(s => new SubsectionViewModel(s)));
        Prompts = new ObservableCollection<PromptViewModel>(
            section.Prompts.Select(p => new PromptViewModel(p)));
    }

    public string Title => _section.Title;
    public string? Description => _section.Description;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<SubsectionViewModel> Subsections { get; }
    public ObservableCollection<PromptViewModel> Prompts { get; }
}

/// <summary>
/// ViewModel for a subsection.
/// </summary>
public class SubsectionViewModel : ViewModelBase
{
    private readonly Subsection _subsection;
    private bool _isExpanded = true;

    public SubsectionViewModel(Subsection subsection)
    {
        _subsection = subsection;
        Prompts = new ObservableCollection<PromptViewModel>(
            subsection.Prompts.Select(p => new PromptViewModel(p)));
    }

    public string Title => _subsection.Title;
    public string? Description => _subsection.Description;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<PromptViewModel> Prompts { get; }
}

/// <summary>
/// ViewModel for a prompt.
/// </summary>
public class PromptViewModel : ViewModelBase
{
    private readonly Prompt _prompt;

    public PromptViewModel(Prompt prompt)
    {
        _prompt = prompt;
    }

    public string Label => _prompt.Label;
    public string? Placeholder => _prompt.Hints.Placeholder;
    public string? HelpText => _prompt.Hints.HelpText;
    public string? ExpectedDataType => _prompt.Hints.ExpectedDataType;

    public string Response
    {
        get => _prompt.Response;
        set
        {
            if (_prompt.Response != value)
            {
                _prompt.Response = value;
                OnPropertyChanged();
            }
        }
    }
}
