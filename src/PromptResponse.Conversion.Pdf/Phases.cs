using Excise.Core.Document;
using PromptResponse.Core.Models;
using PromptResponse.Rendering.Pdf;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Phase 1. Decides which field sources the PDF offers, before anything tries to
/// read it.
/// </summary>
/// <remarks>
/// Everything downstream routes on this, and a wrong call is expensive in both
/// directions: treat a digitally-authored form as a scan and OCR re-derives text
/// the file already states exactly; treat a scan as text-bearing and the
/// converter produces an empty document.
/// </remarks>
public sealed class DetectSourcesPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "detect-sources";

    /// <inheritdoc/>
    public string Purpose => "Which field sources does this PDF offer: AcroForm, text layer, neither?";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        var report = PdfSourceDetector.Detect(state.SourcePath);
        var sources = report.IsImageOnly ? "image-only (needs OCR)" : report.Sources.ToString();

        return (
            state with { Sources = report },
            PhaseStatus.Completed,
            $"{sources}; {report.ImportableFieldCount} importable field(s), " +
            $"{report.CharacterCount} character(s) across {report.Pages.Count} page(s)");
    }
}

/// <summary>
/// Phase 2. Reads the printed text with its coordinates.
/// </summary>
/// <remarks>
/// Exact text with exact page positions, straight from the file — no OCR error
/// and no model. This is the evidence every later phase reasons over: what the
/// form says, and where it says it.
/// </remarks>
public sealed class ExtractTextPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "extract-text";

    /// <inheritdoc/>
    public string Purpose => "Read the printed text and where each run of it sits on the page.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        if (state.Sources?.HasTextLayer != true)
        {
            return (state, PhaseStatus.Skipped,
                "no text layer; the pixels would need OCR, which is not built");
        }

        // Reuses the committed reference extractor rather than a second copy of
        // the same logic, so the text the converter reads is exactly the text
        // the reference files record and grade against.
        var reference = PdfReferenceExtractor.Extract(state.SourcePath, state.Title);
        var spans = reference.Lines
            .Select(l => new TextSpan(
                l.Page,
                l.Text,
                new PdfRectangle(l.Rect.Left, l.Rect.Bottom, l.Rect.Right, l.Rect.Top)))
            .ToList();

        return (state with { Spans = spans }, PhaseStatus.Completed,
            $"{spans.Count} text span(s) with geometry");
    }
}

/// <summary>
/// Phase 3. Takes the fields the PDF already declares.
/// </summary>
/// <remarks>
/// Free and exact where it applies: an AcroForm states its fields, their types,
/// their options and where each one sits. Nothing later can improve on that, so
/// it runs before any inference and later phases only fill gaps it leaves.
/// </remarks>
public sealed class DiscoverAcroFormFieldsPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "discover-acroform";

    /// <inheritdoc/>
    public string Purpose => "Take the fields the PDF declares outright, with their types and positions.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        if (state.Sources?.HasAcroForm != true)
        {
            return (state, PhaseStatus.Skipped, "no AcroForm; fields must be found from the page");
        }

        var manifest = PdfWidgetManifest.Extract(state.SourcePath);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var fields = new List<DiscoveredField>();

        foreach (var entry in manifest.Importable)
        {
            var id = UniqueId(string.IsNullOrWhiteSpace(entry.FullName) ? "field" : entry.FullName, seen);
            var label = Meaningful(entry.Tooltip) ? entry.Tooltip : null;

            // The review queue is built here rather than at the end: a field
            // whose only name is `f1_01[0]` is a known deficiency the moment it
            // is read, and saying so now is what lets a later phase target it.
            var review = new List<string>();
            if (label is null)
            {
                review.Add("no human-readable label; the field name is not a question");
            }

            fields.Add(new DiscoveredField(
                id,
                label,
                label is null ? LabelSource.None : LabelSource.FormAuthor,
                FieldOrigin.AcroFormWidget,
                entry.PageNumber ?? 0,
                entry.Rect,
                ExpectedDataType: DataTypeFor(entry.FieldType),
                Options: null,
                NeedsReview: review.Count == 0 ? null : review));
        }

        var labelled = fields.Count(f => f.HasLabel);
        return (state with { Fields = fields }, PhaseStatus.Completed,
            $"{fields.Count} field(s); {labelled} arrived with the form author's own label, " +
            $"{fields.Count - labelled} need one recovered");
    }

    private static bool Meaningful(string? tooltip)
    {
        // A present tooltip is not a good tooltip: every field in ct-dmv-a25
        // carries a /TU and every one of them says "TextField1". Treating those
        // as labels would report the problem as solved.
        if (string.IsNullOrWhiteSpace(tooltip))
        {
            return false;
        }

        return !ImportQualityHeuristics.LooksCryptic(tooltip);
    }

    private static string DataTypeFor(PdfFieldType type) => type switch
    {
        PdfFieldType.Button => "boolean",
        PdfFieldType.Choice => "text",
        _ => "text",
    };

    private static string UniqueId(string candidate, HashSet<string> seen)
    {
        var id = candidate;
        for (var suffix = 2; !seen.Add(id); suffix++)
        {
            id = $"{candidate}#{suffix}";
        }

        return id;
    }
}

/// <summary>
/// Phase 4. Finds fields on a form that declares none — the converter's actual target.
/// </summary>
/// <remarks>
/// The blank a person writes on is not absence: it is a drawn vector primitive,
/// a long thin rectangle or line, and a checkbox is a small square. Reading
/// those gives what the text layer alone cannot — where the answer goes. Not
/// built (#424).
/// </remarks>
public sealed class DiscoverTextLayerFieldsPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "discover-text-layer";

    /// <inheritdoc/>
    public string Purpose => "Find the blanks and checkboxes on a form that declares no fields.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        if (state.Sources?.HasAcroForm == true)
        {
            return (state, PhaseStatus.Skipped, "the AcroForm already stated every field");
        }

        return (state, PhaseStatus.NotImplemented,
            "no vector-geometry discovery yet (#424); a form without an AcroForm yields no fields");
    }
}

/// <summary>
/// Phase 5. Decides what each run of text is doing: heading, label, instruction, furniture.
/// </summary>
/// <remarks>
/// The phase with the largest downside when wrong, and no mechanical oracle.
/// Reading instructions as fields is how a model produced 130 "fields" for a
/// W-4 whose ground truth is 19. Not built.
/// </remarks>
public sealed class ClassifySpansPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "classify-spans";

    /// <inheritdoc/>
    public string Purpose => "Separate headings and questions from instructions and page furniture.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        if (state.SpansOrEmpty.Count == 0)
        {
            return (state, PhaseStatus.Skipped, "no text to classify");
        }

        return (state, PhaseStatus.NotImplemented,
            $"{state.SpansOrEmpty.Count} span(s) left unclassified (#422)");
    }
}

/// <summary>
/// Phase 6. Gives a cryptic field a real question by reading the text beside it.
/// </summary>
/// <remarks>
/// The highest value-per-effort step in the milestone: two thirds of the
/// corpus's fields are cryptic, and every one of those forms carries a text
/// layer. The widget knows exactly where it sits and the text layer knows
/// exactly what is printed nearby. Not built (#426).
/// </remarks>
public sealed class RecoverLabelsPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "recover-labels";

    /// <inheritdoc/>
    public string Purpose => "Turn a cryptic field name into the question printed beside it.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        if (state.FieldsOrEmpty.Count == 0)
        {
            // Distinguished from the success case below on purpose: "no fields
            // need a label" and "no fields were found" produce the same empty
            // set, and reporting the second as the first is a phase claiming
            // success for work that never happened.
            return (state, PhaseStatus.Skipped, "no fields were discovered, so there is nothing to label");
        }

        var needing = state.FieldsOrEmpty.Count(f => !f.HasLabel);
        if (needing == 0)
        {
            return (state, PhaseStatus.Skipped, "every field already carries a real label");
        }

        if (state.SpansOrEmpty.Count == 0)
        {
            return (state, PhaseStatus.Skipped, "no text layer to recover labels from");
        }

        return (state, PhaseStatus.NotImplemented,
            $"{needing} field(s) still need a label (#426)");
    }
}

/// <summary>
/// Phase 7. Turns the discovered fields into an APR template.
/// </summary>
/// <remarks>
/// The one phase that must never invent: it arranges what earlier phases found
/// and adds nothing of its own. A field with no label still becomes a prompt,
/// carrying its deficiency into the quality report rather than being dropped —
/// a silently missing question is worse than a badly named one.
/// </remarks>
public sealed class AssembleDocumentPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "assemble";

    /// <inheritdoc/>
    public string Purpose => "Arrange the discovered fields into a valid APR template.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        var fields = state.FieldsOrEmpty;
        if (fields.Count == 0)
        {
            return (state, PhaseStatus.Skipped, "no fields were discovered, so there is nothing to assemble");
        }

        var sections = fields
            .GroupBy(f => f.PageNumber)
            .OrderBy(g => g.Key)
            .Select(g => new Section
            {
                Id = $"page-{g.Key}",
                Title = $"Page {g.Key}",
                Prompts = [.. g.Select(ToPrompt)],
            })
            .ToList();

        var document = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = state.Title },
            Sections = sections,
        };

        return (state with { Document = document }, PhaseStatus.Completed,
            $"{sections.Count} section(s), {fields.Count} prompt(s)");
    }

    private static Prompt ToPrompt(DiscoveredField field) => new()
    {
        Id = field.Id,
        // Falling back to the id keeps the prompt valid (APR requires a
        // non-empty label) while leaving the deficiency visible in the report.
        Label = field.HasLabel ? field.Label! : field.Id,
        Response = string.Empty,
        Hints = new PromptHints { ExpectedDataType = field.ExpectedDataType },
    };
}

/// <summary>
/// Phase 8. Lets a small local model repair what determinism could not decide.
/// </summary>
/// <remarks>
/// Runs per flagged field, never per document, and only on the residue the
/// earlier phases left. On a well-formed fillable PDF that residue should be
/// empty and the model should never load at all. Not built (#423), and
/// deliberately last: the deterministic phases have to be as good as they can be
/// before anything is asked of a model.
/// </remarks>
public sealed class ModelTouchUpPhase : IConversionPhase
{
    /// <inheritdoc/>
    public string Name => "model-touch-up";

    /// <inheritdoc/>
    public string Purpose => "Ask a small local model only about the fields determinism could not resolve.";

    /// <inheritdoc/>
    public (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state)
    {
        if (state.FieldsOrEmpty.Count == 0)
        {
            return (state, PhaseStatus.Skipped,
                "no fields were discovered; an empty review queue here means the earlier " +
                "phases found nothing, not that they resolved everything");
        }

        var queue = state.ReviewQueue.Count;
        if (queue == 0)
        {
            return (state, PhaseStatus.Skipped,
                "nothing was flagged, so no model would be loaded — which is the intended outcome");
        }

        return (state, PhaseStatus.NotImplemented,
            $"{queue} field(s) would be sent to a local model (#423); no model is loaded by this build");
    }
}
