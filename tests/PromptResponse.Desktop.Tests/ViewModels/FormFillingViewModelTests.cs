using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Unit tests for FormFillingViewModel.
/// </summary>
public class FormFillingViewModelTests
{
    #region Construction Tests

    [Fact]
    public void Constructor_ShouldInitializeSections()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Sections.Should().HaveCount(2);
        viewModel.Sections[0].Title.Should().Be("Personal Information");
        viewModel.Sections[1].Title.Should().Be("Contact Details");
    }

    [Fact]
    public void Constructor_ShouldSetTitleFromMetadata()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Metadata.Title = "Test Form Title";

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Title.Should().Be("Test Form Title");
    }

    [Fact]
    public void Constructor_ShouldSetDescriptionFromMetadata()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Metadata.Description = "Test Description";

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Description.Should().Be("Test Description");
    }

    [Fact]
    public void Constructor_ShouldNotHaveUnsavedChangesInitially()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    #endregion

    #region Read-Only / Signed Form Tests

    [Fact]
    public void Constructor_WithSignedForm_ShouldBeReadOnly()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Metadata.FormSignatures = new List<DigitalSignature>
        {
            new DigitalSignature
            {
                SignerName = "Test Signer",
                SignedAt = DateTime.UtcNow,
                SignatureData = "test"
            }
        };

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.IsReadOnly.Should().BeTrue();
        viewModel.IsEditable.Should().BeFalse();
        viewModel.HasFormSignatures.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithUnsignedForm_ShouldBeEditable()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.IsReadOnly.Should().BeFalse();
        viewModel.IsEditable.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithTemplateSignatures_ShouldShowTemplateSignatureInfo()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Metadata.TemplateSignatures = new List<DigitalSignature>
        {
            new DigitalSignature
            {
                SignerName = "Template Publisher",
                SignedAt = DateTime.UtcNow,
                SignatureData = "test"
            }
        };

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.HasTemplateSignatures.Should().BeTrue();
        viewModel.IsReadOnly.Should().BeFalse("template signatures don't make form read-only");
    }

    #endregion

    #region Unsaved Changes Tracking Tests

    [Fact]
    public void PromptResponseChange_ShouldSetHasUnsavedChanges()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Act
        viewModel.Sections[0].Prompts[0].Response = "New Value";

        // Assert
        viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void MarkAsSaved_ShouldClearUnsavedChanges()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);
        viewModel.Sections[0].Prompts[0].Response = "New Value";
        viewModel.HasUnsavedChanges.Should().BeTrue();

        // Act
        viewModel.MarkAsSaved();

        // Assert
        viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void SaveStateText_ShouldReflectUnsavedChanges()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert - initially saved
        viewModel.SaveStateText.Should().Be("Saved");

        // Act - make a change
        viewModel.Sections[0].Prompts[0].Response = "New Value";

        // Assert - modified
        viewModel.SaveStateText.Should().Be("● Modified");
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public void ProgressPercentage_WithNoAnswers_ShouldBeZero()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.AnsweredPrompts.Should().Be(0);
        viewModel.ProgressPercentage.Should().Be(0);
    }

    [Fact]
    public void ProgressPercentage_WithAllAnswers_ShouldBeHundred()
    {
        // Arrange
        var document = CreateTestDocument();
        foreach (var section in document.Sections)
        {
            foreach (var prompt in section.Prompts)
            {
                prompt.Response = "Answered";
            }
        }

        // Act
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public void ProgressPercentage_ShouldUpdateWhenResponseChanges()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);
        var initialProgress = viewModel.ProgressPercentage;

        // Act
        viewModel.Sections[0].Prompts[0].Response = "New Answer";

        // Assert
        viewModel.ProgressPercentage.Should().BeGreaterThan(initialProgress);
    }

    [Fact]
    public void ProgressText_ShouldShowAnsweredCount()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.ProgressText.Should().Contain("of");
        viewModel.ProgressText.Should().Contain("prompts answered");
    }

    #endregion

    #region Search Tests

    [Fact]
    public void IsSearchVisible_DefaultsToFalse()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.IsSearchVisible.Should().BeFalse();
    }

    [Fact]
    public void ToggleSearchCommand_ShouldToggleVisibility()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Act
        viewModel.ToggleSearchCommand.Execute(null);

        // Assert
        viewModel.IsSearchVisible.Should().BeTrue();

        // Act again
        viewModel.ToggleSearchCommand.Execute(null);

        // Assert
        viewModel.IsSearchVisible.Should().BeFalse();
    }

    [Fact]
    public void SearchText_WhenSet_ShouldPerformSearch()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);
        viewModel.IsSearchVisible = true;

        // Act
        viewModel.SearchText = "Name";

        // Assert
        viewModel.SearchResults.Should().NotBeEmpty();
    }

    [Fact]
    public void SearchText_WhenNoMatches_ShouldShowNoMatches()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);
        viewModel.IsSearchVisible = true;

        // Act
        viewModel.SearchText = "XYZNONEXISTENT";

        // Assert
        viewModel.SearchResults.Should().BeEmpty();
        viewModel.MatchStatusText.Should().Be("No matches");
    }

    [Fact]
    public void ClearSearchCommand_ShouldClearSearchText()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);
        viewModel.IsSearchVisible = true;
        viewModel.SearchText = "Test";

        // Act
        viewModel.ClearSearchCommand.Execute(null);

        // Assert
        viewModel.SearchText.Should().BeEmpty();
        viewModel.SearchResults.Should().BeEmpty();
    }

    #endregion

    #region Section ViewModel Tests

    [Fact]
    public void SectionViewModel_ShouldExposeTitle()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Sections[0].Title.Should().Be("Personal Information");
    }

    [Fact]
    public void SectionViewModel_ShouldExposeDescription()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Sections[0].Description.Should().Be("Enter your personal details");
    }

    [Fact]
    public void SectionViewModel_ShouldDefaultToExpanded()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Sections[0].IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void SectionViewModel_ShouldIncludePrompts()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Sections[0].Prompts.Should().HaveCount(2);
        viewModel.Sections[0].Prompts[0].Label.Should().Be("Full Name");
    }

    [Fact]
    public void SectionViewModel_ShouldIncludeChildSections()
    {
        // Arrange
        var document = CreateDocumentWithNestedSections();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.Sections[0].Sections.Should().HaveCount(1);
        viewModel.Sections[0].Sections[0].Title.Should().Be("Child Section");
    }

    #endregion

    #region StatusMessage Tests

    [Fact]
    public void StatusMessage_Initially_ShouldBeReady()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Assert
        viewModel.StatusMessage.Should().Be("Ready");
    }

    [Fact]
    public void StatusMessage_WithUnsavedChanges_ShouldShowModified()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Act
        viewModel.Sections[0].Prompts[0].Response = "Changed";

        // Assert
        viewModel.StatusMessage.Should().Contain("unsaved");
    }

    [Fact]
    public void SetStatusMessage_ShouldUpdateStatusMessage()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Act
        viewModel.SetStatusMessage("Custom message");

        // Assert
        viewModel.StatusMessage.Should().Be("Custom message");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldUnsubscribeFromEvents()
    {
        // Arrange
        var document = CreateTestDocument();
        var viewModel = new FormFillingViewModel(document);

        // Act
        viewModel.Dispose();

        // Assert - should not throw when modifying prompts after dispose
        var prompt = viewModel.Sections[0].Prompts[0];
        prompt.Response = "After dispose";
        // If events weren't unsubscribed, this could cause issues
    }

    #endregion

    #region Helper Methods

    private static AprDocument CreateTestDocument()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Form",
                Description = "Test Description",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Personal Information",
                    Description = "Enter your personal details",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "prompt_name",
                            Label = "Full Name",
                            Hints = new PromptHints { Placeholder = "Enter your name" }
                        },
                        new Prompt
                        {
                            Id = "prompt_dob",
                            Label = "Date of Birth",
                            Hints = new PromptHints { ExpectedDataType = "date" }
                        }
                    },
                    Sections = new List<Section>()
                },
                new Section
                {
                    Id = "section_2",
                    Title = "Contact Details",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "prompt_email",
                            Label = "Email",
                            Hints = new PromptHints { ExpectedDataType = "email" }
                        }
                    },
                    Sections = new List<Section>()
                }
            }
        };
    }

    private static AprDocument CreateDocumentWithNestedSections()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Nested Test Form",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "parent_section",
                    Title = "Parent Section",
                    Prompts = new List<Prompt>(),
                    Sections = new List<Section>
                    {
                        new Section
                        {
                            Id = "child_section",
                            Title = "Child Section",
                            Prompts = new List<Prompt>
                            {
                                new Prompt
                                {
                                    Id = "child_prompt",
                                    Label = "Child Prompt"
                                }
                            },
                            Sections = new List<Section>()
                        }
                    }
                }
            }
        };
    }

    #endregion
}
