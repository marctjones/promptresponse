using AwesomeAssertions;
using PromptResponse.Desktop.ViewModels.Prompts.Presentation;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Prompts;

public class RawEditorPresentationTests
{
    [Theory]
    [InlineData(false, true, true, false, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(false, false, false, true, false)]
    [InlineData(true, false, false, true, false)]
    public void WidgetAndRawEditorStates_PreserveTheUniversalTextEntryContract(
        bool rawEditing, bool available, bool widget, bool rawEditor, bool toggle)
    {
        RawEditorPresentation.ShowHintedWidget(rawEditing, available).Should().Be(widget);
        RawEditorPresentation.ShowRawEditor(rawEditing, available).Should().Be(rawEditor);
        RawEditorPresentation.ShowRawToggle(available).Should().Be(toggle);
    }

    [Fact]
    public void TogglePresentation_UsesAccessibleActionTextAndProfileScaledGeometry()
    {
        RawEditorPresentation.ToggleName(false, "Date of birth").Should().Be("Type any text for Date of birth");
        RawEditorPresentation.ToggleName(true, "Date of birth").Should().Be("Use the suggested input for Date of birth");
        RawEditorPresentation.ToggleGlyph(false).Should().Be("\u270E\uFE0E");
        RawEditorPresentation.ToggleGlyph(true).Should().Be("\u2611\uFE0E");

        var glyphSize = RawEditorPresentation.ToggleGlyphSize(1.5);
        glyphSize.Should().Be(30.0);
        RawEditorPresentation.ToggleButtonSize(glyphSize).Should().Be(51.0);
        RawEditorPresentation.ToggleButtonSize(10.0).Should().Be(36.0);
    }
}
