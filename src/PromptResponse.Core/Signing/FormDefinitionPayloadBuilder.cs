using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>Builds the ordered canonical payload for an APR form definition.</summary>
internal static class FormDefinitionPayloadBuilder
{
    internal static byte[] Build(AprDocument document)
    {
        var writer = new CanonicalPayloadWriter().Add("scheme", AprCanonicalizer.Scheme + "/formdef").Add("title", document.Metadata.Title).Add("templateId", document.Metadata.TemplateId).Add("templateVersion", document.Metadata.TemplateVersion);
        foreach (var role in document.Roles ?? []) writer.Add("R.id", role.Id).Add("R.name", role.Name).Add("R.desc", role.Description);
        foreach (var section in document.Sections) AppendSection(writer, section);
        return writer.ToBytes();
    }
    private static void AppendSection(CanonicalPayloadWriter writer, Section section)
    {
        writer.Add("S.id", section.Id).Add("S.title", section.Title).Add("S.desc", section.Description).Add("S.kind", section.Kind).Add("S.canAddRows", section.CanAddRows).Add("S.maxRows", section.MaxRows).Add("S.role", section.Role);
        foreach (var prompt in section.Prompts) AppendPrompt(writer, prompt);
        foreach (var child in section.Sections) AppendSection(writer, child);
    }
    private static void AppendPrompt(CanonicalPayloadWriter writer, Prompt prompt)
    {
        var hints = prompt.Hints;
        writer.Add("P.id", prompt.Id).Add("P.label", prompt.Label).Add("P.type", hints.ExpectedDataType).Add("P.placeholder", hints.Placeholder).Add("P.help", hints.HelpText).Add("P.suggested", string.Join("\u001f", hints.SuggestedValues)).Add("P.pattern", hints.ValidationPattern).Add("P.min", hints.Min).Add("P.max", hints.Max).Add("P.step", hints.Step).Add("P.role", prompt.Role).Add("P.exprHidden", hints.ExprHidden).Add("P.exprValue", hints.ExprValue).Add("P.exprExpected", hints.ExprExpected).Add("P.exprValidation", hints.ExprValidation).Add("P.exprReadOnly", hints.ExprReadOnly);
    }
}
