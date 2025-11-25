using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Evaluation context for APR documents.
/// </summary>
public class DocumentEvaluationContext : IEvaluationContext
{
    private readonly Dictionary<string, Prompt> _promptsById;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentEvaluationContext"/> class.
    /// </summary>
    /// <param name="document">The APR document to provide context for.</param>
    public DocumentEvaluationContext(AprDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        _promptsById = new Dictionary<string, Prompt>();

        // Index all prompts by ID (recursive)
        foreach (var section in document.Sections)
        {
            IndexPromptsInSection(section);
        }
    }

    private void IndexPromptsInSection(Section section)
    {
        // Index prompts at this level
        foreach (var prompt in section.Prompts)
        {
            _promptsById[prompt.Id] = prompt;
        }

        // Recursively index prompts in child sections
        foreach (var childSection in section.Sections)
        {
            IndexPromptsInSection(childSection);
        }
    }

    /// <inheritdoc/>
    public string GetFieldValue(string fieldId)
    {
        if (_promptsById.TryGetValue(fieldId, out var prompt))
        {
            return prompt.Response ?? "";
        }
        return "";
    }

    /// <inheritdoc/>
    public bool HasField(string fieldId)
    {
        return _promptsById.ContainsKey(fieldId);
    }
}
