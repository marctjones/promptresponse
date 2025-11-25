using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Services;
using PromptResponse.Core.Services.Certificates;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.Views;
using IInputCommand = System.Windows.Input.ICommand;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IFileService _fileService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ICertificateStore _certificateStore;
    private readonly ISignatureService _signatureService;
    private readonly IS3BrowserService _s3BrowserService;
    private readonly IS3SubmissionService _s3SubmissionService;
    private readonly ITemplateGalleryService _templateGalleryService;
    private readonly ITemplatePublishingService _templatePublishingService;
    private readonly S3PolicyGenerator _s3PolicyGenerator;
    private readonly ITemplateUpdateService _templateUpdateService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly UndoRedoManager _undoRedoManager;
    private AprDocument? _currentDocument;
    private FormFillingViewModel? _formFillingViewModel;
    private TemplateEditorViewModel? _templateEditorViewModel;
    private string _title = "PromptResponse";
    private bool _isEditingTemplate = false; // Track if we're editing a template vs filling a form
    private bool _canUndo;
    private bool _canRedo;
    private string _undoDescription = string.Empty;
    private string _redoDescription = string.Empty;

    public MainWindowViewModel(
        IFileService fileService,
        ISettingsService settingsService,
        IDialogService dialogService,
        ICertificateGenerator certificateGenerator,
        ICertificateStore certificateStore,
        ISignatureService signatureService,
        IS3BrowserService s3BrowserService,
        IS3SubmissionService s3SubmissionService,
        ITemplateGalleryService templateGalleryService,
        ITemplatePublishingService templatePublishingService,
        S3PolicyGenerator s3PolicyGenerator,
        ITemplateUpdateService templateUpdateService,
        ILogger<MainWindowViewModel> logger)
    {
        _fileService = fileService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _certificateGenerator = certificateGenerator;
        _certificateStore = certificateStore;
        _signatureService = signatureService;
        _s3BrowserService = s3BrowserService;
        _s3SubmissionService = s3SubmissionService;
        _templateGalleryService = templateGalleryService;
        _templatePublishingService = templatePublishingService;
        _s3PolicyGenerator = s3PolicyGenerator;
        _templateUpdateService = templateUpdateService;
        _logger = logger;

        _logger.LogInformation("MainWindowViewModel constructor called");
        _logger.LogDebug("  FileService type: {Type}", fileService.GetType().Name);
        _logger.LogDebug("  SettingsService type: {Type}", settingsService.GetType().Name);

        // Initialize UndoRedoManager
        _undoRedoManager = new UndoRedoManager();
        _undoRedoManager.StateChanged += OnUndoRedoStateChanged;

        // Commands
        _logger.LogDebug("Setting up commands...");
        OpenCommand = new RelayCommand(async () => await OpenFileAsync(forFilling: true));
        OpenTemplateForEditingCommand = new RelayCommand(async () => await OpenFileAsync(forFilling: false));
        NewTemplateCommand = new RelayCommand(CreateNewTemplate);
        SaveCommand = new RelayCommand(async () => await SaveFileAsync(), () => _currentDocument != null);
        SaveAsCommand = new RelayCommand(async () => await SaveFileAsAsync(), () => _currentDocument != null);
        CloseCommand = new RelayCommand(CloseDocument, () => _currentDocument != null);
        SwitchToTemplateEditingCommand = new RelayCommand(SwitchToTemplateEditing, () => _currentDocument != null && !_isEditingTemplate);
        SwitchToFormFillingCommand = new RelayCommand(SwitchToFormFilling, () => _currentDocument != null && _isEditingTemplate);

        // Undo/Redo commands
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);

        // Theme commands
        SetLightThemeCommand = new RelayCommand(() => SetTheme(ThemeVariant.Light));
        SetDarkThemeCommand = new RelayCommand(() => SetTheme(ThemeVariant.Dark));
        SetSystemThemeCommand = new RelayCommand(() => SetTheme(ThemeVariant.Default));
        SetCustomThemeCommand = new RelayCommand(SetCustomTheme);

        // Tools commands
        OpenCertificateManagementCommand = new RelayCommand(OpenCertificateManagement);
        OpenS3BrowserCommand = new RelayCommand(OpenS3Browser);

        // S3 commands
        SubmitToS3Command = new RelayCommand(async () => await SubmitToS3Async(), CanSubmitToS3);
        BrowseTemplateGalleryCommand = new RelayCommand(BrowseTemplateGallery);
        PublishTemplateCommand = new RelayCommand(async () => await PublishTemplateAsync(), CanPublishTemplate);
        ConfigureS3Command = new RelayCommand(OpenS3ConfigurationDialog, CanConfigureS3);

        // Template update commands
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(), CanCheckForUpdates);

        _logger.LogInformation("MainWindowViewModel initialized successfully");
    }

    /// <summary>
    /// Gets the UndoRedoManager for tracking command history.
    /// </summary>
    public UndoRedoManager UndoRedoManager => _undoRedoManager;

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo
    {
        get => _canUndo;
        private set
        {
            if (SetProperty(ref _canUndo, value))
            {
                (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo
    {
        get => _canRedo;
        private set
        {
            if (SetProperty(ref _canRedo, value))
            {
                (RedoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the description of the command that would be undone.
    /// </summary>
    public string UndoDescription
    {
        get => _undoDescription;
        private set => SetProperty(ref _undoDescription, value);
    }

    /// <summary>
    /// Gets the description of the command that would be redone.
    /// </summary>
    public string RedoDescription
    {
        get => _redoDescription;
        private set => SetProperty(ref _redoDescription, value);
    }

    private void OnUndoRedoStateChanged(object? sender, EventArgs e)
    {
        CanUndo = _undoRedoManager.CanUndo;
        CanRedo = _undoRedoManager.CanRedo;
        UndoDescription = _undoRedoManager.GetUndoDescription() ?? string.Empty;
        RedoDescription = _undoRedoManager.GetRedoDescription() ?? string.Empty;
    }

    private void Undo()
    {
        if (!_undoRedoManager.CanUndo) return;

        _logger.LogDebug("Executing Undo: {Description}", _undoRedoManager.GetUndoDescription());
        _undoRedoManager.Undo();

        // Notify the active ViewModel to refresh its view
        FormFillingViewModel?.RefreshFromModel();
    }

    private void Redo()
    {
        if (!_undoRedoManager.CanRedo) return;

        _logger.LogDebug("Executing Redo: {Description}", _undoRedoManager.GetRedoDescription());
        _undoRedoManager.Redo();

        // Notify the active ViewModel to refresh its view
        FormFillingViewModel?.RefreshFromModel();
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public FormFillingViewModel? FormFillingViewModel
    {
        get => _formFillingViewModel;
        private set
        {
            // Dispose the old ViewModel to prevent memory leaks
            _formFillingViewModel?.Dispose();
            SetProperty(ref _formFillingViewModel, value);
        }
    }

    public TemplateEditorViewModel? TemplateEditorViewModel
    {
        get => _templateEditorViewModel;
        private set => SetProperty(ref _templateEditorViewModel, value);
    }

    public IInputCommand OpenCommand { get; }
    public IInputCommand OpenTemplateForEditingCommand { get; }
    public IInputCommand NewTemplateCommand { get; }
    public IInputCommand SaveCommand { get; }
    public IInputCommand SaveAsCommand { get; }
    public IInputCommand CloseCommand { get; }
    public IInputCommand SwitchToTemplateEditingCommand { get; }
    public IInputCommand SwitchToFormFillingCommand { get; }
    public IInputCommand UndoCommand { get; }
    public IInputCommand RedoCommand { get; }
    public IInputCommand SetLightThemeCommand { get; }
    public IInputCommand SetDarkThemeCommand { get; }
    public IInputCommand SetSystemThemeCommand { get; }
    public IInputCommand SetCustomThemeCommand { get; }
    public IInputCommand OpenCertificateManagementCommand { get; }
    public IInputCommand OpenS3BrowserCommand { get; }
    public IInputCommand SubmitToS3Command { get; }
    public IInputCommand BrowseTemplateGalleryCommand { get; }
    public IInputCommand PublishTemplateCommand { get; }
    public IInputCommand ConfigureS3Command { get; }
    public IInputCommand CheckForUpdatesCommand { get; }

    private async Task OpenFileAsync(bool forFilling)
    {
        _logger.LogInformation("OpenFile command invoked (forFilling: {ForFilling})", forFilling);

        try
        {
            _logger.LogDebug("Calling FileService.OpenFileAsync()...");
            var document = await _fileService.OpenFileAsync();

            if (document != null)
            {
                _logger.LogInformation("Document loaded successfully");
                _logger.LogDebug("  Document Type: {Type}", document.DocumentType);
                _logger.LogDebug("  Title: {Title}", document.Metadata.Title);
                _logger.LogDebug("  Sections: {Count}", document.Sections.Count);

                // Verify signatures if present
                if (_signatureService.IsSigned(document))
                {
                    _logger.LogInformation("Document has signatures - verifying...");
                    var verificationResults = _signatureService.VerifyAllSignatures(document);

                    foreach (var (signature, result) in verificationResults)
                    {
                        _logger.LogInformation("Signature from {Signer}: {Result}",
                            signature.SignerName,
                            result.Summary);
                    }
                }

                // Add to recent files
                if (_fileService.CurrentFilePath != null)
                {
                    _settingsService.AddRecentFile(_fileService.CurrentFilePath);
                }

                // Handle template opening workflow
                if (document.DocumentType == DocumentType.Template && forFilling)
                {
                    _logger.LogInformation("Template opened for filling - converting to FilledForm");

                    // Convert template to filled form
                    document.DocumentType = DocumentType.FilledForm;
                    document.Metadata.TemplateId = document.Metadata.TemplateId ?? Guid.NewGuid().ToString();
                    // Keep existing TemplateVersion (already set in template)
                    document.Metadata.FilledDate = DateTime.UtcNow;
                    document.Metadata.FilledBy = Environment.UserName;

                    _isEditingTemplate = false;

                    // Clear the current file path so Save As is required
                    _logger.LogDebug("Clearing file path to force Save As on first save");
                    _fileService.ClearCurrentFilePath();

                    _currentDocument = document;
                    TemplateEditorViewModel = null; // Clear template editor
                    FormFillingViewModel = new FormFillingViewModel(document);

                    UpdateTitle();
                    _logger.LogInformation("Template converted to FilledForm - user must Save As");
                }
                else if (document.DocumentType == DocumentType.Template && !forFilling)
                {
                    _logger.LogInformation("Template opened for editing");
                    _isEditingTemplate = true;
                    _currentDocument = document;
                    FormFillingViewModel = null; // Clear form filling view
                    TemplateEditorViewModel = new TemplateEditorViewModel(document);
                    UpdateTitle();
                }
                else
                {
                    // FilledForm - just open normally
                    _logger.LogInformation("FilledForm opened for editing");
                    _isEditingTemplate = false;
                    _currentDocument = document;
                    TemplateEditorViewModel = null; // Clear template editor
                    FormFillingViewModel = new FormFillingViewModel(document);
                    UpdateTitle();
                }

                _logger.LogInformation("Document opened and view updated");
            }
            else
            {
                _logger.LogInformation("File dialog cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file");
            await _dialogService.ShowErrorAsync(
                "File Open Error",
                $"Unable to open the file. Please check that the file exists and is a valid APR document.\n\nDetails: {ex.Message}");
        }
    }

    private void CreateNewTemplate()
    {
        _logger.LogInformation("CreateNewTemplate command invoked");

        try
        {
            // Create a new template document
            var newTemplate = new AprDocument
            {
                DocumentType = DocumentType.Template,
                Metadata = new Metadata
                {
                    Title = "New Template",
                    Description = "Enter template description",
                    TemplateVersion = "1.0.0",
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                },
                Sections = new List<Section>
                {
                    new Section
                    {
                        Id = "section_1",
                        Title = "Section 1",
                        Description = null,
                        Prompts = new List<Prompt>
                        {
                            new Prompt
                            {
                                Id = "section_1_prompt_1",
                                Label = "Example Prompt",
                                Hints = new PromptHints
                                {
                                    Placeholder = "Enter response here",
                                    HelpText = "This is a sample prompt. Edit or delete as needed."
                                }
                            }
                        },
                        Sections = new List<Section>()
                    }
                }
            };

            _isEditingTemplate = true;
            _currentDocument = newTemplate;
            _fileService.ClearCurrentFilePath(); // Force Save As for new templates

            FormFillingViewModel = null;
            TemplateEditorViewModel = new TemplateEditorViewModel(newTemplate);

            Title = "PromptResponse - New Template*";
            _logger.LogInformation("New template created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new template");
            _ = _dialogService.ShowErrorAsync(
                "Template Creation Error",
                $"Unable to create a new template.\n\nDetails: {ex.Message}");
        }
    }

    private void SwitchToTemplateEditing()
    {
        _logger.LogInformation("SwitchToTemplateEditing command invoked");

        if (_currentDocument == null)
        {
            _logger.LogWarning("No document loaded, switch cancelled");
            return;
        }

        if (_isEditingTemplate)
        {
            _logger.LogWarning("Already in template editing mode");
            return;
        }

        try
        {
            _logger.LogInformation("Switching from form filling to template editing mode");

            // Update document from form filling view before switching
            FormFillingViewModel?.UpdateDocument();

            // Convert to template
            _currentDocument.DocumentType = DocumentType.Template;

            // Clear filled form metadata
            _currentDocument.Metadata.FilledDate = null;
            _currentDocument.Metadata.FilledBy = null;

            // Set editing mode
            _isEditingTemplate = true;

            // Clear the file path to force Save As
            _fileService.ClearCurrentFilePath();

            // Switch ViewModels
            FormFillingViewModel = null;
            TemplateEditorViewModel = new TemplateEditorViewModel(_currentDocument);

            UpdateTitle();
            _logger.LogInformation("Switched to template editing mode successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to template editing mode");
            _ = _dialogService.ShowErrorAsync(
                "Mode Switch Error",
                $"Unable to switch to template editing mode.\n\nDetails: {ex.Message}");
        }
    }

    private void SwitchToFormFilling()
    {
        _logger.LogInformation("SwitchToFormFilling command invoked");

        if (_currentDocument == null)
        {
            _logger.LogWarning("No document loaded, switch cancelled");
            return;
        }

        if (!_isEditingTemplate)
        {
            _logger.LogWarning("Already in form filling mode");
            return;
        }

        try
        {
            _logger.LogInformation("Switching from template editing to form filling mode");

            // Update document from template editor view before switching
            TemplateEditorViewModel?.UpdateDocument();

            // Convert to filled form
            _currentDocument.DocumentType = DocumentType.FilledForm;

            // Set filled form metadata
            _currentDocument.Metadata.TemplateId = _currentDocument.Metadata.TemplateId ?? Guid.NewGuid().ToString();
            _currentDocument.Metadata.FilledDate = DateTime.UtcNow;
            _currentDocument.Metadata.FilledBy = Environment.UserName;

            // Set editing mode
            _isEditingTemplate = false;

            // Clear the file path to force Save As
            _fileService.ClearCurrentFilePath();

            // Switch ViewModels
            TemplateEditorViewModel = null;
            FormFillingViewModel = new FormFillingViewModel(_currentDocument);

            UpdateTitle();
            _logger.LogInformation("Switched to form filling mode successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to form filling mode");
            _ = _dialogService.ShowErrorAsync(
                "Mode Switch Error",
                $"Unable to switch to form filling mode.\n\nDetails: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a file on application startup (from command line).
    /// </summary>
    public async Task OpenFileOnStartup(string filePath, bool editMode)
    {
        _logger.LogInformation("OpenFileOnStartup called: {File} (Edit mode: {EditMode})",
            filePath, editMode);

        try
        {
            // Validate file exists
            if (!File.Exists(filePath))
            {
                _logger.LogError("Startup file not found: {File}", filePath);
                await _dialogService.ShowErrorAsync(
                    "File Not Found",
                    $"The specified file could not be found:\n{filePath}");
                return;
            }

            // Load the document
            _logger.LogDebug("Loading document from: {File}", filePath);
            await using var stream = File.OpenRead(filePath);
            var serializer = App.ServiceProvider?.GetService(typeof(IAprSerializer)) as IAprSerializer;
            if (serializer == null)
            {
                _logger.LogError("Failed to get IAprSerializer from service provider");
                return;
            }

            var document = await serializer.DeserializeAsync(stream);
            if (document == null)
            {
                _logger.LogError("Failed to deserialize document");
                await _dialogService.ShowErrorAsync(
                    "Document Load Error",
                    $"Failed to load the document. The file may be corrupted or not a valid APR file:\n{filePath}");
                return;
            }

            // Override DocumentType based on file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".aprt")
            {
                document.DocumentType = DocumentType.Template;
            }
            else if (extension == ".aprf")
            {
                document.DocumentType = DocumentType.FilledForm;
            }

            _logger.LogInformation("Document loaded successfully");
            _logger.LogDebug("  Document Type: {Type}", document.DocumentType);
            _logger.LogDebug("  Title: {Title}", document.Metadata.Title);

            // Set the file path
            _fileService.SetCurrentFilePath(filePath);

            // Add to recent files
            _settingsService.AddRecentFile(filePath);

            // Open in appropriate mode
            if (editMode || document.DocumentType == DocumentType.Template)
            {
                // Edit mode - open template for editing
                _logger.LogInformation("Opening in edit mode");
                _isEditingTemplate = true;
                _currentDocument = document;
                FormFillingViewModel = null;
                TemplateEditorViewModel = new TemplateEditorViewModel(document);
            }
            else
            {
                // Fill mode - open for filling out
                _logger.LogInformation("Opening in fill mode");

                if (document.DocumentType == DocumentType.Template)
                {
                    // Convert template to filled form
                    document.DocumentType = DocumentType.FilledForm;
                    document.Metadata.FilledDate = DateTime.UtcNow;
                    document.Metadata.FilledBy = Environment.UserName;
                    _fileService.ClearCurrentFilePath(); // Force Save As
                }

                _isEditingTemplate = false;
                _currentDocument = document;
                TemplateEditorViewModel = null;
                FormFillingViewModel = new FormFillingViewModel(document);
            }

            UpdateTitle();
            _logger.LogInformation("File opened successfully on startup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening startup file");
            await _dialogService.ShowErrorAsync(
                "Startup File Error",
                $"Unable to open the specified file at startup.\n\nDetails: {ex.Message}");
        }
    }

    private async Task SaveFileAsync()
    {
        _logger.LogInformation("SaveFile command invoked");

        if (_currentDocument == null)
        {
            _logger.LogWarning("No document loaded, save cancelled");
            return;
        }

        try
        {
            // Update document from ViewModel first
            _logger.LogDebug("Updating document from ViewModel...");
            FormFillingViewModel?.UpdateDocument();
            TemplateEditorViewModel?.UpdateDocument();

            // If this was a template that we started filling out, force Save As
            if (_currentDocument.DocumentType == DocumentType.FilledForm && !_isEditingTemplate && _fileService.CurrentFilePath == null)
            {
                _logger.LogInformation("FilledForm without existing path - redirecting to Save As");
                await SaveFileAsAsync();
                return;
            }

            if (_fileService.CurrentFilePath != null)
            {
                _logger.LogInformation("Saving to existing file: {Path}", _fileService.CurrentFilePath);
                _logger.LogDebug("Document Type: {Type}", _currentDocument.DocumentType);

                _logger.LogDebug("Calling FileService.SaveFileAsync()...");
                await _fileService.SaveFileAsync(_currentDocument, _fileService.CurrentFilePath);

                // Add to recent files
                if (_fileService.CurrentFilePath != null)
                {
                    _settingsService.AddRecentFile(_fileService.CurrentFilePath);
                }

                _logger.LogInformation("File saved successfully");
                FormFillingViewModel?.MarkAsSaved();
                TemplateEditorViewModel?.MarkAsSaved();
            }
            else
            {
                _logger.LogDebug("No current file path, redirecting to SaveAs");
                await SaveFileAsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file");
            await _dialogService.ShowErrorAsync(
                "File Save Error",
                $"Unable to save the file. Please check that you have write permissions and sufficient disk space.\n\nDetails: {ex.Message}");
        }
    }

    private async Task SaveFileAsAsync()
    {
        _logger.LogInformation("SaveFileAs command invoked");

        if (_currentDocument == null)
        {
            _logger.LogWarning("No document loaded, save cancelled");
            return;
        }

        try
        {
            _logger.LogDebug("Updating document from ViewModel...");
            // Update document from ViewModel
            FormFillingViewModel?.UpdateDocument();
            TemplateEditorViewModel?.UpdateDocument();

            _logger.LogDebug("Calling FileService.SaveFileAsAsync()...");
            await _fileService.SaveFileAsAsync(_currentDocument);

            // Add to recent files
            if (_fileService.CurrentFilePath != null)
            {
                _settingsService.AddRecentFile(_fileService.CurrentFilePath);
            }

            _logger.LogInformation("File saved successfully");
            FormFillingViewModel?.MarkAsSaved();
            TemplateEditorViewModel?.MarkAsSaved();
            UpdateTitle();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file");
            await _dialogService.ShowErrorAsync(
                "File Save Error",
                $"Unable to save the file. Please check that you have write permissions and sufficient disk space.\n\nDetails: {ex.Message}");
        }
    }

    private void CloseDocument()
    {
        _logger.LogInformation("CloseDocument command invoked");

        try
        {
            // Clear the current document and ViewModels
            _currentDocument = null;
            FormFillingViewModel = null;
            TemplateEditorViewModel = null;
            _fileService.ClearCurrentFilePath();

            // Reset title
            Title = "PromptResponse";

            _logger.LogInformation("Document closed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing document");
        }
    }

    private void UpdateTitle()
    {
        var fileName = _fileService.CurrentFilePath != null
            ? Path.GetFileName(_fileService.CurrentFilePath)
            : "Untitled";
        var docTitle = _currentDocument?.Metadata.Title;
        var newTitle = $"{docTitle} - {fileName} - PromptResponse";

        _logger.LogDebug("Updating window title to: {Title}", newTitle);
        Title = newTitle;
    }

    private void SetTheme(ThemeVariant themeVariant)
    {
        var themeName = themeVariant == ThemeVariant.Light ? "Light"
            : themeVariant == ThemeVariant.Dark ? "Dark"
            : "System";

        _logger.LogInformation("Changing theme to: {Theme}", themeName);

        try
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = themeVariant;

                // Save theme preference
                _settingsService.Settings.Theme = themeName;
                _logger.LogInformation("Theme changed successfully to: {Theme}", themeName);
            }
            else
            {
                _logger.LogWarning("Application.Current is null, cannot change theme");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing theme to: {Theme}", themeName);
        }
    }

    private void SetCustomTheme()
    {
        _logger.LogInformation("Changing theme to: Custom (Vivid)");

        try
        {
            if (Application.Current != null)
            {
                // Set to light mode as base
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;

                // Override with custom vivid colors
                var resources = Application.Current.Resources;

                // Warm copper accent colors
                resources["SystemAccentColor"] = Color.Parse("#D97706");
                resources["SystemAccentColorLight1"] = Color.Parse("#F59E0B");
                resources["SystemAccentColorLight2"] = Color.Parse("#FBBF24");
                resources["SystemAccentColorLight3"] = Color.Parse("#FCD34D");
                resources["SystemAccentColorDark1"] = Color.Parse("#B45309");
                resources["SystemAccentColorDark2"] = Color.Parse("#92400E");
                resources["SystemAccentColorDark3"] = Color.Parse("#78350F");

                // Deep teal/cyan primary
                resources["SystemControlHighlightAccentBrush"] = new SolidColorBrush(Color.Parse("#0891B2"));
                resources["SystemControlForegroundAccentBrush"] = new SolidColorBrush(Color.Parse("#0E7490"));

                // Warm neutral backgrounds that complement the main background
                resources["SystemAltHighColor"] = Color.Parse("#D6D3CE");
                resources["SystemChromeMediumColor"] = Color.Parse("#C8C4BE");
                resources["SystemChromeLowColor"] = Color.Parse("#BAB6B0");
                resources["SystemControlBackgroundAltHighBrush"] = new SolidColorBrush(Color.Parse("#D6D3CE"));
                resources["SystemControlBackgroundAltMediumBrush"] = new SolidColorBrush(Color.Parse("#C8C4BE"));

                // Main window background - darker warm gray with purple tint
                resources["SystemRegionBrush"] = new SolidColorBrush(Color.Parse("#C8C5C0"));
                resources["SystemChromeMediumLowColor"] = Color.Parse("#C8C5C0");
                resources["SystemAltMediumColor"] = Color.Parse("#C8C5C0");

                // Window background specifically
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if (desktop.MainWindow != null)
                    {
                        desktop.MainWindow.Background = new SolidColorBrush(Color.Parse("#C8C5C0"));
                    }
                }

                // Rich text colors
                resources["SystemBaseHighColor"] = Color.Parse("#1F2937");
                resources["SystemBaseMediumHighColor"] = Color.Parse("#374151");
                resources["SystemBaseMediumColor"] = Color.Parse("#6B7280");

                // Subtle warm borders
                resources["SystemControlForegroundBaseMediumBrush"] = new SolidColorBrush(Color.Parse("#A8A29E"));
                resources["SystemControlForegroundBaseMediumLowBrush"] = new SolidColorBrush(Color.Parse("#D4D1CC"));

                // Button colors - Teal with warm highlights
                resources["ButtonBackground"] = new SolidColorBrush(Color.Parse("#0891B2"));
                resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.Parse("#06B6D4"));
                resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.Parse("#0E7490"));
                resources["ButtonForeground"] = new SolidColorBrush(Colors.White);

                // TextBox with warm accent - slightly darker background for contrast
                resources["TextControlBackground"] = new SolidColorBrush(Color.Parse("#F5F3EF"));
                resources["TextControlForeground"] = new SolidColorBrush(Color.Parse("#1F2937"));
                resources["TextControlBorderBrush"] = new SolidColorBrush(Color.Parse("#A8A29E"));
                resources["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Color.Parse("#0891B2"));
                resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Color.Parse("#0E7490"));

                // Save theme preference
                _settingsService.Settings.Theme = "Custom";
                _logger.LogInformation("Custom theme applied successfully");
            }
            else
            {
                _logger.LogWarning("Application.Current is null, cannot change theme");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing to custom theme");
        }
    }

    /// <summary>
    /// Applies the theme from saved settings on startup.
    /// </summary>
    public void ApplyThemeFromSettings()
    {
        var theme = _settingsService.Settings.Theme;
        _logger.LogInformation("Applying saved theme: {Theme}", theme);

        switch (theme)
        {
            case "Light":
                SetTheme(ThemeVariant.Light);
                break;
            case "Dark":
                SetTheme(ThemeVariant.Dark);
                break;
            case "Custom":
                SetCustomTheme();
                break;
            case "System":
            default:
                SetTheme(ThemeVariant.Default);
                break;
        }
    }

    private void OpenCertificateManagement()
    {
        _logger.LogInformation("Opening Certificate Management window");

        try
        {
            var onePasswordService = App.ServiceProvider?.GetService(typeof(IOnePasswordService)) as IOnePasswordService
                ?? throw new InvalidOperationException("OnePasswordService not available");

            var logger = App.ServiceProvider?.GetService(typeof(ILogger<CertificateManagementViewModel>)) as ILogger<CertificateManagementViewModel>
                ?? throw new InvalidOperationException("Logger not available");

            var viewModel = new CertificateManagementViewModel(
                _certificateGenerator,
                _certificateStore,
                onePasswordService,
                logger);

            var window = new CertificateManagementWindow(viewModel);

            // Get the main window as the owner
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is not null)
            {
                window.ShowDialog(desktop.MainWindow);
            }
            else
            {
                window.Show();
            }

            _logger.LogInformation("Certificate Management window opened");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening Certificate Management window");
            _ = _dialogService.ShowErrorAsync(
                "Certificate Management Error",
                $"Unable to open the Certificate Management window.\n\nDetails: {ex.Message}");
        }
    }

    private void OpenS3Browser()
    {
        _logger.LogInformation("Opening S3 Browser window");

        try
        {
            var logger = App.ServiceProvider?.GetService(typeof(ILogger<S3BrowserViewModel>)) as ILogger<S3BrowserViewModel>
                ?? throw new InvalidOperationException("Logger not available");

            // Create a callback to load downloaded documents
            Action<AprDocument> documentLoadedCallback = (document) =>
            {
                _logger.LogInformation("Document loaded from S3: {Title}", document.Metadata.Title);

                // Open the document in form filling mode
                _currentDocument = document;
                _isEditingTemplate = false;
                _fileService.ClearCurrentFilePath(); // Force Save As since it came from S3

                TemplateEditorViewModel = null;
                FormFillingViewModel = new FormFillingViewModel(document);

                UpdateTitle();
            };

            var viewModel = new S3BrowserViewModel(
                _s3BrowserService,
                logger,
                documentLoadedCallback);

            var window = new S3BrowserWindow(viewModel);

            // Get the main window as the owner
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is not null)
            {
                window.ShowDialog(desktop.MainWindow);
            }
            else
            {
                window.Show();
            }

            _logger.LogInformation("S3 Browser window opened");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening S3 Browser window");
            _ = _dialogService.ShowErrorAsync(
                "S3 Browser Error",
                $"Unable to open the S3 Browser window.\n\nDetails: {ex.Message}");
        }
    }

    private bool CanSubmitToS3()
    {
        return _currentDocument != null && !_isEditingTemplate;
    }

    private async Task SubmitToS3Async()
    {
        if (_currentDocument == null) return;

        _logger.LogInformation("Submitting form to S3");

        try
        {
            // Check if submission is configured
            if (!_s3SubmissionService.CanSubmit(_currentDocument))
            {
                await _dialogService.ShowErrorAsync(
                    "S3 Submission Error",
                    "This form is not configured for S3 submission. Please configure S3 settings in the template.");
                return;
            }

            // Check expiration status
            var expirationStatus = _s3SubmissionService.GetExpirationStatus(_currentDocument);
            if (expirationStatus.IsExpired)
            {
                await _dialogService.ShowErrorAsync(
                    "Submission Expired",
                    "The submission policy for this form has expired.\n\nPlease contact the form provider for an updated template.");
                return;
            }

            // Build confirmation message
            var formTitle = _currentDocument.Metadata.Title;
            var confirmMessage = $"You are about to submit the form:\n\n\"{formTitle}\"\n\n";

            if (expirationStatus.TimeRemaining.HasValue)
            {
                var daysRemaining = expirationStatus.TimeRemaining.Value.TotalDays;
                if (daysRemaining < 7)
                {
                    confirmMessage += $"Note: This submission window expires in {(int)daysRemaining} days.\n\n";
                }
            }

            confirmMessage += "This action will upload your form data to the configured S3 storage. Continue?";

            // Show confirmation dialog
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Confirm Submission",
                confirmMessage);

            if (!confirmed)
            {
                _logger.LogInformation("S3 submission cancelled by user");
                return;
            }

            // Perform the submission
            _logger.LogInformation("User confirmed S3 submission, uploading...");

            var s3Key = await _s3SubmissionService.SubmitFormAsync(_currentDocument);

            // Show success with details
            var successMessage = $"Your form has been submitted successfully!\n\n" +
                                 $"Form: {formTitle}\n" +
                                 $"Storage Key: {s3Key}\n" +
                                 $"Submitted: {DateTime.Now:g}";

            await _dialogService.ShowInfoAsync(
                "Submission Complete",
                successMessage);

            _logger.LogInformation("Form submitted successfully to S3: {Key}", s3Key);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error submitting form to S3");
            await _dialogService.ShowErrorAsync(
                "Network Error",
                $"Could not connect to the storage server.\n\nPlease check your internet connection and try again.\n\nDetails: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting form to S3");
            await _dialogService.ShowErrorAsync(
                "Submission Failed",
                $"An error occurred while submitting the form.\n\nDetails: {ex.Message}");
        }
    }

    private void BrowseTemplateGallery()
    {
        _logger.LogInformation("Opening Template Gallery");

        try
        {
            // Template gallery functionality would go here
            _ = _dialogService.ShowInfoAsync(
                "Template Gallery",
                "Template Gallery functionality coming soon.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening Template Gallery");
            _ = _dialogService.ShowErrorAsync(
                "Template Gallery Error",
                $"Unable to open the Template Gallery.\n\nDetails: {ex.Message}");
        }
    }

    private bool CanPublishTemplate()
    {
        return _currentDocument != null && _isEditingTemplate;
    }

    private async Task PublishTemplateAsync()
    {
        if (_currentDocument == null) return;

        _logger.LogInformation("Publishing template");

        try
        {
            // Validate template for publishing
            var (isValid, errorMessage) = _templatePublishingService.ValidateForPublishing(_currentDocument);
            if (!isValid)
            {
                await _dialogService.ShowErrorAsync(
                    "Publishing Validation Failed",
                    errorMessage ?? "Template cannot be published.");
                return;
            }

            // Open S3 configuration dialog for publishing
            OpenS3ConfigurationDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing template");
            await _dialogService.ShowErrorAsync(
                "Publishing Error",
                $"Failed to publish template: {ex.Message}");
        }
    }

    private bool CanConfigureS3()
    {
        return _currentDocument != null && _isEditingTemplate;
    }

    private void OpenS3ConfigurationDialog()
    {
        if (_currentDocument == null) return;

        _logger.LogInformation("Opening S3 Configuration dialog");

        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;

            if (mainWindow == null)
            {
                _logger.LogWarning("Could not get main window for S3 Configuration dialog");
                return;
            }

            var viewModel = new S3ConfigurationViewModel(
                _s3PolicyGenerator,
                _currentDocument,
                (applied) =>
                {
                    if (applied && _templateEditorViewModel != null)
                    {
                        _templateEditorViewModel.MarkAsChanged();
                        _logger.LogInformation("S3 configuration applied to template");
                    }
                });

            var window = new S3ConfigurationWindow(viewModel)
            {
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
            };

            window.ShowDialog(mainWindow);

            _logger.LogInformation("S3 Configuration dialog opened");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening S3 Configuration dialog");
            _ = _dialogService.ShowErrorAsync(
                "S3 Configuration Error",
                $"Unable to open the S3 Configuration dialog.\n\nDetails: {ex.Message}");
        }
    }

    private bool CanCheckForUpdates()
    {
        // Can check for updates when filling a form that has a template source URL
        return _currentDocument != null &&
               !_isEditingTemplate &&
               !string.IsNullOrWhiteSpace(_currentDocument.Metadata.TemplateSourceUrl);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_currentDocument == null) return;

        _logger.LogInformation("Checking for template updates");

        try
        {
            // Check for updates
            var result = await _templateUpdateService.CheckForUpdateAsync(_currentDocument);

            if (!result.Success)
            {
                await _dialogService.ShowErrorAsync(
                    "Update Check Failed",
                    result.ErrorMessage ?? "Failed to check for updates.");
                return;
            }

            if (!result.UpdateAvailable)
            {
                await _dialogService.ShowInfoAsync(
                    "No Updates Available",
                    $"You have the latest version of this template.\n\nCurrent version: {result.CurrentVersion}");
                return;
            }

            // Update is available - ask user if they want to apply it
            var confirmMessage = $"A new version of this template is available.\n\n" +
                                 $"Current version: {result.CurrentVersion}\n" +
                                 $"New version: {result.NewVersion}\n\n" +
                                 "Your existing responses will be preserved where possible.\n\n" +
                                 "Do you want to update to the new version?";

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Template Update Available",
                confirmMessage);

            if (!confirmed || result.NewTemplate == null)
            {
                _logger.LogInformation("User declined template update");
                return;
            }

            // Apply the update
            _logger.LogInformation("Applying template update from {Old} to {New}",
                result.CurrentVersion, result.NewVersion);

            var migrationResult = _templateUpdateService.ApplyUpdate(_currentDocument, result.NewTemplate);

            // Update the current document
            _currentDocument = migrationResult.MigratedDocument;

            // Refresh the view
            FormFillingViewModel = new FormFillingViewModel(_currentDocument);

            // Show summary
            var summaryMessage = $"Template updated successfully!\n\n" +
                                 $"{migrationResult.Summary}\n\n";

            if (migrationResult.OrphanedPrompts.Count > 0)
            {
                summaryMessage += "Note: Some of your responses were for fields that no longer exist. " +
                                  "These have been preserved in the form description.";
            }

            if (migrationResult.NewPrompts.Count > 0)
            {
                summaryMessage += $"\n\n{migrationResult.NewPrompts.Count} new field(s) have been added. " +
                                  "Please review and complete them.";
            }

            await _dialogService.ShowInfoAsync("Update Complete", summaryMessage);

            // Mark as needing save
            _fileService.ClearCurrentFilePath();
            UpdateTitle();

            _logger.LogInformation("Template update applied: {Summary}", migrationResult.Summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for template updates");
            await _dialogService.ShowErrorAsync(
                "Update Error",
                $"Failed to check for updates.\n\nDetails: {ex.Message}");
        }
    }
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
public class RelayCommand : IInputCommand
{
    private readonly Func<Task>? _execute;
    private readonly Func<object?, Task>? _executeWithParameter;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));
        _execute = () => { execute(); return Task.CompletedTask; };
        _canExecute = canExecute;
    }

    public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));
        _executeWithParameter = (param) => { execute(param); return Task.CompletedTask; };
        _canExecute = canExecute;
    }

    public RelayCommand(Func<object?, Task> execute, Func<bool>? canExecute = null)
    {
        _executeWithParameter = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_executeWithParameter != null)
        {
            await _executeWithParameter(parameter);
        }
        else if (_execute != null)
        {
            await _execute();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
