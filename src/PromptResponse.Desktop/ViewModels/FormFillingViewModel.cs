using PromptResponse.Core.Commands;
using PromptResponse.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using IInputCommand = System.Windows.Input.ICommand;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for filling out a form.
/// </summary>
public class FormFillingViewModel : ViewModelBase, IDisposable
{
    private readonly AprDocument _document;
    private readonly UndoRedoManager? _undoRedoManager;
    private bool _hasUnsavedChanges;
    private string _statusMessage = string.Empty;
    private string _mode = "Filling Form";
    private bool _isReadOnly = false;
    private readonly List<(PromptViewModel prompt, PropertyChangedEventHandler handler)> _eventSubscriptions = new();
    private bool _disposed;

    // Search and navigation
    private string _searchText = string.Empty;
    private ObservableCollection<SearchResultViewModel> _searchResults = new();
    private int _currentMatchIndex = -1;
    private bool _isSearchVisible = false;

    // Progress tracking
    private int _totalPrompts;
    private int _answeredPrompts;
    private double _progressPercentage;

    public FormFillingViewModel(AprDocument document, UndoRedoManager? undoRedoManager = null)
    {
        _document = document;
        _undoRedoManager = undoRedoManager;

        // Check if form is signed (making it read-only)
        var hasFormSignatures = document.Metadata.FormSignatures?.Count > 0;
        _isReadOnly = hasFormSignatures;

        Sections = new ObservableCollection<SectionViewModel>(
            document.Sections.Select(s => new SectionViewModel(s, undoRedoManager)));

        // Subscribe to property changes to track unsaved changes
        SubscribeToPromptChanges(Sections);

        // Initialize search commands
        ToggleSearchCommand = new RelayCommand(ToggleSearch);
        NextMatchCommand = new RelayCommand(GoToNextMatch, () => _searchResults.Count > 0);
        PreviousMatchCommand = new RelayCommand(GoToPreviousMatch, () => _searchResults.Count > 0);
        ClearSearchCommand = new RelayCommand(ClearSearch);

        // Calculate initial progress
        CalculateProgress();

        UpdateStatusMessage();
    }

    /// <summary>
    /// Refreshes all PromptViewModels from their underlying models.
    /// Called after undo/redo operations to sync the UI.
    /// </summary>
    public void RefreshFromModel()
    {
        foreach (var section in Sections)
        {
            RefreshSectionFromModel(section);
        }
        CalculateProgress();
    }

    private void RefreshSectionFromModel(SectionViewModel section)
    {
        foreach (var prompt in section.Prompts)
        {
            prompt.RefreshFromModel();
        }

        foreach (var childSection in section.Sections)
        {
            RefreshSectionFromModel(childSection);
        }
    }

    /// <summary>
    /// Recursively subscribes to property changes for all prompts in sections.
    /// </summary>
    private void SubscribeToPromptChanges(IEnumerable<SectionViewModel> sections)
    {
        foreach (var section in sections)
        {
            foreach (var prompt in section.Prompts)
            {
                PropertyChangedEventHandler handler = (s, e) =>
                {
                    HasUnsavedChanges = true;
                    // Recalculate progress when Response changes
                    if (e.PropertyName == nameof(PromptViewModel.Response))
                    {
                        CalculateProgress();
                    }
                };
                prompt.PropertyChanged += handler;
                _eventSubscriptions.Add((prompt, handler));
            }

            // Recursively subscribe to child sections
            SubscribeToPromptChanges(section.Sections);
        }
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

    // Search commands
    public IInputCommand ToggleSearchCommand { get; }
    public IInputCommand NextMatchCommand { get; }
    public IInputCommand PreviousMatchCommand { get; }
    public IInputCommand ClearSearchCommand { get; }

    /// <summary>
    /// Gets or sets whether the search panel is visible.
    /// </summary>
    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
    }

    /// <summary>
    /// Gets or sets the search text for filtering prompts.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                PerformSearch();
            }
        }
    }

    /// <summary>
    /// Gets the search results.
    /// </summary>
    public ObservableCollection<SearchResultViewModel> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
    }

    /// <summary>
    /// Gets the current match index (1-based for display).
    /// </summary>
    public int CurrentMatchIndex
    {
        get => _currentMatchIndex;
        private set
        {
            if (SetProperty(ref _currentMatchIndex, value))
            {
                OnPropertyChanged(nameof(MatchStatusText));
            }
        }
    }

    /// <summary>
    /// Gets the match status text (e.g., "3 of 10 matches").
    /// </summary>
    public string MatchStatusText
    {
        get
        {
            if (SearchResults.Count == 0)
            {
                return string.IsNullOrWhiteSpace(SearchText) ? string.Empty : "No matches";
            }
            return $"{CurrentMatchIndex + 1} of {SearchResults.Count} matches";
        }
    }

    // Progress tracking properties

    /// <summary>
    /// Gets the total number of prompts in the form.
    /// </summary>
    public int TotalPrompts
    {
        get => _totalPrompts;
        private set => SetProperty(ref _totalPrompts, value);
    }

    /// <summary>
    /// Gets the number of answered prompts.
    /// </summary>
    public int AnsweredPrompts
    {
        get => _answeredPrompts;
        private set
        {
            if (SetProperty(ref _answeredPrompts, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    /// <summary>
    /// Gets the progress percentage (0-100).
    /// </summary>
    public double ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, value);
    }

    /// <summary>
    /// Gets the progress text (e.g., "15 of 30 prompts answered").
    /// </summary>
    public string ProgressText => $"{AnsweredPrompts} of {TotalPrompts} prompts answered";

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

    #region S3 Submission Status

    /// <summary>
    /// Gets whether this form has S3 submission configured.
    /// </summary>
    public bool HasS3SubmissionConfig =>
        _document.Metadata.SubmissionConfig?.Type == "s3-presigned-post";

    /// <summary>
    /// Gets whether this form has a template source URL configured for updates.
    /// </summary>
    public bool HasTemplateSourceUrl =>
        !string.IsNullOrWhiteSpace(_document.Metadata.TemplateSourceUrl);

    /// <summary>
    /// Gets whether the S3 submission policy has expired.
    /// </summary>
    public bool IsSubmissionExpired =>
        HasS3SubmissionConfig &&
        _document.Metadata.SubmissionConfig?.ExpiresAt < DateTime.UtcNow;

    /// <summary>
    /// Gets whether the S3 submission policy is expiring soon (within 7 days).
    /// </summary>
    public bool IsSubmissionExpiringSoon
    {
        get
        {
            if (!HasS3SubmissionConfig) return false;
            var expiresAt = _document.Metadata.SubmissionConfig?.ExpiresAt;
            if (expiresAt == null) return false;
            var daysUntilExpiry = (expiresAt.Value - DateTime.UtcNow).TotalDays;
            return daysUntilExpiry > 0 && daysUntilExpiry < 7;
        }
    }

    /// <summary>
    /// Gets a user-friendly message about S3 submission status.
    /// </summary>
    public string SubmissionStatusText
    {
        get
        {
            if (!HasS3SubmissionConfig)
                return string.Empty;

            var config = _document.Metadata.SubmissionConfig!;

            if (IsSubmissionExpired)
                return "S3 submission EXPIRED - contact form provider";

            if (config.ExpiresAt.HasValue)
            {
                var remaining = config.ExpiresAt.Value - DateTime.UtcNow;
                if (remaining.TotalDays < 1)
                    return $"Submit to S3 (expires in {remaining.Hours}h)";
                if (remaining.TotalDays < 7)
                    return $"Submit to S3 (expires in {(int)remaining.TotalDays} days)";
                return $"Submit to S3 (expires {config.ExpiresAt.Value:MMM d})";
            }

            return "Submit to S3";
        }
    }

    /// <summary>
    /// Gets the S3 submission tooltip with more details.
    /// </summary>
    public string SubmissionTooltip
    {
        get
        {
            if (!HasS3SubmissionConfig)
                return "S3 submission is not configured for this form";

            if (IsSubmissionExpired)
                return "The S3 submission policy has expired. Please contact the form provider for an updated form.";

            var config = _document.Metadata.SubmissionConfig!;
            var tooltip = "Click to submit this completed form to S3 storage";

            if (config.ExpiresAt.HasValue)
            {
                tooltip += $"\n\nExpires: {config.ExpiresAt.Value:g}";
            }

            return tooltip;
        }
    }

    #endregion

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

    #region Search and Navigation Methods

    /// <summary>
    /// Toggles the visibility of the search panel.
    /// </summary>
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible)
        {
            ClearSearch();
        }
    }

    /// <summary>
    /// Clears the search text and results.
    /// </summary>
    private void ClearSearch()
    {
        SearchText = string.Empty;
        SearchResults.Clear();
        CurrentMatchIndex = -1;
        ClearAllHighlights();
    }

    /// <summary>
    /// Performs a search across all prompts.
    /// </summary>
    private void PerformSearch()
    {
        SearchResults.Clear();
        CurrentMatchIndex = -1;
        ClearAllHighlights();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            OnPropertyChanged(nameof(MatchStatusText));
            return;
        }

        var searchLower = SearchText.ToLowerInvariant();
        var results = new List<SearchResultViewModel>();

        // Search through all sections recursively
        foreach (var section in Sections)
        {
            SearchInSection(section, searchLower, results);
        }

        foreach (var result in results)
        {
            SearchResults.Add(result);
        }

        // Highlight all matches
        foreach (var result in SearchResults)
        {
            result.Prompt.IsHighlighted = true;
        }

        if (SearchResults.Count > 0)
        {
            CurrentMatchIndex = 0;
            SearchResults[0].Prompt.IsCurrentMatch = true;
        }

        OnPropertyChanged(nameof(MatchStatusText));
    }

    /// <summary>
    /// Searches for prompts within a section and its child sections.
    /// </summary>
    private void SearchInSection(SectionViewModel section, string searchLower, List<SearchResultViewModel> results)
    {
        foreach (var prompt in section.Prompts)
        {
            if (MatchesSearch(prompt, searchLower))
            {
                results.Add(new SearchResultViewModel(prompt, section));
            }
        }

        // Search child sections
        foreach (var childSection in section.Sections)
        {
            SearchInSection(childSection, searchLower, results);
        }
    }

    /// <summary>
    /// Checks if a prompt matches the search criteria.
    /// </summary>
    private bool MatchesSearch(PromptViewModel prompt, string searchLower)
    {
        // Search by label
        if (prompt.Label.ToLowerInvariant().Contains(searchLower))
            return true;

        // Search by prompt ID (from the model)
        if (prompt.Id?.ToLowerInvariant().Contains(searchLower) == true)
            return true;

        // Search by response text
        if (!string.IsNullOrWhiteSpace(prompt.Response) &&
            prompt.Response.ToLowerInvariant().Contains(searchLower))
            return true;

        return false;
    }

    /// <summary>
    /// Navigates to the next search match.
    /// </summary>
    private void GoToNextMatch()
    {
        if (SearchResults.Count == 0) return;

        // Clear current match highlight
        if (CurrentMatchIndex >= 0 && CurrentMatchIndex < SearchResults.Count)
        {
            SearchResults[CurrentMatchIndex].Prompt.IsCurrentMatch = false;
        }

        // Move to next
        CurrentMatchIndex = (CurrentMatchIndex + 1) % SearchResults.Count;
        SearchResults[CurrentMatchIndex].Prompt.IsCurrentMatch = true;

        // Request navigation to the prompt
        OnNavigateToPromptRequested?.Invoke(SearchResults[CurrentMatchIndex].Prompt);
    }

    /// <summary>
    /// Navigates to the previous search match.
    /// </summary>
    private void GoToPreviousMatch()
    {
        if (SearchResults.Count == 0) return;

        // Clear current match highlight
        if (CurrentMatchIndex >= 0 && CurrentMatchIndex < SearchResults.Count)
        {
            SearchResults[CurrentMatchIndex].Prompt.IsCurrentMatch = false;
        }

        // Move to previous
        CurrentMatchIndex = CurrentMatchIndex <= 0 ? SearchResults.Count - 1 : CurrentMatchIndex - 1;
        SearchResults[CurrentMatchIndex].Prompt.IsCurrentMatch = true;

        // Request navigation to the prompt
        OnNavigateToPromptRequested?.Invoke(SearchResults[CurrentMatchIndex].Prompt);
    }

    /// <summary>
    /// Clears all search highlights from prompts.
    /// </summary>
    private void ClearAllHighlights()
    {
        foreach (var section in Sections)
        {
            ClearHighlightsInSection(section);
        }
    }

    /// <summary>
    /// Clears highlights from prompts in a section and its children.
    /// </summary>
    private void ClearHighlightsInSection(SectionViewModel section)
    {
        foreach (var prompt in section.Prompts)
        {
            prompt.IsHighlighted = false;
            prompt.IsCurrentMatch = false;
        }

        foreach (var childSection in section.Sections)
        {
            ClearHighlightsInSection(childSection);
        }
    }

    /// <summary>
    /// Event raised when navigation to a specific prompt is requested.
    /// </summary>
    public event Action<PromptViewModel>? OnNavigateToPromptRequested;

    #endregion

    #region Progress Tracking Methods

    /// <summary>
    /// Calculates and updates the progress statistics.
    /// </summary>
    public void CalculateProgress()
    {
        var total = 0;
        var answered = 0;

        foreach (var section in Sections)
        {
            CountPromptsInSection(section, ref total, ref answered);
        }

        TotalPrompts = total;
        AnsweredPrompts = answered;
        ProgressPercentage = total > 0 ? (double)answered / total * 100 : 0;
    }

    /// <summary>
    /// Counts prompts in a section and its children.
    /// </summary>
    private void CountPromptsInSection(SectionViewModel section, ref int total, ref int answered)
    {
        foreach (var prompt in section.Prompts)
        {
            total++;
            if (!string.IsNullOrWhiteSpace(prompt.Response))
            {
                answered++;
            }
        }

        foreach (var childSection in section.Sections)
        {
            CountPromptsInSection(childSection, ref total, ref answered);
        }
    }

    #endregion

    /// <summary>
    /// Disposes the ViewModel and unsubscribes from all event handlers to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Unsubscribe from all prompt property changed events
        foreach (var (prompt, handler) in _eventSubscriptions)
        {
            prompt.PropertyChanged -= handler;
        }
        _eventSubscriptions.Clear();

        _disposed = true;
    }
}

/// <summary>
/// ViewModel for a section with recursive child section support.
/// </summary>
public class SectionViewModel : ViewModelBase
{
    private readonly Section _section;
    private bool _isExpanded = true;

    public SectionViewModel(Section section, UndoRedoManager? undoRedoManager = null)
    {
        _section = section;

        // Recursively create child section ViewModels
        Sections = new ObservableCollection<SectionViewModel>(
            section.Sections.Select(s => new SectionViewModel(s, undoRedoManager)));

        Prompts = new ObservableCollection<PromptViewModel>(
            section.Prompts.Select(p => new PromptViewModel(p, undoRedoManager)));
    }

    public Section Model => _section;
    public string Title => _section.Title;
    public string? Description => _section.Description;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// Child sections within this section.
    /// </summary>
    public ObservableCollection<SectionViewModel> Sections { get; }

    /// <summary>
    /// Prompts directly in this section.
    /// </summary>
    public ObservableCollection<PromptViewModel> Prompts { get; }
}

/// <summary>
/// ViewModel for a prompt.
/// </summary>
public class PromptViewModel : ViewModelBase
{
    private readonly Prompt _prompt;
    private readonly UndoRedoManager? _undoRedoManager;
    private bool _useSmartControl = true;
    private bool _isHighlighted = false;
    private bool _isCurrentMatch = false;
    private bool _isRefreshing = false;

    public PromptViewModel(Prompt prompt, UndoRedoManager? undoRedoManager = null)
    {
        _prompt = prompt;
        _undoRedoManager = undoRedoManager;
    }

    /// <summary>
    /// Gets the underlying prompt model.
    /// </summary>
    public Prompt Model => _prompt;

    /// <summary>
    /// Gets the prompt ID.
    /// </summary>
    public string Id => _prompt.Id;

    public string Label => _prompt.Label;
    public string? Placeholder => _prompt.Hints.Placeholder;
    public string? HelpText => _prompt.Hints.HelpText;
    public string? ExpectedDataType => _prompt.Hints.ExpectedDataType;
    public List<string> SuggestedValues => _prompt.Hints.SuggestedValues;

    // Table-related properties
    public TableDefinition? TableDefinition => _prompt.Hints.TableDefinition;
    public bool IsFixedTable => TableDefinition?.IsFixedTable ?? false;
    public bool IsDynamicTable => TableDefinition?.IsDynamicTable ?? false;
    public List<TableColumn> TableColumns => TableDefinition?.Columns ?? new List<TableColumn>();
    public List<FixedRow> TableFixedRows => TableDefinition?.FixedRows ?? new List<FixedRow>();

    /// <summary>
    /// Gets or sets whether this prompt is highlighted as a search match.
    /// </summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    /// <summary>
    /// Gets or sets whether this prompt is the current search match.
    /// </summary>
    public bool IsCurrentMatch
    {
        get => _isCurrentMatch;
        set => SetProperty(ref _isCurrentMatch, value);
    }

    public bool HasSuggestedValues => SuggestedValues.Count > 0;
    private bool IsMultilineField => ExpectedDataType?.ToLowerInvariant() == "multiline";

    // Table field - highest priority, shows JSON editor
    public bool IsTableField => ExpectedDataType?.ToLowerInvariant() == "table";

    // Date field - shows date picker (only if no suggested values to avoid overlap)
    public bool IsDateField => ExpectedDataType?.ToLowerInvariant() == "date" && !HasSuggestedValues;

    // Time field - shows time picker
    public bool IsTimeField => ExpectedDataType?.ToLowerInvariant() == "time" && !HasSuggestedValues;

    // DateTime field - shows combined date and time picker
    public bool IsDateTimeField => ExpectedDataType?.ToLowerInvariant() == "datetime" && !HasSuggestedValues;

    // Boolean field - shows single checkbox (only if no suggested values)
    public bool IsBooleanField => ExpectedDataType?.ToLowerInvariant() == "boolean" && !HasSuggestedValues;

    // Password field - shows masked input with toggle
    public bool IsPasswordField => ExpectedDataType?.ToLowerInvariant() == "password";
    private bool _showPassword;
    public bool ShowPassword
    {
        get => _showPassword;
        set => SetProperty(ref _showPassword, value);
    }

    // Color field - shows color picker
    public bool IsColorField => ExpectedDataType?.ToLowerInvariant() == "color";

    // Range/slider field - shows slider control
    public bool IsRangeField => ExpectedDataType?.ToLowerInvariant() == "range";
    public double RangeMin => GetHintDouble("min", 0);
    public double RangeMax => GetHintDouble("max", 100);
    public double RangeStep => GetHintDouble("step", 1);
    public double RangeValue
    {
        get => double.TryParse(Response, out var v) ? v : RangeMin;
        set => Response = value.ToString();
    }
    private double GetHintDouble(string key, double defaultValue)
    {
        // TODO: Add support for range hints in Prompt model
        return defaultValue;
    }

    // File attachment field
    public bool IsFileField => ExpectedDataType?.ToLowerInvariant() == "file";
    public string FileName => string.IsNullOrEmpty(Response) ? "" : System.IO.Path.GetFileName(Response);

    // Signature field - digital signature capture
    public bool IsSignatureField => ExpectedDataType?.ToLowerInvariant() == "signature";
    public bool HasSignature => !string.IsNullOrEmpty(Response) && Response.StartsWith("data:image");

    // Multichoice field - multiple selection with checkboxes
    public bool IsMultichoiceField => ExpectedDataType?.ToLowerInvariant() == "multichoice" && HasSuggestedValues;
    public List<string> SelectedChoices
    {
        get => string.IsNullOrEmpty(Response) ? new List<string>() : Response.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        set => Response = string.Join(", ", value);
    }
    public bool IsChoiceSelected(string choice) => SelectedChoices.Contains(choice);
    public void ToggleChoice(string choice)
    {
        var choices = SelectedChoices;
        if (choices.Contains(choice))
            choices.Remove(choice);
        else
            choices.Add(choice);
        SelectedChoices = choices;
        OnPropertyChanged(nameof(Response));
    }

    // Email field - text with validation indicator
    public bool IsEmailField => ExpectedDataType?.ToLowerInvariant() == "email";
    public bool IsValidEmail => string.IsNullOrEmpty(Response) || System.Text.RegularExpressions.Regex.IsMatch(Response, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    // URL field - text with validation indicator
    public bool IsUrlField => ExpectedDataType?.ToLowerInvariant() == "url";
    public bool IsValidUrl => string.IsNullOrEmpty(Response) || Uri.TryCreate(Response, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Pattern validation field - shows validation indicator based on regex pattern
    public string? ValidationPattern => _prompt.Hints.ValidationPattern;
    public bool HasValidationPattern => !string.IsNullOrEmpty(ValidationPattern) && !IsFormattedField;
    public bool IsValidPattern
    {
        get
        {
            if (string.IsNullOrEmpty(ValidationPattern))
                return true;
            if (string.IsNullOrEmpty(Response))
                return true; // Empty is valid (unless required)
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(Response, ValidationPattern);
            }
            catch
            {
                return false;
            }
        }
    }

    // Number field - shows numeric-only input
    public bool IsNumberField => ExpectedDataType?.ToLowerInvariant() is "number" or "currency";

    // Auto-formatted fields (phone, ssn, ein, creditcard, zipcode, currency, number)
    private static readonly HashSet<string> FormattedDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone", "ssn", "ein", "creditcard", "zipcode", "currency", "number"
    };
    public bool IsFormattedField => ExpectedDataType != null && FormattedDataTypes.Contains(ExpectedDataType);

    // Fields with limited options show radio buttons (2-5) or dropdown (6+)
    // Only if NOT a special type field (table, date with no suggestions, etc.)
    private bool CanShowSelectionControl => HasSuggestedValues && !IsMultilineField && !IsTableField && !IsFormattedField && !IsMultichoiceField;
    public bool UseRadioButtons => CanShowSelectionControl && SuggestedValues.Count >= 2 && SuggestedValues.Count <= 5;
    public bool UseDropdown => CanShowSelectionControl && SuggestedValues.Count > 5;

    // AutoComplete is disabled - we use radio/dropdown for all suggestion cases
    public bool ShowAutocomplete => false;

    public bool HasSmartControl => UseRadioButtons || UseDropdown || IsDateField || IsTimeField || IsDateTimeField ||
                                   IsBooleanField || IsTableField || IsFormattedField || IsPasswordField ||
                                   IsColorField || IsRangeField || IsFileField || IsSignatureField ||
                                   IsMultichoiceField || IsEmailField || IsUrlField || HasValidationPattern;

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
    /// Use multiline textbox for multiline text fields.
    /// </summary>
    public bool UseMultilineTextBox => IsMultilineField;

    public string Response
    {
        get => _prompt.Response;
        set
        {
            if (_prompt.Response != value)
            {
                // Skip command creation during refresh operations
                if (_isRefreshing)
                {
                    _prompt.Response = value;
                    OnPropertyChanged();
                    NotifyValidationProperties();
                    return;
                }

                // Use UndoRedoManager if available
                if (_undoRedoManager != null)
                {
                    var command = new SetPromptResponseCommand(_prompt, value);
                    _undoRedoManager.ExecuteCommand(command);
                    OnPropertyChanged();
                    NotifyValidationProperties();
                }
                else
                {
                    // Fallback to direct assignment
                    _prompt.Response = value;
                    OnPropertyChanged();
                    NotifyValidationProperties();
                }
            }
        }
    }

    /// <summary>
    /// Notifies validation-related properties when Response changes.
    /// </summary>
    private void NotifyValidationProperties()
    {
        OnPropertyChanged(nameof(IsValidEmail));
        OnPropertyChanged(nameof(IsValidUrl));
        OnPropertyChanged(nameof(IsValidPattern));
    }

    /// <summary>
    /// Refreshes the Response property from the underlying model.
    /// Called after undo/redo operations.
    /// </summary>
    public void RefreshFromModel()
    {
        _isRefreshing = true;
        try
        {
            OnPropertyChanged(nameof(Response));
            // Also refresh table data if this is a table field
            if (IsTableField)
            {
                InitializeTableData();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // Table data for editable grid
    private ObservableCollection<TableRowViewModel>? _tableRows;
    public ObservableCollection<TableRowViewModel> TableRows
    {
        get
        {
            if (_tableRows == null && IsTableField)
            {
                InitializeTableData();
            }
            return _tableRows ?? new ObservableCollection<TableRowViewModel>();
        }
    }

    private void InitializeTableData()
    {
        if (TableDefinition == null) return;

        _tableRows = new ObservableCollection<TableRowViewModel>();

        if (IsFixedTable && TableDefinition.FixedRows != null)
        {
            // Parse existing response JSON for fixed table
            Dictionary<string, Dictionary<string, string>>? existingData = null;
            if (!string.IsNullOrWhiteSpace(Response))
            {
                try
                {
                    existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(Response);
                }
                catch { /* Ignore parse errors, start fresh */ }
            }

            foreach (var fixedRow in TableDefinition.FixedRows)
            {
                var cells = new ObservableCollection<TableCellViewModel>();
                foreach (var column in TableDefinition.Columns)
                {
                    var existingValue = "";
                    if (existingData != null &&
                        existingData.TryGetValue(fixedRow.Id, out var rowData) &&
                        rowData.TryGetValue(column.Id, out var cellValue))
                    {
                        existingValue = cellValue;
                    }

                    var cell = new TableCellViewModel(fixedRow.Id, column.Id, column.Label, column.Placeholder, existingValue);
                    cell.PropertyChanged += OnTableCellChanged;
                    cells.Add(cell);
                }
                _tableRows.Add(new TableRowViewModel(fixedRow.Id, fixedRow.Label, cells));
            }
        }
        else if (IsDynamicTable)
        {
            // Parse existing response JSON for dynamic table (array)
            List<Dictionary<string, string>>? existingData = null;
            if (!string.IsNullOrWhiteSpace(Response))
            {
                try
                {
                    existingData = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(Response);
                }
                catch { /* Ignore parse errors, start fresh */ }
            }

            if (existingData != null && existingData.Count > 0)
            {
                for (int i = 0; i < existingData.Count; i++)
                {
                    var rowData = existingData[i];
                    AddDynamicRow(rowData, i + 1);
                }
            }
            else
            {
                // Start with minimum rows or 1 row
                var minRows = TableDefinition.DynamicRows?.MinRows ?? 1;
                for (int i = 0; i < Math.Max(1, minRows); i++)
                {
                    AddDynamicRow(null, i + 1);
                }
            }
        }

        OnPropertyChanged(nameof(TableRows));
        OnPropertyChanged(nameof(CanAddRow));
        OnPropertyChanged(nameof(CanRemoveRow));
    }

    private void AddDynamicRow(Dictionary<string, string>? existingData, int rowNumber)
    {
        if (TableDefinition == null) return;

        var rowId = $"row_{rowNumber}";
        var rowLabel = $"{TableDefinition.DynamicRows?.RowLabel ?? "Row"} {rowNumber}";
        var cells = new ObservableCollection<TableCellViewModel>();

        foreach (var column in TableDefinition.Columns)
        {
            var existingValue = "";
            if (existingData != null && existingData.TryGetValue(column.Id, out var cellValue))
            {
                existingValue = cellValue;
            }

            var cell = new TableCellViewModel(rowId, column.Id, column.Label, column.Placeholder, existingValue);
            cell.PropertyChanged += OnTableCellChanged;
            cells.Add(cell);
        }

        _tableRows?.Add(new TableRowViewModel(rowId, rowLabel, cells));
    }

    public bool CanAddRow => IsDynamicTable && _tableRows != null &&
                             _tableRows.Count < (TableDefinition?.DynamicRows?.MaxRows ?? 100);

    public bool CanRemoveRow => IsDynamicTable && _tableRows != null &&
                                _tableRows.Count > (TableDefinition?.DynamicRows?.MinRows ?? 0);

    public void AddRow()
    {
        if (!CanAddRow || _tableRows == null) return;

        AddDynamicRow(null, _tableRows.Count + 1);
        OnPropertyChanged(nameof(TableRows));
        OnPropertyChanged(nameof(CanAddRow));
        OnPropertyChanged(nameof(CanRemoveRow));
        SerializeTableToResponse();
    }

    public void RemoveRow()
    {
        if (!CanRemoveRow || _tableRows == null || _tableRows.Count == 0) return;

        var lastRow = _tableRows[^1];
        foreach (var cell in lastRow.Cells)
        {
            cell.PropertyChanged -= OnTableCellChanged;
        }
        _tableRows.RemoveAt(_tableRows.Count - 1);

        OnPropertyChanged(nameof(TableRows));
        OnPropertyChanged(nameof(CanAddRow));
        OnPropertyChanged(nameof(CanRemoveRow));
        SerializeTableToResponse();
    }

    private void OnTableCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableCellViewModel.Value))
        {
            SerializeTableToResponse();
        }
    }

    private void SerializeTableToResponse()
    {
        if (_tableRows == null) return;

        string json;
        if (IsFixedTable)
        {
            var data = new Dictionary<string, Dictionary<string, string>>();
            foreach (var row in _tableRows)
            {
                var rowData = new Dictionary<string, string>();
                foreach (var cell in row.Cells)
                {
                    if (!string.IsNullOrEmpty(cell.Value))
                    {
                        rowData[cell.ColumnId] = cell.Value;
                    }
                }
                if (rowData.Count > 0)
                {
                    data[row.RowId] = rowData;
                }
            }
            json = data.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = false })
                : "";
        }
        else // Dynamic table
        {
            var data = new List<Dictionary<string, string>>();
            foreach (var row in _tableRows)
            {
                var rowData = new Dictionary<string, string>();
                foreach (var cell in row.Cells)
                {
                    rowData[cell.ColumnId] = cell.Value ?? "";
                }
                data.Add(rowData);
            }
            json = data.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = false })
                : "[]";
        }

        // Update the response without triggering re-initialization
        _isRefreshing = true;
        try
        {
            Response = json;
        }
        finally
        {
            _isRefreshing = false;
        }
    }
}

/// <summary>
/// ViewModel for a table row.
/// </summary>
public class TableRowViewModel : ViewModelBase
{
    public TableRowViewModel(string rowId, string label, ObservableCollection<TableCellViewModel> cells)
    {
        RowId = rowId;
        Label = label;
        Cells = cells;
    }

    public string RowId { get; }
    public string Label { get; }
    public ObservableCollection<TableCellViewModel> Cells { get; }
}

/// <summary>
/// ViewModel for a single table cell.
/// </summary>
public class TableCellViewModel : ViewModelBase
{
    private string _value;

    public TableCellViewModel(string rowId, string columnId, string columnLabel, string? placeholder, string value)
    {
        RowId = rowId;
        ColumnId = columnId;
        ColumnLabel = columnLabel;
        Placeholder = placeholder ?? "";
        _value = value;
    }

    public string RowId { get; }
    public string ColumnId { get; }
    public string ColumnLabel { get; }
    public string Placeholder { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>
/// ViewModel for a search result item.
/// </summary>
public class SearchResultViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the SearchResultViewModel class.
    /// </summary>
    /// <param name="prompt">The matching prompt.</param>
    /// <param name="section">The section containing the prompt.</param>
    public SearchResultViewModel(PromptViewModel prompt, SectionViewModel section)
    {
        Prompt = prompt;
        Section = section;
    }

    /// <summary>
    /// Gets the matching prompt.
    /// </summary>
    public PromptViewModel Prompt { get; }

    /// <summary>
    /// Gets the section containing the prompt.
    /// </summary>
    public SectionViewModel Section { get; }

    /// <summary>
    /// Gets the display text for the search result.
    /// </summary>
    public string DisplayText => $"{Prompt.Label} ({Section.Title})";
}
