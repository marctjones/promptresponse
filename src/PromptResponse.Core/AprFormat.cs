namespace PromptResponse.Core;

/// <summary>
/// The single source of truth for the only APR document format version this build accepts.
/// </summary>
/// <remarks>
/// <para>
/// Three different numbers describe this project and only one of them lives here:
/// </para>
/// <list type="bullet">
///   <item><b>Format version</b> (this class) — the <c>version</c> field written into
///   every <c>.aprt</c>/<c>.aprf</c>. It changes only when the wire format changes,
///   never per release.</item>
///   <item><b>Specification document version</b> — per release, in
///   <c>docs/APR_SPECIFICATION.md</c>.</item>
///   <item><b>Conformance corpus tag</b> — beta.6, in <c>tests/Conformance/beta6</c>.</item>
/// </list>
/// This repository has not made a public release, so it deliberately has no wire
/// compatibility policy: a document must declare the exact current version.
/// </remarks>
public static class AprFormat
{
    /// <summary>The format version written into newly created documents.</summary>
    public const string CurrentVersion = "1.0-beta.6";

    /// <summary>Whether this build can read a document declaring the given version.</summary>
    public static bool IsSupported(string? version) =>
        string.Equals(version, CurrentVersion, StringComparison.Ordinal);

    /// <summary>
    /// Member names that were deliberately removed from the format and MUST NOT be
    /// preserved, even though they are unrecognised.
    /// </summary>
    /// <remarks>
    /// Unknown members are normally kept so the format can grow additively. Retired
    /// names are the exception: these encode presentation, which APR excludes by
    /// design. Preserving them would let a renderer smuggle layout back into the
    /// document and make "no presentation data" unenforceable — a legacy file's
    /// column width would then live forever. Dropping them is how a removal actually
    /// takes effect.
    ///
    /// Only ever add a name here for a member the specification has retired. An
    /// unrecognised member that is merely from the future belongs in
    /// <c>Extensions</c>, not here.
    /// </remarks>
    public static readonly IReadOnlySet<string> RetiredMembers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Table column presentation, removed before 1.0 (see specification section 4.5).
            "width", "alignment", "color", "background", "fontSize", "bold", "style",
        };

    /// <summary>Removes retired members from a captured extension bag, in place.</summary>
    public static void DropRetiredMembers(IDictionary<string, System.Text.Json.JsonElement>? extensions)
    {
        if (extensions is null)
        {
            return;
        }
        foreach (var name in extensions.Keys.Where(RetiredMembers.Contains).ToList())
        {
            extensions.Remove(name);
        }
    }

}
