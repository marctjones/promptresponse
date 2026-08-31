using System.Text;
using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Cli.Tests.Robustness;

/// <summary>
/// Points hostile and corrupted files at the read-only commands.
/// </summary>
/// <remarks>
/// <para>
/// The core parser is fuzzed (PromptResponse.Core.Tests ParserFuzzTests), but the CLI is
/// the surface most likely to meet a file nobody vouched for - it is what a script runs
/// over an inbox, and what a pipeline runs over whatever arrived. Robustness was the one
/// dimension the CLI had no gate for at all.
/// </para>
/// <para>
/// The contract has three parts, and the third is the one that matters in practice: the
/// command must return a non-zero exit code, must not throw, and must not print a stack
/// trace. An exit code is what a shell script branches on, and a stack trace tells the
/// person holding a broken file about our internals instead of about their file.
/// </para>
/// </remarks>
public class CliRobustnessTests : IDisposable
{
    private readonly AprJsonSerializer _serializer = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "apr-cli-fuzz-" + Guid.NewGuid().ToString("N"));

    public CliRobustnessTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static string CorpusDir => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "v1");

    public static IEnumerable<object[]> HostileInputs()
    {
        var valid = Path.Combine(CorpusDir, "valid");
        var sample = Directory.Exists(valid)
            ? Directory.GetFiles(valid, "*.apr*").OrderBy(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault()
            : null;
        var json = sample is null ? "{\"version\":\"1.0-beta.6\",\"sections\":[]}" : File.ReadAllText(sample);

        yield return ["empty file", Array.Empty<byte>()];
        yield return ["not json at all", Encoding.UTF8.GetBytes("this is a text file, not a form")];
        yield return ["truncated halfway", Encoding.UTF8.GetBytes(json[..(json.Length / 2)])];
        yield return ["truncated at one byte", Encoding.UTF8.GetBytes(json)[..1]];
        yield return ["deeply nested", Encoding.UTF8.GetBytes(new string('[', 50_000) + new string(']', 50_000))];
        yield return ["utf-16 encoded", Encoding.Unicode.GetBytes(json)];
        yield return ["null bytes", new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }];
        yield return ["binary garbage", Enumerable.Range(0, 4096).Select(i => (byte)(i * 37 % 256)).ToArray()];
        yield return ["json but not a document", Encoding.UTF8.GetBytes("[1, 2, 3]")];
        yield return ["object of wrong shape", Encoding.UTF8.GetBytes("{\"unrelated\": {\"nested\": true}}")];
    }

    [Theory]
    [MemberData(nameof(HostileInputs))]
    public async Task ReadOnlyCommands_RefuseHostileInput_WithoutCrashingOrLeakingAStackTrace(
        string description, byte[] content)
    {
        var path = Path.Combine(_dir, "hostile.aprt");
        await File.WriteAllBytesAsync(path, content);

        foreach (var (name, command) in ReadOnlyCommands())
        {
            var output = new StringWriter();
            var originalOut = Console.Out;
            var originalError = Console.Error;
            int exitCode;
            try
            {
                Console.SetOut(output);
                Console.SetError(output);
                exitCode = await command(path);
            }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"`{name}` threw {ex.GetType().Name} on {description}. A command-line tool " +
                    $"must report a bad file, not crash on it. Message: {ex.Message.Split('\n')[0]}");
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            var text = output.ToString();

            exitCode.Should().NotBe(0,
                $"`{name}` must report failure on {description}; a shell script branches on the " +
                "exit code, and reporting success on an unreadable file is how bad data gets " +
                "through a pipeline unnoticed");

            text.Should().NotContain("   at PromptResponse.",
                $"`{name}` printed a stack trace for {description}; the person holding a broken " +
                "file needs to be told about their file, not about our internals");
            text.Should().NotContain("Unhandled exception",
                $"`{name}` reported an unhandled exception for {description}");
        }
    }

    private IEnumerable<(string Name, Func<string, Task<int>>)> ReadOnlyCommands()
    {
        var validate = new ValidateCommand(_serializer, new DocumentValidator(), new DataTypeValidator());
        var info = new InfoCommand(_serializer);
        var stats = new StatsCommand(_serializer);

        yield return ("validate", path => validate.ExecuteAsync([path]));
        yield return ("info", path => info.ExecuteAsync([path]));
        yield return ("stats", path => stats.ExecuteAsync([path]));
    }

    /// <summary>A file that is not there at all is the most common bad path of all.</summary>
    [Fact]
    public async Task ReadOnlyCommands_ReportMissingFiles_Cleanly()
    {
        var missing = Path.Combine(_dir, "does-not-exist.aprt");

        foreach (var (name, command) in ReadOnlyCommands())
        {
            var output = new StringWriter();
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(output);
                (await command(missing)).Should().NotBe(0, $"`{name}` must fail on a missing file");
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            output.ToString().Should().NotContain("   at PromptResponse.",
                $"`{name}` printed a stack trace for a missing file");
        }
    }
}
