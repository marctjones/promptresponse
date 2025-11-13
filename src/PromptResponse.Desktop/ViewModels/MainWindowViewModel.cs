using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;
using System.Windows.Input;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IFileService _fileService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private AprDocument? _currentDocument;
    private FormFillingViewModel? _formFillingViewModel;
    private TemplateEditorViewModel? _templateEditorViewModel;
    private string _title = "PromptResponse";
    private bool _isEditingTemplate = false; // Track if we're editing a template vs filling a form

    public MainWindowViewModel(IFileService fileService, ILogger<MainWindowViewModel> logger)
    {
        _fileService = fileService;
        _logger = logger;

        _logger.LogInformation("MainWindowViewModel constructor called");
        _logger.LogDebug("  FileService type: {Type}", fileService.GetType().Name);

        // Commands
        _logger.LogDebug("Setting up commands...");
        OpenCommand = new RelayCommand(async () => await OpenFileAsync(forFilling: true));
        OpenTemplateForEditingCommand = new RelayCommand(async () => await OpenFileAsync(forFilling: false));
        NewTemplateCommand = new RelayCommand(CreateNewTemplate);
        SaveCommand = new RelayCommand(async () => await SaveFileAsync(), () => _currentDocument != null);
        SaveAsCommand = new RelayCommand(async () => await SaveFileAsAsync(), () => _currentDocument != null);
        CloseCommand = new RelayCommand(CloseDocument, () => _currentDocument != null);

        // Theme commands
        SetLightThemeCommand = new RelayCommand(() => SetTheme(ThemeVariant.Light));
        SetDarkThemeCommand = new RelayCommand(() => SetTheme(ThemeVariant.Dark));
        SetSystemThemeCommand = new RelayCommand(() => SetTheme(ThemeVariant.Default));
        SetCustomThemeCommand = new RelayCommand(SetCustomTheme);

        _logger.LogInformation("MainWindowViewModel initialized successfully");
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public FormFillingViewModel? FormFillingViewModel
    {
        get => _formFillingViewModel;
        private set => SetProperty(ref _formFillingViewModel, value);
    }

    public TemplateEditorViewModel? TemplateEditorViewModel
    {
        get => _templateEditorViewModel;
        private set => SetProperty(ref _templateEditorViewModel, value);
    }

    public ICommand OpenCommand { get; }
    public ICommand OpenTemplateForEditingCommand { get; }
    public ICommand NewTemplateCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SetLightThemeCommand { get; }
    public ICommand SetDarkThemeCommand { get; }
    public ICommand SetSystemThemeCommand { get; }
    public ICommand SetCustomThemeCommand { get; }

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
            // TODO: Show error dialog
            Console.Error.WriteLine($"Error opening file: {ex.Message}");
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
                        Subsections = new List<Subsection>()
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
            Console.Error.WriteLine($"Error creating new template: {ex.Message}");
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
                Console.Error.WriteLine($"Error: File not found: {filePath}");
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
                Console.Error.WriteLine($"Error: Failed to load document from {filePath}");
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
            Console.Error.WriteLine($"Error opening file: {ex.Message}");
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
            // TODO: Show error dialog
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
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

            _logger.LogInformation("File saved successfully");
            FormFillingViewModel?.MarkAsSaved();
            TemplateEditorViewModel?.MarkAsSaved();
            UpdateTitle();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file");
            // TODO: Show error dialog
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
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
            : "System Default";

        _logger.LogInformation("Changing theme to: {Theme}", themeName);

        try
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = themeVariant;
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
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
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

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
