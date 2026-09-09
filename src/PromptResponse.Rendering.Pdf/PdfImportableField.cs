using Excise.Core.Document;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Which AcroForm fields represent a question somebody answers.
/// </summary>
/// <remarks>
/// Not every widget in a form's field tree is a field in the sense APR cares
/// about. Two kinds carry no answer and must be skipped by <em>every</em>
/// consumer — the importer that produces prompts, and the
/// <see cref="PdfWidgetManifest"/> oracle that checks the importer did not drop
/// anything. Keeping the rule in one place is what stops those two disagreeing:
/// if the oracle expected a field the importer is right to skip, it would report
/// a correct import as incomplete.
/// </remarks>
public static class PdfImportableField
{
    /// <summary>Whether this field is a question a person answers.</summary>
    /// <remarks>
    /// Excluded:
    /// <list type="bullet">
    /// <item><description>
    /// <b>Signature fields</b> — a signature is applied, not typed, and APR
    /// handles signing separately from responses.
    /// </description></item>
    /// <item><description>
    /// <b>Push buttons</b> — a push button is a JavaScript action ("Print Form",
    /// "Clear Fields"), not a field. It has no value, and nothing on the printed
    /// form asks it. Importing one produces an unanswerable yes/no prompt, which
    /// is what CT-W4 did before this existed. Checkboxes and radio buttons are
    /// also <c>Button</c> fields and are <em>not</em> excluded — they do carry an
    /// answer, which is why the test is <see cref="PdfField.IsPushButton"/>
    /// rather than the field type.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static bool CarriesAnAnswer(PdfField field) =>
        field.FieldType != PdfFieldType.Signature && !field.IsPushButton;
}
