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
            document.Sections.Select(s => new EditableSectionViewModel(s, this, null)));

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
            Sections = new List<Section>()
        };

        _document.Sections.Add(newSection);
        Sections.Add(new EditableSectionViewModel(newSection, this, null));
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
/// ViewModel for editing a section with recursive child section support.
/// </summary>
public class EditableSectionViewModel : ViewModelBase
{
    internal readonly TemplateEditorViewModel _root;
    private readonly EditableSectionViewModel? _parentSection;
    public readonly Section Section;
    private bool _isExpanded = true;
    private string _title;
    private string? _description;

    public EditableSectionViewModel(Section section, TemplateEditorViewModel root, EditableSectionViewModel? parentSection)
    {
        Section = section;
        _root = root;
        _parentSection = parentSection;
        _title = section.Title;
        _description = section.Description;

        // Recursively create child section ViewModels
        Sections = new ObservableCollection<EditableSectionViewModel>(
            section.Sections.Select(s => new EditableSectionViewModel(s, root, this)));
        Prompts = new ObservableCollection<EditablePromptViewModel>(
            section.Prompts.Select(p => new EditablePromptViewModel(p, root)));

        AddPromptCommand = new RelayCommand(AddPrompt);
        AddSectionCommand = new RelayCommand(AddChildSection);
        RemoveSectionCommand = new RelayCommand(RemoveThisSection);
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
                _root.MarkAsChanged();
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
                _root.MarkAsChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// Child sections within this section.
    /// </summary>
    public ObservableCollection<EditableSectionViewModel> Sections { get; }

    /// <summary>
    /// Prompts directly in this section.
    /// </summary>
    public ObservableCollection<EditablePromptViewModel> Prompts { get; }

    public ICommand AddPromptCommand { get; }
    public ICommand AddSectionCommand { get; }
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
        Prompts.Add(new EditablePromptViewModel(newPrompt, _root));
        _root.MarkAsChanged();
    }

    private void AddChildSection()
    {
        var newSection = new Section
        {
            Id = $"{Section.Id}_section_{Sections.Count + 1}",
            Title = "New Section",
            Prompts = new List<Prompt>(),
            Sections = new List<Section>()
        };

        Section.Sections.Add(newSection);
        Sections.Add(new EditableSectionViewModel(newSection, _root, this));
        _root.MarkAsChanged();
    }

    private void RemoveThisSection()
    {
        if (_parentSection != null)
        {
            // Remove from parent section
            _parentSection.RemoveChildSection(this);
        }
        else
        {
            // Remove from root
            _root.RemoveSection(this);
        }
    }

    public void RemovePrompt(EditablePromptViewModel prompt)
    {
        Section.Prompts.Remove(prompt.Prompt);
        Prompts.Remove(prompt);
        _root.MarkAsChanged();
    }

    public void RemoveChildSection(EditableSectionViewModel childSection)
    {
        Section.Sections.Remove(childSection.Section);
        Sections.Remove(childSection);
        _root.MarkAsChanged();
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
