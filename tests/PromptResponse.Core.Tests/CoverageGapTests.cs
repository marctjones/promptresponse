using AwesomeAssertions;
using PromptResponse.Core.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests;

/// <summary>
/// Targeted tests filling coverage gaps for Core paths that aren't naturally exercised
/// by the larger feature-oriented test suites: stack-trim path on UndoRedoManager,
/// AddRange on Errors/Warnings, ToString on a clean result, the few data-type hint
/// branches that are uncommon (time / datetime / boolean / multiline / InferDataType
/// fallback), and the AprJsonSerializer paths for "literal null content" and the
/// generic catch-all wrapping when the JsonSerializer surfaces non-JsonException errors.
/// </summary>
public class CoverageGapTests
{
    // ---- UndoRedoManager: stack-trim path triggered when _maxUndoLevels is exceeded ----

    [Fact]
    public void UndoRedoManager_ExceedingMaxUndoLevels_TrimsOldestCommands()
    {
        var manager = new UndoRedoManager(maxUndoLevels: 3);
        var doc = new AprDocument { Metadata = new Metadata { Title = "T" } };

        // Push 5 commands; only the most recent 3 should remain undoable.
        for (int i = 0; i < 5; i++)
        {
            manager.ExecuteCommand(new AddSectionCommand(doc, new Section { Id = $"s{i}", Title = $"S{i}" }));
        }

        manager.CanUndo.Should().BeTrue();

        // Undo three times; the fourth should not be possible.
        manager.Undo();
        manager.Undo();
        manager.Undo();
        manager.CanUndo.Should().BeFalse("oldest two commands were trimmed when max-levels was exceeded");
    }

    // ---- ValidationResult: AddErrors range, AddWarnings range, ToString edge cases ----

    [Fact]
    public void ValidationResult_AddErrorsRange_AppendsAll()
    {
        var result = new ValidationResult();
        var errors = new[]
        {
            new ValidationError("E1", "p1"),
            new ValidationError("E2", "p2"),
            new ValidationError("E3", "p3"),
        };

        result.AddErrors(errors);

        result.Errors.Should().HaveCount(3);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_AddWarningsRange_AppendsAll()
    {
        var result = new ValidationResult();
        var warnings = new[]
        {
            new ValidationWarning("W1", "p1"),
            new ValidationWarning("W2", "p2"),
        };

        result.AddWarnings(warnings);

        result.Warnings.Should().HaveCount(2);
        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void ValidationResult_ToString_CleanResult_SaysSucceeded()
    {
        var result = ValidationResult.Valid();

        result.ToString().Should().Be("Validation succeeded");
    }

    [Fact]
    public void ValidationResult_ToString_ErrorsAndWarnings_FormatsBothSections()
    {
        var result = new ValidationResult();
        result.AddError(new ValidationError("Missing title", "metadata.title"));
        result.AddWarning(new ValidationWarning("Hint mismatch", "prompts[0]"));

        var text = result.ToString();

        text.Should().Contain("error");
        text.Should().Contain("warning");
        text.Should().Contain("Missing title");
        text.Should().Contain("Hint mismatch");
    }

    [Fact]
    public void ValidationResult_ToString_OnlyWarnings_HasWarningSection()
    {
        var result = new ValidationResult();
        result.AddWarning(new ValidationWarning("Soft hint", "x"));

        var text = result.ToString();

        text.Should().Contain("warning");
        text.Should().Contain("Soft hint");
    }

    // ---- DataTypeValidator: hint branches not otherwise exercised ----

    [Theory]
    [InlineData("time", "10:30")]
    [InlineData("time", "23:59:59")]
    [InlineData("datetime", "2025-04-29T14:00:00Z")]
    [InlineData("boolean", "true")]
    [InlineData("boolean", "yes")]
    [InlineData("boolean", "0")]
    [InlineData("multiline", "line one\nline two")]
    public void DataTypeValidator_AcceptsValidValuesAcrossLessCommonHints(string type, string value)
    {
        var validator = new DataTypeValidator();
        var prompt = new Prompt { Id = "p1", Label = "x", Response = value, Hints = new PromptHints { ExpectedDataType = type } };

        var result = validator.ValidateResponse(prompt);

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeFalse($"'{value}' matches '{type}' so no advisory should fire");
    }

    [Fact]
    public void DataTypeValidator_InferDataType_FallsBackToText_OnUnclassifiable()
    {
        var validator = new DataTypeValidator();

        var inferred = validator.InferDataType("just some unstructured prose");

        inferred.Should().Be("text");
    }

    [Fact]
    public void DataTypeValidator_InferDataType_OnEmpty_ReturnsText()
    {
        var validator = new DataTypeValidator();

        validator.InferDataType("").Should().Be("text");
        validator.InferDataType("   ").Should().Be("text");
    }

    [Theory]
    [InlineData("user@example.com", "email")]
    [InlineData("https://example.com", "url")]
    [InlineData("2025-04-29", "date")]
    [InlineData("42.5", "number")]
    public void DataTypeValidator_InferDataType_RecognisesObviousFormats(string value, string expected)
    {
        var validator = new DataTypeValidator();

        validator.InferDataType(value).Should().Be(expected);
    }

    // ---- AprJsonSerializer: deserialize a literal JSON null ----

    [Fact]
    public void AprJsonSerializer_Deserialize_LiteralJsonNull_ThrowsSerializationException()
    {
        var serializer = new AprJsonSerializer();

        var act = () => serializer.Deserialize("null");

        act.Should().Throw<SerializationException>().WithMessage("*returned null*");
    }
}
