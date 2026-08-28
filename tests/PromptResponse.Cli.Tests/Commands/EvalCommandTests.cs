using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// One CEL implementation, exposed so nothing else has to write a second.
/// </summary>
/// <remarks>
/// This repository already shipped a CEL-<i>flavoured</i> engine once and had to correct
/// the specification when the difference was noticed. A demo or an SDK writing its own
/// would repeat that in a new language, and its answers would drift from the ones that
/// count. So expression results are asked for, not reimplemented.
/// </remarks>
public class EvalCommandTests : IDisposable
{
    private readonly EvalCommand _command = new(new AprJsonSerializer());
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "apr-eval-" + Guid.NewGuid().ToString("N"));

    public EvalCommandTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "doc.aprf");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Claim(string total) => $$"""
        {
          "version": "1.0-beta", "documentType": "filledForm",
          "metadata": { "title": "Claim", "templateId": "c", "templateVersion": "1.0" },
          "sections": [{ "id": "s", "title": "Claim", "prompts": [
            { "id": "subtotal", "label": "Subtotal", "response": "100.00",
              "hints": { "expectedDataType": "currency" } },
            { "id": "tax", "label": "Tax", "response": "20.00",
              "hints": { "expectedDataType": "currency" } },
            { "id": "total", "label": "Total", "response": "{{total}}",
              "hints": { "expectedDataType": "currency",
                "exprValidation": "double(subtotal) + double(tax) == double(total) ? '' : 'Total does not add up'" } }
          ]}]
        }
        """;

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
    public async Task ASatisfiedRule_IsReportedAsSatisfied()
    {
        var (exit, output) = await Run(Write(Claim("120.00")));

        exit.Should().Be(0);
        output.Should().Contain("satisfied")
            .And.Contain("Every rule the form states about itself is satisfied.");
    }

    [Fact]
    public async Task AFailingRule_IsReportedWithTheAuthorsOwnMessage()
    {
        var (exit, output) = await Run(Write(Claim("150.00")));

        exit.Should().Be(0,
            "a failing rule is the author's message about an answer, not a reason to " +
            "reject the document. No error may arise from the content of a response " +
            "(specification 6.1), and the exit code says whether the file could be read");
        output.Should().Contain("Total does not add up");
    }

    [Fact]
    public async Task TheJsonForm_IsWhatAnotherToolWouldConsume()
    {
        var (_, output) = await Run(Write(Claim("150.00")), "--json");

        using var parsed = JsonDocument.Parse(output);
        var expressions = parsed.RootElement.GetProperty("expressions").EnumerateArray().ToList();

        expressions.Should().ContainSingle("only one prompt carries an expression");
        var total = expressions[0];
        total.GetProperty("promptId").GetString().Should().Be("total");
        total.GetProperty("validationMessage").GetString().Should().Be("Total does not add up");
        total.GetProperty("response").GetString().Should().Be("150.00",
            "a consumer needs the answer beside the verdict, or it has to go and find it");
    }

    [Fact]
    public async Task AComputedValueIsReported_AndSoIsBeingUnableToComputeOne()
    {
        var path = Write("""
            {
              "version": "1.0-beta", "documentType": "filledForm",
              "metadata": { "title": "T", "templateId": "t", "templateVersion": "1.0" },
              "sections": [{ "id": "s", "title": "S", "prompts": [
                { "id": "qty", "label": "Qty", "response": "",
                  "hints": { "expectedDataType": "number" } },
                { "id": "line", "label": "Line total", "response": "",
                  "hints": { "expectedDataType": "currency", "exprValue": "qty * 2.0" } }
              ]}]
            }
            """);

        var (_, output) = await Run(path);

        output.Should().Contain("could not evaluate",
            "a blank driver means there is nothing to compute from, and saying so is more " +
            "use than printing an empty answer as though it were the result");
    }

    [Fact]
    public async Task ADocumentWithNoExpressions_SaysSo()
    {
        var path = Write("""
            {"version":"1.0-beta","metadata":{"title":"T"},"sections":
             [{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":"x"}]}]}
            """);

        var (exit, output) = await Run(path);

        exit.Should().Be(0);
        output.Should().Contain("no expressions");
    }

    [Fact]
    public async Task AnUnreadableFile_ExitsOne() =>
        (await Run(Write("{ not json"))).Exit.Should().Be(1);

    [Fact]
    public async Task NoArguments_ExitsOne() => (await Run()).Exit.Should().Be(1);
}
