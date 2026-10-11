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
/// Beta.7 adds no member to a record and removes none, so a reader accepts it
/// alongside beta.6; a document must declare one of <see cref="AcceptedVersions"/>.
/// </remarks>
public static class AprFormat
{
    /// <summary>The format version written into newly created documents.</summary>
    public const string CurrentVersion = "1.0-beta.7";

    /// <summary>Whether this build can read a document declaring the given version.</summary>
    public static bool IsSupported(string? version) =>
        version is not null && AcceptedVersions.Contains(version, StringComparer.Ordinal);

    /// <summary>Every format version a reader accepts, current first.</summary>
    public static IReadOnlyList<string> AcceptedVersions { get; } = ["1.0-beta.7", "1.0-beta.6"];
}
