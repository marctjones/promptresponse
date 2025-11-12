using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using System.Windows.Input;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IFileService _fileService;
    private AprDocument? _currentDocument;
    private FormFillingViewModel? _formFillingViewModel;
    private string _title = "PromptResponse";

    public MainWindowViewModel(IFileService fileService)
    {
        _fileService = fileService;

        // Commands
        OpenCommand = new RelayCommand(async () => await OpenFileAsync());
        SaveCommand = new RelayCommand(async () => await SaveFileAsync(), () => _currentDocument != null);
        SaveAsCommand = new RelayCommand(async () => await SaveFileAsAsync(), () => _currentDocument != null);
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

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }

    private async Task OpenFileAsync()
    {
        try
        {
            var document = await _fileService.OpenFileAsync();
            if (document != null)
            {
                _currentDocument = document;
                FormFillingViewModel = new FormFillingViewModel(document);
                UpdateTitle();
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            Console.Error.WriteLine($"Error opening file: {ex.Message}");
        }
    }

    private async Task SaveFileAsync()
    {
        if (_currentDocument == null) return;

        try
        {
            if (_fileService.CurrentFilePath != null)
            {
                // Update document from ViewModel
                FormFillingViewModel?.UpdateDocument();
                await _fileService.SaveFileAsync(_currentDocument, _fileService.CurrentFilePath);
            }
            else
            {
                await SaveFileAsAsync();
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    private async Task SaveFileAsAsync()
    {
        if (_currentDocument == null) return;

        try
        {
            // Update document from ViewModel
            FormFillingViewModel?.UpdateDocument();
            await _fileService.SaveFileAsAsync(_currentDocument);
            UpdateTitle();
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            Console.Error.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    private void UpdateTitle()
    {
        var fileName = _fileService.CurrentFilePath != null
            ? Path.GetFileName(_fileService.CurrentFilePath)
            : "Untitled";
        var docTitle = _currentDocument?.Metadata.Title;
        Title = $"{docTitle} - {fileName} - PromptResponse";
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

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
