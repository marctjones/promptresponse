using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Models;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Unit tests for MainWindowViewModel.
/// </summary>
public class MainWindowViewModelTests
{
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<ILogger<MainWindowViewModel>> _mockLogger;
    private readonly AppSettings _appSettings;

    public MainWindowViewModelTests()
    {
        _mockFileService = new Mock<IFileService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockLogger = new Mock<ILogger<MainWindowViewModel>>();

        _appSettings = new AppSettings { Theme = "System" };
        _mockSettingsService.Setup(s => s.Settings).Returns(_appSettings);
    }

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            _mockFileService.Object,
            _mockSettingsService.Object,
            _mockDialogService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCommands()
    {
        var viewModel = CreateViewModel();

        viewModel.OpenCommand.Should().NotBeNull();
        viewModel.OpenTemplateForEditingCommand.Should().NotBeNull();
        viewModel.NewTemplateCommand.Should().NotBeNull();
        viewModel.SaveCommand.Should().NotBeNull();
        viewModel.SaveAsCommand.Should().NotBeNull();
        viewModel.CloseCommand.Should().NotBeNull();
        viewModel.SwitchToTemplateEditingCommand.Should().NotBeNull();
        viewModel.SwitchToFormFillingCommand.Should().NotBeNull();
        viewModel.SetLightThemeCommand.Should().NotBeNull();
        viewModel.SetDarkThemeCommand.Should().NotBeNull();
        viewModel.SetSystemThemeCommand.Should().NotBeNull();
        viewModel.SetCustomThemeCommand.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldSetDefaultTitle()
    {
        var viewModel = CreateViewModel();
        viewModel.Title.Should().Be("PromptResponse");
    }

    [Fact]
    public void Constructor_ShouldHaveNoDocumentLoaded()
    {
        var viewModel = CreateViewModel();
        viewModel.FormFillingViewModel.Should().BeNull();
        viewModel.TemplateEditorViewModel.Should().BeNull();
    }

    [Fact]
    public void CreateNewTemplate_ShouldCreateBlankTemplate()
    {
        var viewModel = CreateViewModel();

        viewModel.NewTemplateCommand.Execute(null);

        viewModel.TemplateEditorViewModel.Should().NotBeNull();
        viewModel.FormFillingViewModel.Should().BeNull();
        viewModel.Title.Should().Contain("New Template");
        _mockFileService.Verify(f => f.ClearCurrentFilePath(), Times.Once);
    }

    [Fact]
    public async Task OpenTemplate_ForFilling_ShouldConvertToFilledForm()
    {
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");

        var viewModel = CreateViewModel();

        viewModel.OpenCommand.Execute(null);
        await Task.Delay(100);

        viewModel.FormFillingViewModel.Should().NotBeNull();
        viewModel.TemplateEditorViewModel.Should().BeNull();

        template.DocumentType.Should().Be(DocumentType.FilledForm);
        template.Metadata.FilledBy.Should().NotBeNullOrEmpty();
        template.Metadata.FilledDate.Should().NotBeNull();

        _mockFileService.Verify(f => f.ClearCurrentFilePath(), Times.Once);
    }

    [Fact]
    public async Task OpenTemplate_ForEditing_ShouldLoadInTemplateEditor()
    {
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");

        var viewModel = CreateViewModel();

        viewModel.OpenTemplateForEditingCommand.Execute(null);
        await Task.Delay(100);

        viewModel.TemplateEditorViewModel.Should().NotBeNull();
        viewModel.FormFillingViewModel.Should().BeNull();

        template.DocumentType.Should().Be(DocumentType.Template);
    }

    [Fact]
    public async Task OpenFilledForm_ShouldLoadInFormFilling()
    {
        var filledForm = CreateTestFilledForm();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(filledForm);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/form.aprf");

        var viewModel = CreateViewModel();

        viewModel.OpenCommand.Execute(null);
        await Task.Delay(100);

        viewModel.FormFillingViewModel.Should().NotBeNull();
        viewModel.TemplateEditorViewModel.Should().BeNull();
    }

    [Fact]
    public async Task OpenFile_WhenCancelled_ShouldNotChangeState()
    {
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync((AprDocument?)null);

        var viewModel = CreateViewModel();

        viewModel.OpenCommand.Execute(null);
        await Task.Delay(100);

        viewModel.FormFillingViewModel.Should().BeNull();
        viewModel.TemplateEditorViewModel.Should().BeNull();
        viewModel.Title.Should().Be("PromptResponse");
    }

    [Fact]
    public async Task SaveFile_WithCurrentPath_ShouldSaveToPath()
    {
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");

        var viewModel = CreateViewModel();

        viewModel.OpenTemplateForEditingCommand.Execute(null);
        await Task.Delay(100);

        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        _mockFileService.Verify(f => f.SaveFileAsync(It.IsAny<AprDocument>(), "/test/template.aprt"), Times.Once);
    }

    [Fact]
    public async Task SaveFile_WithoutCurrentPath_ShouldCallSaveAs()
    {
        var viewModel = CreateViewModel();

        viewModel.NewTemplateCommand.Execute(null);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns((string?)null);

        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        _mockFileService.Verify(f => f.SaveFileAsAsync(It.IsAny<AprDocument>()), Times.Once);
    }

    [Fact]
    public void CloseDocument_ShouldClearDocument()
    {
        var viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);

        viewModel.TemplateEditorViewModel.Should().NotBeNull();

        viewModel.CloseCommand.Execute(null);

        viewModel.FormFillingViewModel.Should().BeNull();
        viewModel.TemplateEditorViewModel.Should().BeNull();
        viewModel.Title.Should().Be("PromptResponse");

        _mockFileService.Verify(f => f.ClearCurrentFilePath(), Times.AtLeast(2));
    }

    [Fact]
    public async Task OpenFile_ShouldAddToRecentFiles()
    {
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");

        var viewModel = CreateViewModel();

        viewModel.OpenTemplateForEditingCommand.Execute(null);
        await Task.Delay(100);

        _mockSettingsService.Verify(s => s.AddRecentFile("/test/template.aprt"), Times.Once);
    }

    [Fact]
    public void SaveCommand_CanExecute_ShouldBeFalseWithNoDocument()
    {
        var viewModel = CreateViewModel();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveAsCommand_CanExecute_ShouldBeFalseWithNoDocument()
    {
        var viewModel = CreateViewModel();
        viewModel.SaveAsCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CloseCommand_CanExecute_ShouldBeFalseWithNoDocument()
    {
        var viewModel = CreateViewModel();
        viewModel.CloseCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void NewTemplateCommand_ShouldAlwaysBeExecutable()
    {
        var viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void OpenCommand_ShouldAlwaysBeExecutable()
    {
        var viewModel = CreateViewModel();
        viewModel.OpenCommand.CanExecute(null).Should().BeTrue();
    }

    private static AprDocument CreateTestTemplate()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Template",
                Description = "Test template description",
                TemplateVersion = "1.0.0",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "prompt_1", Label = "Test Prompt" }
                    },
                    Sections = new List<Section>()
                }
            }
        };
    }

    private static AprDocument CreateTestFilledForm()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Filled Form",
                Description = "Test form description",
                TemplateVersion = "1.0.0",
                TemplateId = "template-123",
                FilledBy = "TestUser",
                FilledDate = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "prompt_1", Label = "Test Prompt", Response = "Test Response" }
                    },
                    Sections = new List<Section>()
                }
            }
        };
    }
}
