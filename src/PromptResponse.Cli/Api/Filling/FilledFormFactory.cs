using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Api.Filling;

/// <summary>
/// Clones templates and records the metadata required for a filled form.
/// </summary>
internal sealed class FilledFormFactory(IAprSerializer serializer)
{
    public AprDocument Create(AprDocument template, string? filledBy)
    {
        var cloned = serializer.Deserialize(serializer.Serialize(template));
        var now = DateTime.UtcNow;

        cloned.DocumentType = DocumentType.FilledForm;
        // `filledBy` and `filledDate` were retired in beta.6 as workflow state: an
        // unsigned claim about who completed a form and when is not evidence of either,
        // and the format declines to carry a claim it cannot support. A workflow that
        // needs to record receipt writes an ordinary form naming this one under
        // `metadata.regarding` (specification 5.2.2).
        cloned.Metadata.Modified = now;

        return cloned;
    }
}
