using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>The CLI answers the driver contract, and answers it as itself.</summary>
public class ConformanceCommandTests
{
    private static async Task<JsonDocument> Run(string suite)
    {
        var input = Console.In;
        var output = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetIn(new StringReader(suite));
            Console.SetOut(writer);
            (await new ConformanceCommand().ExecuteAsync([])).Should().Be(0);
            return JsonDocument.Parse(writer.ToString());
        }
        finally
        {
            Console.SetIn(input);
            Console.SetOut(output);
        }
    }

    private const string Valid =
        "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":"
        + "[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}";

    private static string Suite(string document, string representation = "jsonc") =>
        $"{{\"cases\":[{{\"id\":\"c\",\"representation\":\"{representation}\","
        + $"\"document\":{JsonSerializer.Serialize(document)}}}]}}";

    [Fact]
    public async Task ItDeclaresWhoItIs_AndWhatItClaims()
    {
        using var report = await Run(Suite(Valid));
        var implementation = report.RootElement.GetProperty("implementation");

        implementation.GetProperty("name").GetString().Should().Contain("CLI",
            "the report says which surface answered, because two surfaces answer this suite");
        implementation.GetProperty("profiles").EnumerateArray()
            .Select(p => p.GetString()).Should().Contain("core");
    }

    [Fact]
    public async Task AValidDocument_IsAcceptedWithItsDigest()
    {
        using var report = await Run(Suite(Valid));
        var result = report.RootElement.GetProperty("results")[0];

        result.GetProperty("outcome").GetString().Should().Be("valid");
        result.GetProperty("digest").GetString().Should().StartWith("sha256:",
            "acceptance alone is a claim any program can make");
    }

    [Fact]
    public async Task AnInvalidDocument_IsRefusedUnderTheCodeTheFormatNames()
    {
        var empty = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":"
            + "[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}]}";

        using var report = await Run(Suite(empty));
        var result = report.RootElement.GetProperty("results")[0];

        result.GetProperty("outcome").GetString().Should().Be("reject");
        result.GetProperty("diagnostic").GetString().Should().Be("EMPTY_SECTION");
    }

    [Fact]
    public async Task AParseFailure_CarriesTheParseStagesOwnCode()
    {
        var duplicate = "{\"aprVersion\":\"1.0-beta.6\",\"aprVersion\":\"1.0-beta.6\","
            + "\"metadata\":{\"title\":\"T\"},\"sections\":[]}";

        using var report = await Run(Suite(duplicate));
        var result = report.RootElement.GetProperty("results")[0];

        result.GetProperty("outcome").GetString().Should().Be("reject");
        result.GetProperty("diagnostic").GetString().Should().Be("DUPLICATE_MEMBER",
            "a document that will not parse was never validated");
    }
}
