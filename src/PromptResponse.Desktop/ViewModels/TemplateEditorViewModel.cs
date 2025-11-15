using PromptResponse.Core.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for editing templates (creating/editing .aprt files).
/// </summary>
public class TemplateEditorViewModel : ViewModelBase
{
    private readonly AprDocument _document;
    private bool _hasUnsavedChanges;
    private string _statusMessage = string.Empty;
    private string _mode = "Editing Template";
    private string _title;
    private string? _description;

    /// <summary>
    /// Standard field type hints available for prompts.
    /// </summary>
    public static readonly string[] ValidFieldTypes = new[]
    {
        "text",
        "email",
        "phone",
        "date",
        "time",
        "datetime",
        "number",
        "currency",
        "url",
        "multiline",
        "boolean",
        "choice",         // Single choice from suggestedValues (radio or dropdown)
        "multichoice",    // Multiple choices from suggestedValues (checkboxes)
        "password",
        "range",
        "color",
        "file"
    };

    public TemplateEditorViewModel(AprDocument document)
    {
        _document = document;
        _title = document.Metadata.Title;
        _description = document.Metadata.Description;

        Sections = new ObservableCollection<EditableSectionViewModel>(
            document.Sections.Select(s => new EditableSectionViewModel(s, this)));

        AddSectionCommand = new RelayCommand(AddSection);
        UpdateStatusMessage();
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                _document.Metadata.Title = value;
                HasUnsavedChanges = true;
            }
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                _document.Metadata.Description = value;
                HasUnsavedChanges = true;
            }
        }
    }

    public ObservableCollection<EditableSectionViewModel> Sections { get; }

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

    public ICommand AddSectionCommand { get; }

    private void AddSection()
    {
        var newSection = new Section
        {
            Id = $"section_{Sections.Count + 1}",
            Title = "New Section",
            Description = null,
            Prompts = new List<Prompt>(),
            Subsections = new List<Subsection>()
        };

        _document.Sections.Add(newSection);
        Sections.Add(new EditableSectionViewModel(newSection, this));
        HasUnsavedChanges = true;
    }

    public void RemoveSection(EditableSectionViewModel section)
    {
        _document.Sections.Remove(section.Section);
        Sections.Remove(section);
        HasUnsavedChanges = true;
    }

    public void MarkAsChanged()
    {
        HasUnsavedChanges = true;
    }

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

    public void MarkAsSaved()
    {
        HasUnsavedChanges = false;
        UpdateStatusMessage();
    }

    public void UpdateDocument()
    {
        _document.Metadata.Modified = DateTime.UtcNow;
    }
}

/// <summary>
/// ViewModel for editing a section.
/// </summary>
public class EditableSectionViewModel : ViewModelBase
{
    internal readonly TemplateEditorViewModel _parent;
    public readonly Section Section;
    private bool _isExpanded = true;
    private string _title;
    private string? _description;

    public EditableSectionViewModel(Section section, TemplateEditorViewModel parent)
    {
        Section = section;
        _parent = parent;
        _title = section.Title;
        _description = section.Description;

        Subsections = new ObservableCollection<EditableSubsectionViewModel>(
            section.Subsections.Select(s => new EditableSubsectionViewModel(s, this)));
        Prompts = new ObservableCollection<EditablePromptViewModel>(
            section.Prompts.Select(p => new EditablePromptViewModel(p, _parent)));

        AddPromptCommand = new RelayCommand(AddPrompt);
        AddSubsectionCommand = new RelayCommand(AddSubsection);
        RemoveSectionCommand = new RelayCommand(() => _parent.RemoveSection(this));
    }

    public string Id => Section.Id;

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                Section.Title = value;
                _parent.MarkAsChanged();
            }
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                Section.Description = value;
                _parent.MarkAsChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<EditableSubsectionViewModel> Subsections { get; }
    public ObservableCollection<EditablePromptViewModel> Prompts { get; }

    public ICommand AddPromptCommand { get; }
    public ICommand AddSubsectionCommand { get; }
    public ICommand RemoveSectionCommand { get; }

    private void AddPrompt()
    {
        var newPrompt = new Prompt
        {
            Id = $"{Section.Id}_prompt_{Prompts.Count + 1}",
            Label = "New Prompt",
            Hints = new PromptHints()
        };

        Section.Prompts.Add(newPrompt);
        Prompts.Add(new EditablePromptViewModel(newPrompt, _parent));
        _parent.MarkAsChanged();
    }

    private void AddSubsection()
    {
        var newSubsection = new Subsection
        {
            Id = $"{Section.Id}_subsection_{Subsections.Count + 1}",
            Title = "New Subsection",
            Prompts = new List<Prompt>()
        };

        Section.Subsections.Add(newSubsection);
        Subsections.Add(new EditableSubsectionViewModel(newSubsection, this));
        _parent.MarkAsChanged();
    }

    public void RemovePrompt(EditablePromptViewModel prompt)
    {
        Section.Prompts.Remove(prompt.Prompt);
        Prompts.Remove(prompt);
        _parent.MarkAsChanged();
    }

    public void RemoveSubsection(EditableSubsectionViewModel subsection)
    {
        Section.Subsections.Remove(subsection.Subsection);
        Subsections.Remove(subsection);
        _parent.MarkAsChanged();
    }
}

/// <summary>
/// ViewModel for editing a subsection.
/// </summary>
public class EditableSubsectionViewModel : ViewModelBase
{
    private readonly EditableSectionViewModel _parentSection;
    public readonly Subsection Subsection;
    private bool _isExpanded = true;
    private string _title;
    private string? _description;

    public EditableSubsectionViewModel(Subsection subsection, EditableSectionViewModel parentSection)
    {
        Subsection = subsection;
        _parentSection = parentSection;
        _title = subsection.Title;
        _description = subsection.Description;

        Prompts = new ObservableCollection<EditablePromptViewModel>(
            subsection.Prompts.Select(p => new EditablePromptViewModel(p, _parentSection._parent)));

        AddPromptCommand = new RelayCommand(AddPrompt);
        RemoveSubsectionCommand = new RelayCommand(() => _parentSection.RemoveSubsection(this));
    }

    public string Id => Subsection.Id;

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                Subsection.Title = value;
                _parentSection._parent.MarkAsChanged();
            }
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                Subsection.Description = value;
                _parentSection._parent.MarkAsChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<EditablePromptViewModel> Prompts { get; }

    public ICommand AddPromptCommand { get; }
    public ICommand RemoveSubsectionCommand { get; }

    private void AddPrompt()
    {
        var newPrompt = new Prompt
        {
            Id = $"{Subsection.Id}_prompt_{Prompts.Count + 1}",
            Label = "New Prompt",
            Hints = new PromptHints()
        };

        Subsection.Prompts.Add(newPrompt);
        Prompts.Add(new EditablePromptViewModel(newPrompt, _parentSection._parent));
        _parentSection._parent.MarkAsChanged();
    }

    public void RemovePrompt(EditablePromptViewModel prompt)
    {
        Subsection.Prompts.Remove(prompt.Prompt);
        Prompts.Remove(prompt);
        _parentSection._parent.MarkAsChanged();
    }
}

/// <summary>
/// ViewModel for editing a prompt.
/// </summary>
public class EditablePromptViewModel : ViewModelBase
{
    private readonly TemplateEditorViewModel _parent;
    public readonly Prompt Prompt;
    private string _label;
    private string? _placeholder;
    private string? _helpText;
    private string? _expectedDataType;

    public EditablePromptViewModel(Prompt prompt, TemplateEditorViewModel parent)
    {
        Prompt = prompt;
        _parent = parent;
        _label = prompt.Label;
        _placeholder = prompt.Hints.Placeholder;
        _helpText = prompt.Hints.HelpText;
        _expectedDataType = prompt.Hints.ExpectedDataType;
    }

    public string Id => Prompt.Id;

    public string Label
    {
        get => _label;
        set
        {
            if (SetProperty(ref _label, value))
            {
                Prompt.Label = value;
                _parent.MarkAsChanged();
            }
        }
    }

    public string? Placeholder
    {
        get => _placeholder;
        set
        {
            if (SetProperty(ref _placeholder, value))
            {
                Prompt.Hints.Placeholder = value;
                _parent.MarkAsChanged();
            }
        }
    }

    public string? HelpText
    {
        get => _helpText;
        set
        {
            if (SetProperty(ref _helpText, value))
            {
                Prompt.Hints.HelpText = value;
                _parent.MarkAsChanged();
            }
        }
    }

    public string? ExpectedDataType
    {
        get => _expectedDataType;
        set
        {
            if (SetProperty(ref _expectedDataType, value))
            {
                Prompt.Hints.ExpectedDataType = value;
                _parent.MarkAsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets suggested values as a comma-separated string for easier editing.
    /// </summary>
    public string SuggestedValuesText
    {
        get => string.Join(", ", Prompt.Hints.SuggestedValues);
        set
        {
            var newValues = value
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (!newValues.SequenceEqual(Prompt.Hints.SuggestedValues))
            {
                Prompt.Hints.SuggestedValues.Clear();
                foreach (var item in newValues)
                {
                    Prompt.Hints.SuggestedValues.Add(item);
                }
                OnPropertyChanged();
                _parent.MarkAsChanged();
            }
        }
    }
}
