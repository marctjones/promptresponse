using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Core.Tests.Rendering;

internal static class DocumentRenderModelBuilderTestFactory
{
    public static DocumentRenderModelBuilder CreateBuilder() => new();

    public static AprDocument CreateDocument(params Section[] sections) => new()
    {
        Metadata = new Metadata { Title = "My Form", Description = "A test form" },
        Sections = sections.ToList(),
    };
}
