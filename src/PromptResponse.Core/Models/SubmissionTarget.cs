using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Models;

/// <summary>One <c>submissionUrls</c> entry: a place a completed form may be sent, and how.</summary>
/// <remarks>
/// An entry is a URL string or an object. The string is shorthand for a <c>put</c> entry
/// with that URL (APR-MODEL-134), and it is written back as the string it was read as, so
/// opening and saving a form changes nothing in it. An object is written back with the
/// members it arrived with and no others.
/// </remarks>
[JsonConverter(typeof(SubmissionTargetConverter))]
public sealed class SubmissionTarget
{
    /// <summary>The kind of a pre-signed PUT target, and of every string entry.</summary>
    public const string Put = "put";

    /// <summary>The kind of a pre-signed POST target, which carries its policy fields.</summary>
    public const string Post = "post";

    /// <summary>Gets or sets the entry's kind: <see cref="Put"/> or <see cref="Post"/>.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the target the request is sent to, used verbatim.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets the policy fields a <c>post</c> target is sent with, each verbatim.</summary>
    public JsonElement? Fields { get; set; }

    /// <summary>Gets or sets the RFC 3339 instant after which the grant is spent.</summary>
    public string? Expires { get; set; }

    /// <summary>Gets or sets the URL that issues a replacement entry. Never fetched unasked.</summary>
    public string? Refresh { get; set; }

    /// <summary>Gets or sets whether the entry was written as a URL string.</summary>
    public bool IsShorthand { get; set; }

    /// <summary>Gets or sets the members the entry carried that the format does not define.</summary>
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>A string entry: a <c>put</c> target with this URL.</summary>
    public static SubmissionTarget FromUrl(string url) => new() { Kind = Put, Url = url, IsShorthand = true };

    /// <summary>A string entry: a <c>put</c> target with this URL.</summary>
    public static implicit operator SubmissionTarget(string url) => FromUrl(url);
}
