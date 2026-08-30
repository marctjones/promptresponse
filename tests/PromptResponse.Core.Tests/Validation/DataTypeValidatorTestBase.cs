using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;

namespace PromptResponse.Core.Tests.Validation;

public abstract class DataTypeValidatorTestBase
{
    protected DataTypeValidator Validator { get; } = new();

    protected static Prompt CreatePrompt(
        string response,
        string? expectedDataType = null,
        string id = "prompt_001",
        string label = "Test prompt",
        string? validationPattern = null) =>
        new()
        {
            Id = id,
            Label = label,
            Response = response,
            Hints = new PromptHints
            {
                ExpectedDataType = expectedDataType,
                ValidationPattern = validationPattern
            }
        };
}
