using System.Diagnostics;

namespace PromptResponse.Conversion.Pdf;

/// <summary>What happened when a phase ran.</summary>
public enum PhaseStatus
{
    /// <summary>The phase ran and contributed.</summary>
    Completed,

    /// <summary>The phase had nothing to do on this document, by design.</summary>
    Skipped,

    /// <summary>The phase is not built yet.</summary>
    NotImplemented,

    /// <summary>The phase failed. Conversion continues; the report says so.</summary>
    Failed,
}

/// <summary>One phase's contribution, reported whether or not it did anything.</summary>
/// <param name="Name">The phase's name.</param>
/// <param name="Status">What happened.</param>
/// <param name="Detail">One line a person can act on.</param>
/// <param name="Elapsed">How long it took.</param>
/// <param name="FieldsAfter">Fields known after this phase.</param>
/// <param name="LabelledAfter">Fields carrying a human-readable label after this phase.</param>
/// <param name="SpansAfter">Text spans known after this phase.</param>
public sealed record PhaseOutcome(
    string Name,
    PhaseStatus Status,
    string Detail,
    TimeSpan Elapsed,
    int FieldsAfter,
    int LabelledAfter,
    int SpansAfter);

/// <summary>
/// One step of the conversion. Every step has the same shape so the pipeline can
/// report on each of them identically.
/// </summary>
public interface IConversionPhase
{
    /// <summary>The phase's name, as it appears in the report.</summary>
    string Name { get; }

    /// <summary>What this phase is for, in one line.</summary>
    string Purpose { get; }

    /// <summary>Runs the phase, returning the new state and what happened.</summary>
    (ConversionState State, PhaseStatus Status, string Detail) Run(ConversionState state);
}

/// <summary>The result of a conversion: the document, and how each phase did.</summary>
/// <param name="State">Final state, including the assembled document if there is one.</param>
/// <param name="Phases">Every phase's outcome, in order, including those that did nothing.</param>
public sealed record ConversionResult(ConversionState State, IReadOnlyList<PhaseOutcome> Phases)
{
    /// <summary>Whether a template was produced at all.</summary>
    public bool Succeeded => State.Document is not null;

    /// <summary>Phases that are not built yet.</summary>
    public IReadOnlyList<PhaseOutcome> Unbuilt =>
        [.. Phases.Where(p => p.Status == PhaseStatus.NotImplemented)];

    /// <summary>Fields the deterministic phases left for the model.</summary>
    public int ReviewQueueSize => State.ReviewQueue.Count;
}

/// <summary>
/// Runs the conversion phases in order and reports on every one of them.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline reports each phase separately on purpose. An end-to-end score
/// says a conversion was bad without saying which step was bad, which is the
/// only actionable part — the model benchmark's clearest example was
/// Granite-Docling producing 130 "fields" for a W-4 whose ground truth is 19,
/// nearly all of it instructional prose read as fields. "F1 0.18" does not say
/// that; "the span classifier cannot separate instructions from labels" does.
/// </para>
/// <para>
/// A phase that is not built reports <see cref="PhaseStatus.NotImplemented"/>
/// rather than quietly passing the state through. A pipeline that looks like it
/// ran eight steps when it ran three is the more expensive kind of wrong.
/// </para>
/// </remarks>
public sealed class ConversionPipeline(IReadOnlyList<IConversionPhase> phases)
{
    /// <summary>The phases this pipeline will run, in order.</summary>
    public IReadOnlyList<IConversionPhase> Phases { get; } = phases;

    /// <summary>The full deterministic pipeline, in the order the phases must run.</summary>
    public static ConversionPipeline Default() => new(
    [
        new DetectSourcesPhase(),
        new ExtractTextPhase(),
        new DiscoverAcroFormFieldsPhase(honourFormAuthorLabels: true),
        new DiscoverTextLayerFieldsPhase(),
        new ClassifySpansPhase(),
        new RecoverLabelsPhase(),
        new MergeSplitEntriesPhase(),
        new AssembleDocumentPhase(),
        new ModelTouchUpPhase(),
    ]);

    /// <summary>
    /// The same pipeline with the form author's own labels held back, so label
    /// recovery can be graded against them.
    /// </summary>
    /// <remarks>
    /// Not a debugging switch — it is the only mechanical oracle this project
    /// has for whether a recovered label is the <em>right</em> label. A form's
    /// <c>/TU</c> text is the author's own words for the field, so hiding it and
    /// then comparing what recovery found against it needs no human and no
    /// model. It is worth 128 checks on <c>fed-i9</c> alone.
    /// </remarks>
    public static ConversionPipeline WithoutFormAuthorLabels() => new(
    [
        new DetectSourcesPhase(),
        new ExtractTextPhase(),
        new DiscoverAcroFormFieldsPhase(honourFormAuthorLabels: false),
        new DiscoverTextLayerFieldsPhase(),
        new ClassifySpansPhase(),
        new RecoverLabelsPhase(),
        new MergeSplitEntriesPhase(),
        new AssembleDocumentPhase(),
        new ModelTouchUpPhase(),
    ]);

    /// <summary>Converts a PDF, running every phase and reporting on each.</summary>
    public ConversionResult Convert(string sourcePath, string? title = null)
    {
        var state = new ConversionState(
            sourcePath,
            string.IsNullOrWhiteSpace(title) ? TitleFromFileName(sourcePath) : title);

        var outcomes = new List<PhaseOutcome>();

        foreach (var phase in Phases)
        {
            var stopwatch = Stopwatch.StartNew();
            PhaseStatus status;
            string detail;

            try
            {
                (state, status, detail) = phase.Run(state);
            }
            catch (Exception exception)
            {
                // One phase failing must not lose the work the others did. The
                // report carries the failure rather than an exception replacing
                // the whole result.
                status = PhaseStatus.Failed;
                detail = $"{exception.GetType().Name}: {exception.Message}";
            }

            stopwatch.Stop();
            outcomes.Add(new PhaseOutcome(
                phase.Name,
                status,
                detail,
                stopwatch.Elapsed,
                state.FieldsOrEmpty.Count,
                state.FieldsOrEmpty.Count(f => f.HasLabel),
                state.SpansOrEmpty.Count));
        }

        return new ConversionResult(state, outcomes);
    }

    private static string TitleFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "Converted form" : name;
    }
}
