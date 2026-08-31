using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// The exit-code contract, which is what a receiving pipeline actually consumes.
/// </summary>
/// <remarks>
/// A shell script routing submissions reads the exit code, not the prose. These pin it:
/// 0 means handle it automatically, 2 means route it to a person or a model, 1 means the
/// file could not be read at all. Getting 0 and 2 the wrong way round would send bad data
/// straight through a pipeline, so they are asserted rather than assumed.
/// </remarks>
public class ReviewCommandTests : IDisposable
{
    private readonly ReviewCommand _command = new(new AprJsonSerializer());
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "apr-review-" + Guid.NewGuid().ToString("N"));

    public ReviewCommandTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string Write(string name, string json)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, json);
        return path;
    }

    private static string Form(string prompts) => $$"""
        {
          "version": "1.0-beta.6", "documentType": "filledForm",
          "metadata": { "title": "T", "templateId": "t", "templateVersion": "1.0" },
          "sections": [{ "id": "s", "title": "S", "prompts": [{{prompts}}] }]
        }
        """;

    private const string Clean =
        """{ "id": "a", "label": "Email", "response": "ada@example.com", "hints": { "expectedDataType": "email" } }""";

    private const string AdvisoryOnly =
        """{ "id": "a", "label": "Dept", "response": "Skunkworks", "hints": { "expectedDataType": "select", "suggestedValues": ["Sales"] } }""";

    private const string NeedsReview =
        """{ "id": "a", "label": "Email", "response": "nonsense", "hints": { "expectedDataType": "email" } }""";

    private async Task<(int Exit, string Output)> Run(params string[] args)
    {
        var writer = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(writer);
            return (await _command.ExecuteAsync(args), writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public async Task ACleanSubmission_ExitsZero() =>
        (await Run(Write("clean.aprf", Form(Clean)))).Exit.Should().Be(0,
            "nothing was flagged, so a pipeline should process it without asking anyone");

    [Fact]
    public async Task AdvisoriesAlone_ExitZero() =>
        (await Run(Write("advisory.aprf", Form(AdvisoryOnly)))).Exit.Should().Be(0,
            "answering outside the suggested options is allowed by the format and is often " +
            "right; stopping a pipeline for it would make the signal useless");

    [Fact]
    public async Task AdvisoriesUnderStrict_ExitTwo() =>
        (await Run(Write("advisory.aprf", Form(AdvisoryOnly)), "--strict")).Exit.Should().Be(2,
            "a receiver who wants to see everything can ask for that");

    [Fact]
    public async Task AFieldAMachineCannotRead_ExitsTwo() =>
        (await Run(Write("bad.aprf", Form(NeedsReview)))).Exit.Should().Be(2,
            "route it to a person, a model, or back to the submitter");

    [Fact]
    public async Task AnUnreadableFile_ExitsOne_DistinctFromNeedingReview()
    {
        var (exit, _) = await Run(Write("broken.aprf", "{ this is not json"));

        exit.Should().Be(1,
            "\"I could not read this\" and \"this needs a human\" are different outcomes and " +
            "a pipeline routes them differently");
    }

    [Fact]
    public async Task MissingFile_ExitsOne() =>
        (await Run(Path.Combine(_dir, "absent.aprf"))).Exit.Should().Be(1);

    [Fact]
    public async Task NoArguments_ExitsOne() =>
        (await Run()).Exit.Should().Be(1);

    [Fact]
    public async Task JsonOutput_IsMachineReadableAndSaysTheDocumentIsValid()
    {
        var (exit, output) = await Run(Write("bad.aprf", Form(NeedsReview)), "--json");

        exit.Should().Be(2);
        using var parsed = JsonDocument.Parse(output);
        var root = parsed.RootElement;

        root.GetProperty("verdict").GetString().Should().Be("reviewRequired");
        root.GetProperty("contentIsAlwaysValid").GetBoolean().Should().BeTrue(
            "restated in every report so no downstream system reads \"review required\" as " +
            "\"invalid\"");
        root.GetProperty("findings").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("code").GetString().Should().Be("TYPE_MISMATCH",
                "routing should key on the stable code, not on the wording of the message");
    }

    [Fact]
    public async Task TheHumanReport_SaysPlainlyThatTheDocumentIsValid()
    {
        var (_, output) = await Run(Write("bad.aprf", Form(NeedsReview)));

        output.Should().Contain("The document is valid",
            "someone reading a page of findings needs telling that none of them mean the " +
            "submission was wrong to accept");
    }
}
