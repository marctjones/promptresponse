namespace PromptResponse.Core;

/// <summary>
/// The single source of truth for the only APR document format version this build accepts.
/// </summary>
/// <remarks>
/// <para>
/// Three different numbers describe this project and only one of them lives here:
/// </para>
/// <list type="bullet">
///   <item><b>Format version</b> (this class) — the <c>aprVersion</c> member written into
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
}
