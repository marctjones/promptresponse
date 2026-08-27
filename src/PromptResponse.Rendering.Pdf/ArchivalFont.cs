using System.Reflection;
using Excise.Core.Graphics;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Loads the embedded DejaVu Sans font used for PDF/A archival output. PDF/A
/// forbids the non-embedded base-14 fonts, so archival PDFs embed this Unicode
/// font (which also renders arbitrary Unicode without the base-14 mojibake).
/// </summary>
internal static class ArchivalFont
{
    /// <summary>Creates a pdfe font from the embedded DejaVu Sans TTF.</summary>
    public static PdfFont Load(double size = 11)
    {
        var assembly = typeof(ArchivalFont).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("DejaVuSans.ttf", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded archival font (DejaVuSans.ttf) was not found.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        return PdfFont.FromTrueType(stream, size);
    }
}
