using Excise.Core.Document;
using PromptResponse.Core.Models;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Conversion.Pdf;

/// <summary>Where a discovered field came from.</summary>
public enum FieldOrigin
{
    /// <summary>A real AcroForm widget — exact identity, exact placement.</summary>
    AcroFormWidget,

    /// <summary>Inferred from the embedded text layer and its coordinates.</summary>
    TextLayer,

    /// <summary>Recovered by OCR over a rendered page.</summary>
    Ocr,
}

/// <summary>How confident the pipeline is in a field's label.</summary>
public enum LabelSource
{
    /// <summary>No label found; the field id is standing in for one.</summary>
    None,

    /// <summary>The form author's own <c>/TU</c> text.</summary>
    FormAuthor,

    /// <summary>Recovered from text printed near the field.</summary>
    NearbyText,

    /// <summary>Supplied or corrected by the local model.</summary>
    Model,
}

/// <summary>
/// One field the pipeline has found, in the shape every phase reads and writes.
/// </summary>
/// <param name="Id">Stable identity. For an AcroForm field this is its fully qualified name.</param>
/// <param name="Label">The question text, or null while unresolved.</param>
/// <param name="LabelSource">Where <paramref name="Label"/> came from.</param>
/// <param name="Origin">Which discovery phase produced this field.</param>
/// <param name="PageNumber">1-based page.</param>
/// <param name="TargetRect">Where the answer goes, in PDF user space.</param>
/// <param name="ExpectedDataType">An APR hint — text, boolean, and so on.</param>
/// <param name="Options">Choice options, when the field offers a fixed set.</param>
/// <param name="NeedsReview">
/// Why this field could not be resolved deterministically. This list is the
/// model phase's work queue: an empty list on every field means the model never
/// loads, which is the whole load-minimising premise.
/// </param>
public sealed record DiscoveredField(
    string Id,
    string? Label,
    LabelSource LabelSource,
    FieldOrigin Origin,
    int PageNumber,
    PdfRectangle? TargetRect,
    string? ExpectedDataType = null,
    IReadOnlyList<string>? Options = null,
    IReadOnlyList<string>? NeedsReview = null)
{
    /// <summary>Whether a human-readable question was resolved for this field.</summary>
    public bool HasLabel => !string.IsNullOrWhiteSpace(Label);
}

/// <summary>One run of printed text, with where it sits.</summary>
/// <param name="PageNumber">1-based page.</param>
/// <param name="Text">The text.</param>
/// <param name="Rect">Its bounding box in PDF user space.</param>
/// <param name="Role">What the pipeline believes this text is; null until classified.</param>
public sealed record TextSpan(int PageNumber, string Text, PdfRectangle Rect, SpanRole? Role = null);

/// <summary>What a run of printed text is doing on the page.</summary>
public enum SpanRole
{
    /// <summary>A section heading.</summary>
    Heading,

    /// <summary>The question text for a field.</summary>
    Label,

    /// <summary>Guidance to the person filling the form, not a question.</summary>
    Instruction,

    /// <summary>Pre-printed content that is neither — page numbers, form codes, rules.</summary>
    Furniture,
}

/// <summary>
/// Everything known about the document as it moves through the pipeline.
/// </summary>
/// <remarks>
/// A record rather than a mutable bag so each phase's contribution is a visible
/// transformation. #427 wants a durable per-phase trace; keeping the state
/// immutable is what makes capturing one a matter of writing it out rather than
/// of reconstructing what changed.
/// </remarks>
/// <param name="SourcePath">The PDF being converted.</param>
/// <param name="Title">Title for the resulting template.</param>
/// <param name="Sources">Which field sources the PDF offers, from detection.</param>
/// <param name="Fields">Fields discovered so far.</param>
/// <param name="Spans">Printed text with geometry.</param>
/// <param name="Document">The assembled template, once assembly has run.</param>
public sealed record ConversionState(
    string SourcePath,
    string Title,
    PdfSourceReport? Sources = null,
    IReadOnlyList<DiscoveredField>? Fields = null,
    IReadOnlyList<TextSpan>? Spans = null,
    AprDocument? Document = null)
{
    /// <summary>Fields discovered so far, never null.</summary>
    public IReadOnlyList<DiscoveredField> FieldsOrEmpty => Fields ?? [];

    /// <summary>Text spans found so far, never null.</summary>
    public IReadOnlyList<TextSpan> SpansOrEmpty => Spans ?? [];

    /// <summary>Fields the deterministic phases could not resolve, in the order found.</summary>
    public IReadOnlyList<DiscoveredField> ReviewQueue =>
        [.. FieldsOrEmpty.Where(f => f.NeedsReview is { Count: > 0 })];
}
