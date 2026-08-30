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
        cloned.Metadata.FilledBy = filledBy ?? Environment.UserName;
        cloned.Metadata.FilledDate = now;
        cloned.Metadata.Modified = now;

        return cloned;
    }
}
