namespace PromptResponse.Core;

/// <summary>How this build relates to a document's declared format version.</summary>
public enum VersionCompatibility
{
    /// <summary>The version cannot be parsed as MAJOR.MINOR.</summary>
    Unparseable,

    /// <summary>A different major version. Incompatible — the document must be rejected.</summary>
    UnsupportedMajor,

    /// <summary>A minor version this build knows. Fully readable.</summary>
    Supported,

    /// <summary>
    /// The same major but a newer minor. Readable: the document may carry members this
    /// build does not understand, which are ignored but preserved.
    /// </summary>
    NewerMinor,
}

/// <summary>
/// The single source of truth for the APR document format version, and the rules for
/// deciding whether a given document can be read.
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
///   <item><b>Conformance corpus tag</b> — per release, <c>corpus/v1 @ &lt;sha&gt;</c>.</item>
/// </list>
/// <para>
/// <b>Compatibility is decided by MAJOR.MINOR alone.</b> A different MAJOR is
/// incompatible and is rejected. A newer MINOR is readable — that is what makes
/// additive change possible, and it only works because unknown members survive a
/// round-trip (see the <c>Extensions</c> property on each model class). Any
/// pre-release suffix (<c>-beta</c>) is informational and ignored when deciding
/// compatibility, so <c>1.0-beta</c> and <c>1.0</c> are the same format and the
/// eventual stable tag needs no migration and no legacy-version list.
/// </para>
/// <para>
/// To ship an additive change: add optional members, bump <see cref="KnownMinor"/>,
/// and leave <see cref="KnownMajor"/> alone. Older readers keep working.
/// </para>
/// </remarks>
public static class AprFormat
{
    /// <summary>The major version this build implements. A document with any other major is rejected.</summary>
    public const int KnownMajor = 1;

    /// <summary>The highest minor version this build fully understands.</summary>
    public const int KnownMinor = 0;

    /// <summary>The format version written into newly created documents.</summary>
    public const string CurrentVersion = "1.0-beta";

    /// <summary>A human-readable description of what this build accepts, for error messages.</summary>
    public static string SupportedVersionsDescription =>
        $"{KnownMajor}.x (this build understands up to {KnownMajor}.{KnownMinor})";

    /// <summary>
    /// Splits a version string into its numeric major and minor parts, ignoring any
    /// pre-release suffix. Returns false when the string is not MAJOR.MINOR.
    /// </summary>
    public static bool TryParse(string? version, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        // Compatibility ignores any pre-release suffix: "1.0-beta" is the "1.0" format.
        var core = version.Split('-', 2)[0].Trim();
        var parts = core.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out major) && major >= 0
            && int.TryParse(parts[1], out minor) && minor >= 0;
    }

    /// <summary>Decides how this build relates to a declared version.</summary>
    public static VersionCompatibility Classify(string? version)
    {
        if (!TryParse(version, out var major, out var minor))
        {
            return VersionCompatibility.Unparseable;
        }
        if (major != KnownMajor)
        {
            return VersionCompatibility.UnsupportedMajor;
        }
        return minor > KnownMinor ? VersionCompatibility.NewerMinor : VersionCompatibility.Supported;
    }

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

    /// <summary>Whether this build can read a document declaring the given version.</summary>
    public static bool IsSupported(string? version) =>
        Classify(version) is VersionCompatibility.Supported or VersionCompatibility.NewerMinor;
}
