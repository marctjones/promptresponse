using PromptResponse.Core.Models;
using PromptResponse.Core.Services;
using PromptResponse.Core.Services.Certificates;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for filling out a form.
/// </summary>
public class FormFillingViewModel : ViewModelBase
{
    private readonly AprDocument _document;
    private readonly ISignatureService? _signatureService;
    private readonly ICertificateStore? _certificateStore;
    private bool _hasUnsavedChanges;
    private string _statusMessage = string.Empty;
    private string _mode = "Filling Form";
    private bool _isReadOnly = false;

    public FormFillingViewModel(
        AprDocument document,
        ISignatureService? signatureService = null,
        ICertificateStore? certificateStore = null)
    {
        _document = document;
        _signatureService = signatureService;
        _certificateStore = certificateStore;

        // Check if form is signed (making it read-only)
        var hasFormSignatures = document.Metadata.FormSignatures?.Count > 0;
        _isReadOnly = hasFormSignatures;

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

        // Initialize commands
        SignFormCommand = new RelayCommand(
            async () => await SignFormAsync(),
            () => CanSignForm);

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

    /// <summary>
    /// Gets whether this form is read-only (signed forms cannot be edited).
    /// </summary>
    public bool IsReadOnly => _isReadOnly;

    /// <summary>
    /// Gets whether this form can be edited (inverse of IsReadOnly).
    /// </summary>
    public bool IsEditable => !_isReadOnly;

    /// <summary>
    /// Gets whether the template this form is based on has been signed.
    /// </summary>
    public bool HasTemplateSignatures => _document.Metadata.TemplateSignatures?.Count > 0;

    /// <summary>
    /// Gets whether this filled form has been signed.
    /// </summary>
    public bool HasFormSignatures => _document.Metadata.FormSignatures?.Count > 0;

    /// <summary>
    /// Gets the template signatures (if any).
    /// </summary>
    public IReadOnlyList<DigitalSignature>? TemplateSignatures => _document.Metadata.TemplateSignatures;

    /// <summary>
    /// Gets the form signatures (if any).
    /// </summary>
    public IReadOnlyList<DigitalSignature>? FormSignatures => _document.Metadata.FormSignatures;

    /// <summary>
    /// Gets a user-friendly message about signatures.
    /// </summary>
    public string SignatureStatusMessage
    {
        get
        {
            if (HasFormSignatures)
            {
                var sig = _document.Metadata.FormSignatures![0];
                return $"✓ Signed by {sig.SignerName} on {sig.SignedAt:g} (Read-only)";
            }
            else if (HasTemplateSignatures)
            {
                var sig = _document.Metadata.TemplateSignatures![0];
                return $"ℹ Template signed by {sig.SignerName} on {sig.SignedAt:g}";
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets whether this form can be signed (services available, not already signed, and is a filled form).
    /// </summary>
    public bool CanSignForm => _signatureService != null
                               && _certificateStore != null
                               && !HasFormSignatures
                               && _document.DocumentType == DocumentType.FilledForm;

    /// <summary>
    /// Command to sign the filled form.
    /// </summary>
    public ICommand SignFormCommand { get; }

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

    /// <summary>
    /// Signs the filled form with a user-selected certificate.
    /// </summary>
    private async Task SignFormAsync()
    {
        if (_signatureService == null || _certificateStore == null)
        {
            StatusMessage = "Signature services not available";
            return;
        }

        try
        {
            // Get installed certificates
            var certificates = _certificateStore.GetCertificates().ToList();
            if (certificates.Count == 0)
            {
                StatusMessage = "No certificates found. Please generate or install a certificate first.";
                return;
            }

            // For now, use the first certificate with a private key
            // TODO: Show certificate selection dialog
            var cert = certificates.FirstOrDefault(c => c.HasPrivateKey);
            if (cert == null)
            {
                StatusMessage = "No certificate with private key found";
                return;
            }

            // Sign the form
            StatusMessage = "Signing form...";
            var signature = _signatureService.SignFilledForm(_document, cert, "Signed via PromptResponse Desktop");

            // Add signature to document
            _document.Metadata.FormSignatures ??= new List<DigitalSignature>();
            _document.Metadata.FormSignatures.Add(signature);

            // Update read-only status
            _isReadOnly = true;
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(HasFormSignatures));
            OnPropertyChanged(nameof(FormSignatures));
            OnPropertyChanged(nameof(SignatureStatusMessage));
            OnPropertyChanged(nameof(CanSignForm));

            // Mark as having unsaved changes
            HasUnsavedChanges = true;
            StatusMessage = $"✓ Form signed by {signature.SignerName}. Please save the document.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error signing form: {ex.Message}";
        }
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
    private bool _useSmartControl = true;

    public PromptViewModel(Prompt prompt)
    {
        _prompt = prompt;
    }

    public string Label => _prompt.Label;
    public string? Placeholder => _prompt.Hints.Placeholder;
    public string? HelpText => _prompt.Hints.HelpText;
    public string? ExpectedDataType => _prompt.Hints.ExpectedDataType;
    public List<string> SuggestedValues => _prompt.Hints.SuggestedValues;

    public bool HasSuggestedValues => SuggestedValues.Count > 0;
    public bool IsDateField => ExpectedDataType?.ToLowerInvariant() == "date";
    public bool IsChoiceField => ExpectedDataType?.ToLowerInvariant() == "choice" && HasSuggestedValues;
    public bool IsMultiChoiceField => ExpectedDataType?.ToLowerInvariant() == "multichoice" && HasSuggestedValues;
    public bool IsBooleanField => ExpectedDataType?.ToLowerInvariant() == "boolean";

    // For choice fields, use radio buttons if 2-5 options, dropdown if 6+
    public bool UseRadioButtons => IsChoiceField && SuggestedValues.Count >= 2 && SuggestedValues.Count <= 5;
    public bool UseDropdown => IsChoiceField && SuggestedValues.Count > 5;

    // AutoComplete should only show for fields with suggestions that are NOT choice/multichoice
    public bool ShowAutocomplete => HasSuggestedValues && !IsChoiceField && !IsMultiChoiceField;

    public bool HasSmartControl => HasSuggestedValues || IsDateField || IsChoiceField || IsMultiChoiceField || IsBooleanField;

    /// <summary>
    /// Controls whether to show smart controls (autocomplete, date picker) or plain text box.
    /// </summary>
    public bool UseSmartControl
    {
        get => _useSmartControl;
        set
        {
            if (SetProperty(ref _useSmartControl, value))
            {
                OnPropertyChanged(nameof(ShowSmartControl));
                OnPropertyChanged(nameof(ShowPlainTextBox));
            }
        }
    }

    public bool ShowSmartControl => _useSmartControl && HasSmartControl;
    public bool ShowPlainTextBox => !_useSmartControl || !HasSmartControl;

    /// <summary>
    /// For multichoice fields, use a multiline textbox to show all selected values clearly.
    /// </summary>
    public bool UseMultilineTextBox => IsMultiChoiceField || ExpectedDataType?.ToLowerInvariant() == "multiline";

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
