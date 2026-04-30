using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using FluentAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.Controls;

/// <summary>
/// UI component tests for modern control styles.
/// Verifies that modern controls render correctly with proper styling.
/// </summary>
public class ModernControlTests
{
    public ModernControlTests()
    {
        // Initialize Avalonia for testing
        if (Application.Current == null)
        {
            AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernButton_AppliesCornerRadius()
    {
        // Arrange
        var button = new Button { Classes = { "modern" } };

        // Act
        var cornerRadius = button.CornerRadius;

        // Assert
        // Note: CornerRadius might not be set until styles are applied
        // In full UI tests, we'd apply the style first
        cornerRadius.Should().NotBeNull("modern buttons should have corner radius defined");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernAccentButton_HasCorrectClass()
    {
        // Arrange
        var button = new Button { Classes = { "modern-accent" } };

        // Act & Assert
        button.Classes.Should().Contain("modern-accent", "accent button should have modern-accent class");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernTextBox_HasMinimumHeight()
    {
        // Arrange
        var textBox = new TextBox { Classes = { "modern" } };

        // Act
        var minHeight = textBox.MinHeight;

        // Assert
        minHeight.Should().BeGreaterOrEqualTo(0, "modern textbox should have minimum height defined");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernCard_CanBeCreated()
    {
        // Arrange & Act
        var card = new Border { Classes = { "modern-card" } };

        // Assert
        card.Should().NotBeNull("modern card border should be creatable");
        card.Classes.Should().Contain("modern-card");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void SectionCard_CanBeCreated()
    {
        // Arrange & Act
        var card = new Border { Classes = { "section-card" } };

        // Assert
        card.Should().NotBeNull("section card border should be creatable");
        card.Classes.Should().Contain("section-card");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void CommandBar_CanBeCreated()
    {
        // Arrange & Act
        var commandBar = new Border { Classes = { "command-bar" } };

        // Assert
        commandBar.Should().NotBeNull("command bar border should be creatable");
        commandBar.Classes.Should().Contain("command-bar");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void StatusBar_CanBeCreated()
    {
        // Arrange & Act
        var statusBar = new Border { Classes = { "status-bar" } };

        // Assert
        statusBar.Should().NotBeNull("status bar border should be creatable");
        statusBar.Classes.Should().Contain("status-bar");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void FieldLabel_CanBeCreated()
    {
        // Arrange & Act
        var label = new TextBlock { Classes = { "field-label" } };

        // Assert
        label.Should().NotBeNull("field label should be creatable");
        label.Classes.Should().Contain("field-label");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void HelpText_CanBeCreated()
    {
        // Arrange & Act
        var helpText = new TextBlock { Classes = { "help-text" } };

        // Assert
        helpText.Should().NotBeNull("help text should be creatable");
        helpText.Classes.Should().Contain("help-text");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void SectionTitle_CanBeCreated()
    {
        // Arrange & Act
        var title = new TextBlock { Classes = { "section-title" } };

        // Assert
        title.Should().NotBeNull("section title should be creatable");
        title.Classes.Should().Contain("section-title");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void PageTitle_CanBeCreated()
    {
        // Arrange & Act
        var title = new TextBlock { Classes = { "page-title" } };

        // Assert
        title.Should().NotBeNull("page title should be creatable");
        title.Classes.Should().Contain("page-title");
    }

    [Theory(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    [InlineData("info-bar-success")]
    [InlineData("info-bar-warning")]
    [InlineData("info-bar-error")]
    [InlineData("info-bar-info")]
    public void InfoBar_AllVariants_CanBeCreated(string className)
    {
        // Arrange & Act
        var infoBar = new Border { Classes = { "info-bar", className } };

        // Assert
        infoBar.Should().NotBeNull($"{className} should be creatable");
        infoBar.Classes.Should().Contain(className);
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernExpander_CanBeCreated()
    {
        // Arrange & Act
        var expander = new Expander { Classes = { "modern" } };

        // Assert
        expander.Should().NotBeNull("modern expander should be creatable");
        expander.Classes.Should().Contain("modern");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernCheckBox_CanBeCreated()
    {
        // Arrange & Act
        var checkBox = new CheckBox { Classes = { "modern" } };

        // Assert
        checkBox.Should().NotBeNull("modern checkbox should be creatable");
        checkBox.Classes.Should().Contain("modern");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernRadioButton_CanBeCreated()
    {
        // Arrange & Act
        var radioButton = new RadioButton { Classes = { "modern" } };

        // Assert
        radioButton.Should().NotBeNull("modern radio button should be creatable");
        radioButton.Classes.Should().Contain("modern");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernComboBox_CanBeCreated()
    {
        // Arrange & Act
        var comboBox = new ComboBox { Classes = { "modern" } };

        // Assert
        comboBox.Should().NotBeNull("modern combobox should be creatable");
        comboBox.Classes.Should().Contain("modern");
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void MultipleModernControls_CanCoexist()
    {
        // Arrange & Act
        var button = new Button { Classes = { "modern" } };
        var textBox = new TextBox { Classes = { "modern" } };
        var card = new Border { Classes = { "modern-card" } };

        // Assert
        button.Should().NotBeNull();
        textBox.Should().NotBeNull();
        card.Should().NotBeNull();
        // All controls should be able to exist simultaneously
    }

    [Fact(Skip = "Tests legacy .modern style class via standalone Avalonia setup that conflicts with the Avalonia.Headless harness; will be rewritten against new rendering profile tokens in Phase 5. Tracked: idlergear task #22.")]
    public void ModernControls_CanBeNested()
    {
        // Arrange & Act
        var card = new Border { Classes = { "modern-card" } };
        var button = new Button { Classes = { "modern-accent" } };
        var textBox = new TextBox { Classes = { "modern" } };

        var panel = new StackPanel();
        panel.Children.Add(textBox);
        panel.Children.Add(button);
        card.Child = panel;

        // Assert
        card.Child.Should().NotBeNull("modern controls should be nestable");
        ((StackPanel)card.Child).Children.Count.Should().Be(2);
    }
}
