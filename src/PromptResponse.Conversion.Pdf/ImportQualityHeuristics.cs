namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Whether a label is a question a person can answer, or a machine name wearing
/// a label's clothes.
/// </summary>
/// <remarks>
/// <para>
/// This duplicates the private <c>IsCrypticLabel</c> in
/// <c>PdfImportQualityAssessor</c>. That is deliberate but temporary: the
/// converter is being built segregated from <c>PromptResponse.Rendering.Pdf</c>
/// so an unfinished pipeline cannot regress <c>apr import</c>, and reaching into
/// that assembly to make a private helper public would be exactly the kind of
/// change that segregation is meant to avoid for now.
/// </para>
/// <para>
/// <b>The duplicate is guarded rather than trusted.</b> A test compares this
/// against the public <c>ImportQuality.CrypticLabelRatio</c> across the whole
/// corpus, so the two cannot drift apart unnoticed. When the converter is folded
/// in, delete this and share the original.
/// </para>
/// </remarks>
public static class ImportQualityHeuristics
{
    /// <summary>Whether a label reads as a raw field name rather than a question.</summary>
    /// <remarks>
    /// Three signals, all drawn from what real government forms actually
    /// produce: a bracketed index (<c>f1_01[0]</c>, the XFA naming every IRS
    /// form uses), a leading <c>#</c>, or a short single token mixing letters
    /// and digits (<c>CheckBox12</c>, <c>FillText1</c>, <c>TextField1</c>).
    /// </remarks>
    public static bool LooksCryptic(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return true;
        }

        var trimmed = label.Trim();

        // No letters at all is not a question. ct-w4's author labelled three
        // fields "1", "2" and "3", which passed as meaningful because the
        // digit-and-letter test below requires a letter to be present. Those
        // then graded label recovery, marking a correct "1. Withholding Code:
        // Enter Withholding Code letter chosen..." as wrong for disagreeing
        // with "1" -- an oracle scoring a good answer against a useless one.
        if (!trimmed.Any(char.IsLetter))
        {
            return true;
        }

        return trimmed.Contains('[')
            || trimmed.StartsWith('#')
            || (!trimmed.Contains(' ')
                && trimmed.Length <= 12
                && trimmed.Any(char.IsDigit)
                && trimmed.Any(char.IsLetter));
    }
}
