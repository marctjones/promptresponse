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
/// <para>
/// <b>Interactive behaviour is out of scope, deliberately.</b> A PDF form can
/// carry JavaScript (field formatting, validation, calculation) and submit
/// actions that post to a URL. This converter ignores all of it: no script is
/// read, translated, or carried into the output, and a submit target is dropped
/// with the button that held it rather than becoming a
/// <c>submissionUrls</c> entry.
/// </para>
/// <para>
/// That is a scope decision for the first version, not a limitation being
/// worked around — but it sits comfortably with the format, since APR
/// "contains no scripts, macros, formulas with host access, or external
/// references", a property the specification says MUST NOT be weakened
/// [APR-SEC-009]. A converted form is therefore a set of questions, never a
/// program. What is lost is real and worth knowing: a form whose arithmetic
/// was done by script converts to one where the person does the arithmetic.
/// </para>
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
    /// <b>Push buttons</b> — a push button is an action ("Print Form", "Clear
    /// Fields", "Submit"), not a field. It has no value, and nothing on the
    /// printed form asks it. Importing one produces an unanswerable yes/no
    /// prompt, which is what CT-W4 did before this existed. This covers submit
    /// buttons too, which is intended per the scope note above. Checkboxes and
    /// radio buttons are also <c>Button</c> fields and are <em>not</em>
    /// excluded — they do carry an answer, which is why the test is
    /// <see cref="PdfField.IsPushButton"/> rather than the field type.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static bool CarriesAnAnswer(PdfField field) =>
        field.FieldType != PdfFieldType.Signature && !field.IsPushButton;
}
