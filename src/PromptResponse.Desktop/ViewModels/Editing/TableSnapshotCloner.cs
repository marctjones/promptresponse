using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Creates independent table snapshot models for undo/redo. Kept separate from
/// the view-model so clone policy is explicit and reusable by table editing.
/// </summary>
/// <remarks>
/// The policy itself lives in <see cref="ModelCopier"/>, in Core, with the models it
/// copies. It used to live here, built field by field, and a field-by-field copy is
/// a list of the members somebody remembered: extension members, the prompt's role,
/// and the bounds hints were all absent, so undoing a table edit deleted them from
/// the document. Preserving an unrecognised member is not a nicety the editor may
/// skip - specification 5.8 requires a member present on read to be present,
/// unchanged, on write (APR-MODEL-021).
/// </remarks>
internal static class TableSnapshotCloner
{
    internal static Section CloneSection(Section section) => ModelCopier.Copy(section);

    internal static Prompt ClonePrompt(Prompt prompt) => ModelCopier.Copy(prompt);
}
