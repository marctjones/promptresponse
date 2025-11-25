using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PromptResponse.Core.Models;
using PromptResponse.Core.Services;
using PromptResponse.Core.Services.Certificates;
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
    private readonly Mock<ICertificateGenerator> _mockCertificateGenerator;
    private readonly Mock<ICertificateStore> _mockCertificateStore;
    private readonly Mock<ISignatureService> _mockSignatureService;
    private readonly Mock<IS3BrowserService> _mockS3BrowserService;
    private readonly Mock<IS3SubmissionService> _mockS3SubmissionService;
    private readonly Mock<ITemplateGalleryService> _mockTemplateGalleryService;
    private readonly Mock<ITemplatePublishingService> _mockTemplatePublishingService;
    private readonly Mock<ILogger<MainWindowViewModel>> _mockLogger;
    private readonly AppSettings _appSettings;

    public MainWindowViewModelTests()
    {
        _mockFileService = new Mock<IFileService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockCertificateGenerator = new Mock<ICertificateGenerator>();
        _mockCertificateStore = new Mock<ICertificateStore>();
        _mockSignatureService = new Mock<ISignatureService>();
        _mockS3BrowserService = new Mock<IS3BrowserService>();
        _mockS3SubmissionService = new Mock<IS3SubmissionService>();
        _mockTemplateGalleryService = new Mock<ITemplateGalleryService>();
        _mockTemplatePublishingService = new Mock<ITemplatePublishingService>();
        _mockLogger = new Mock<ILogger<MainWindowViewModel>>();

        // Setup default app settings
        _appSettings = new AppSettings { Theme = "System" };
        _mockSettingsService.Setup(s => s.Settings).Returns(_appSettings);
    }

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            _mockFileService.Object,
            _mockSettingsService.Object,
            _mockDialogService.Object,
            _mockCertificateGenerator.Object,
            _mockCertificateStore.Object,
            _mockSignatureService.Object,
            _mockS3BrowserService.Object,
            _mockS3SubmissionService.Object,
            _mockTemplateGalleryService.Object,
            _mockTemplatePublishingService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCommands()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.OpenCommand.Should().NotBeNull("OpenCommand should be initialized");
        viewModel.OpenTemplateForEditingCommand.Should().NotBeNull("OpenTemplateForEditingCommand should be initialized");
        viewModel.NewTemplateCommand.Should().NotBeNull("NewTemplateCommand should be initialized");
        viewModel.SaveCommand.Should().NotBeNull("SaveCommand should be initialized");
        viewModel.SaveAsCommand.Should().NotBeNull("SaveAsCommand should be initialized");
        viewModel.CloseCommand.Should().NotBeNull("CloseCommand should be initialized");
        viewModel.SwitchToTemplateEditingCommand.Should().NotBeNull("SwitchToTemplateEditingCommand should be initialized");
        viewModel.SwitchToFormFillingCommand.Should().NotBeNull("SwitchToFormFillingCommand should be initialized");
        viewModel.SetLightThemeCommand.Should().NotBeNull("SetLightThemeCommand should be initialized");
        viewModel.SetDarkThemeCommand.Should().NotBeNull("SetDarkThemeCommand should be initialized");
        viewModel.SetSystemThemeCommand.Should().NotBeNull("SetSystemThemeCommand should be initialized");
        viewModel.SetCustomThemeCommand.Should().NotBeNull("SetCustomThemeCommand should be initialized");
        viewModel.OpenCertificateManagementCommand.Should().NotBeNull("OpenCertificateManagementCommand should be initialized");
        viewModel.OpenS3BrowserCommand.Should().NotBeNull("OpenS3BrowserCommand should be initialized");
    }

    [Fact]
    public void Constructor_ShouldSetDefaultTitle()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Title.Should().Be("PromptResponse", "default title should be 'PromptResponse'");
    }

    [Fact]
    public void Constructor_ShouldHaveNoDocumentLoaded()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.FormFillingViewModel.Should().BeNull("no document should be loaded initially");
        viewModel.TemplateEditorViewModel.Should().BeNull("no template should be loaded initially");
    }

    [Fact]
    public void CreateNewTemplate_ShouldCreateBlankTemplate()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.NewTemplateCommand.Execute(null);

        // Assert
        viewModel.TemplateEditorViewModel.Should().NotBeNull("TemplateEditorViewModel should be set after creating new template");
        viewModel.FormFillingViewModel.Should().BeNull("FormFillingViewModel should be null when editing template");
        viewModel.Title.Should().Contain("New Template", "title should indicate new template");

        _mockFileService.Verify(f => f.ClearCurrentFilePath(), Times.Once, "should clear file path for new template");
    }

    [Fact]
    public async Task OpenTemplate_ForFilling_ShouldConvertToFilledForm()
    {
        // Arrange
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");
        _mockSignatureService.Setup(s => s.IsSigned(It.IsAny<AprDocument>())).Returns(false);

        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenCommand.Execute(null);
        // Wait for async operation to complete
        await Task.Delay(100);

        // Assert
        viewModel.FormFillingViewModel.Should().NotBeNull("FormFillingViewModel should be set when opening template for filling");
        viewModel.TemplateEditorViewModel.Should().BeNull("TemplateEditorViewModel should be null when filling form");

        // Verify document was converted
        template.DocumentType.Should().Be(DocumentType.FilledForm, "template should be converted to FilledForm");
        template.Metadata.FilledBy.Should().NotBeNullOrEmpty("FilledBy should be set");
        template.Metadata.FilledDate.Should().NotBeNull("FilledDate should be set");

        // Verify file path was cleared (force Save As)
        _mockFileService.Verify(f => f.ClearCurrentFilePath(), Times.Once, "should clear file path to force Save As");
    }

    [Fact]
    public async Task OpenTemplate_ForEditing_ShouldLoadInTemplateEditor()
    {
        // Arrange
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");
        _mockSignatureService.Setup(s => s.IsSigned(It.IsAny<AprDocument>())).Returns(false);

        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenTemplateForEditingCommand.Execute(null);
        // Wait for async operation to complete
        await Task.Delay(100);

        // Assert
        viewModel.TemplateEditorViewModel.Should().NotBeNull("TemplateEditorViewModel should be set when opening template for editing");
        viewModel.FormFillingViewModel.Should().BeNull("FormFillingViewModel should be null when editing template");

        // Verify document remains as template
        template.DocumentType.Should().Be(DocumentType.Template, "template should remain as Template when editing");
    }

    [Fact]
    public async Task OpenFilledForm_ShouldLoadInFormFilling()
    {
        // Arrange
        var filledForm = CreateTestFilledForm();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(filledForm);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/form.aprf");
        _mockSignatureService.Setup(s => s.IsSigned(It.IsAny<AprDocument>())).Returns(false);

        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenCommand.Execute(null);
        // Wait for async operation to complete
        await Task.Delay(100);

        // Assert
        viewModel.FormFillingViewModel.Should().NotBeNull("FormFillingViewModel should be set for filled form");
        viewModel.TemplateEditorViewModel.Should().BeNull("TemplateEditorViewModel should be null for filled form");
    }

    [Fact]
    public async Task OpenFile_WhenCancelled_ShouldNotChangeState()
    {
        // Arrange
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync((AprDocument?)null);

        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenCommand.Execute(null);
        // Wait for async operation to complete
        await Task.Delay(100);

        // Assert
        viewModel.FormFillingViewModel.Should().BeNull("FormFillingViewModel should remain null when cancelled");
        viewModel.TemplateEditorViewModel.Should().BeNull("TemplateEditorViewModel should remain null when cancelled");
        viewModel.Title.Should().Be("PromptResponse", "title should not change when cancelled");
    }

    [Fact]
    public async Task SaveFile_WithCurrentPath_ShouldSaveToPath()
    {
        // Arrange
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");
        _mockSignatureService.Setup(s => s.IsSigned(It.IsAny<AprDocument>())).Returns(false);

        var viewModel = CreateViewModel();

        // Open a template for editing first
        viewModel.OpenTemplateForEditingCommand.Execute(null);
        await Task.Delay(100);

        // Act
        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockFileService.Verify(f => f.SaveFileAsync(It.IsAny<AprDocument>(), "/test/template.aprt"), Times.Once);
    }

    [Fact]
    public async Task SaveFile_WithoutCurrentPath_ShouldCallSaveAs()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Create new template (no path set)
        viewModel.NewTemplateCommand.Execute(null);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns((string?)null);

        // Act
        viewModel.SaveCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockFileService.Verify(f => f.SaveFileAsAsync(It.IsAny<AprDocument>()), Times.Once);
    }

    [Fact]
    public void CloseDocument_ShouldClearDocument()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.NewTemplateCommand.Execute(null);

        // Verify document is loaded
        viewModel.TemplateEditorViewModel.Should().NotBeNull();

        // Act
        viewModel.CloseCommand.Execute(null);

        // Assert
        viewModel.FormFillingViewModel.Should().BeNull("FormFillingViewModel should be cleared");
        viewModel.TemplateEditorViewModel.Should().BeNull("TemplateEditorViewModel should be cleared");
        viewModel.Title.Should().Be("PromptResponse", "title should reset to default");

        _mockFileService.Verify(f => f.ClearCurrentFilePath(), Times.AtLeast(2), "should clear file path when closing");
    }

    [Fact]
    public async Task OpenFile_WithSignedDocument_ShouldVerifySignatures()
    {
        // Arrange
        var signedDoc = CreateTestFilledForm();
        signedDoc.Metadata.FormSignatures = new List<DigitalSignature>
        {
            new DigitalSignature
            {
                SignerName = "Test Signer",
                SignedAt = DateTime.UtcNow,
                SignatureData = "test-signature"
            }
        };

        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(signedDoc);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/signed.aprf");
        _mockSignatureService.Setup(s => s.IsSigned(It.IsAny<AprDocument>())).Returns(true);
        _mockSignatureService.Setup(s => s.VerifyAllSignatures(It.IsAny<AprDocument>()))
            .Returns(new Dictionary<DigitalSignature, SignatureVerificationResult>
            {
                { signedDoc.Metadata.FormSignatures[0], new SignatureVerificationResult { IsValid = true } }
            });

        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockSignatureService.Verify(s => s.IsSigned(It.IsAny<AprDocument>()), Times.Once);
        _mockSignatureService.Verify(s => s.VerifyAllSignatures(It.IsAny<AprDocument>()), Times.Once);
    }

    [Fact]
    public async Task OpenFile_ShouldAddToRecentFiles()
    {
        // Arrange
        var template = CreateTestTemplate();
        _mockFileService.Setup(f => f.OpenFileAsync()).ReturnsAsync(template);
        _mockFileService.Setup(f => f.CurrentFilePath).Returns("/test/template.aprt");
        _mockSignatureService.Setup(s => s.IsSigned(It.IsAny<AprDocument>())).Returns(false);

        var viewModel = CreateViewModel();

        // Act
        viewModel.OpenTemplateForEditingCommand.Execute(null);
        await Task.Delay(100);

        // Assert
        _mockSettingsService.Verify(s => s.AddRecentFile("/test/template.aprt"), Times.Once);
    }

    [Fact]
    public void SaveCommand_CanExecute_ShouldBeFalseWithNoDocument()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.SaveCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("SaveCommand should not be executable without a document");
    }

    [Fact]
    public void SaveAsCommand_CanExecute_ShouldBeFalseWithNoDocument()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.SaveAsCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("SaveAsCommand should not be executable without a document");
    }

    [Fact]
    public void CloseCommand_CanExecute_ShouldBeFalseWithNoDocument()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.CloseCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("CloseCommand should not be executable without a document");
    }

    [Fact]
    public void NewTemplateCommand_ShouldAlwaysBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.NewTemplateCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue("NewTemplateCommand should always be executable");
    }

    [Fact]
    public void OpenCommand_ShouldAlwaysBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.OpenCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue("OpenCommand should always be executable");
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
                        new Prompt
                        {
                            Id = "prompt_1",
                            Label = "Test Prompt"
                        }
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
                        new Prompt
                        {
                            Id = "prompt_1",
                            Label = "Test Prompt",
                            Response = "Test Response"
                        }
                    },
                    Sections = new List<Section>()
                }
            }
        };
    }
}
