namespace PromptResponse.Rendering.Pdf;

/// <summary>Page size for PDF output.</summary>
public enum PdfPageSize
{
    /// <summary>US Letter (8.5 × 11 in).</summary>
    Letter,

    /// <summary>ISO A4 (210 × 297 mm).</summary>
    A4,

    /// <summary>US Legal (8.5 × 14 in).</summary>
    Legal,
}

/// <summary>
/// Print/layout options for the PDF renderers — concerns that are renderer-
/// specific and deliberately kept out of the format-agnostic
/// <see cref="Core.Rendering.RenderOptions"/> (the <c>.apr</c> format carries no
/// layout). Controls page size and the running footer (page numbers, a footer
/// label, and a generated date) that make the output a presentable, archival
/// artifact.
/// </summary>
public sealed class PdfRenderOptions
{
    /// <summary>The page size. Defaults to US Letter.</summary>
    public PdfPageSize PageSize { get; init; } = PdfPageSize.Letter;

    /// <summary>
    /// Produce a PDF/A-2b archival file: embeds a Unicode font and adds the
    /// required XMP/OutputIntent structures (pdfe <c>PdfA()</c>). Validated as
    /// PDF/A-2b under veraPDF. Applies to flat (non-interactive) output.
    /// </summary>
    public bool Archival { get; init; }

    /// <summary>Whether to draw a running footer at the bottom of every page.</summary>
    public bool ShowFooter { get; init; } = true;

    /// <summary>Whether the footer includes a "Page X of Y" indicator.</summary>
    public bool ShowPageNumbers { get; init; } = true;

    /// <summary>Whether the footer includes a generated/printed date.</summary>
    public bool ShowGeneratedDate { get; init; } = true;

    /// <summary>
    /// The left-hand footer label. When null/blank, the document title is used.
    /// </summary>
    public string? FooterLabel { get; init; }

    /// <summary>
    /// A pre-formatted date string for the footer (e.g. "2026-06-08"). When null
    /// and <see cref="ShowGeneratedDate"/> is set, the render-time date is used.
    /// Provided explicitly mainly so output is deterministic for tests.
    /// </summary>
    public string? GeneratedOn { get; init; }

    /// <summary>
    /// An optional classification / handling banner marking drawn centered and
    /// bold at the top <em>and</em> bottom of every page (in the margins, so it
    /// never overlaps the form body). For public-sector / compliance handling
    /// markings such as "CONTROLLED UNCLASSIFIED INFORMATION", "FOR OFFICIAL USE
    /// ONLY", "OFFICIAL", "DRAFT", or "CONFIDENTIAL". Null/blank = no banner.
    /// </summary>
    public string? BannerText { get; init; }

    /// <summary>Default options: Letter, footer with page numbers and date.</summary>
    public static PdfRenderOptions Default { get; } = new();
}
