using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Evaluation context for APR documents.
/// </summary>
public class DocumentEvaluationContext : IEvaluationContext
{
    private readonly Dictionary<string, Prompt> _promptsById;

    public DocumentEvaluationContext(AprDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        _promptsById = new Dictionary<string, Prompt>();

        // Index all prompts by ID
        foreach (var section in document.Sections)
        {
            foreach (var prompt in section.Prompts)
            {
                _promptsById[prompt.Id] = prompt;
            }

            foreach (var subsection in section.Subsections)
            {
                foreach (var prompt in subsection.Prompts)
                {
                    _promptsById[prompt.Id] = prompt;
                }
            }
        }
    }

    public string GetFieldValue(string fieldId)
    {
        if (_promptsById.TryGetValue(fieldId, out var prompt))
        {
            return prompt.Response ?? "";
        }
        return "";
    }

    public bool HasField(string fieldId)
    {
        return _promptsById.ContainsKey(fieldId);
    }
}
