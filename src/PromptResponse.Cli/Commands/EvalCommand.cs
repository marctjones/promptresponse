using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Evaluates a document's expression hints and reports what each one produced.
/// </summary>
/// <remarks>
/// <para>
/// Exists so other tools do not have to reimplement CEL. This repository already
/// learned that lesson once: the shipped engine was CEL-<i>flavoured</i> rather than
/// CEL, and the specification had to be corrected. A second engine written for a demo
/// would make the same mistake in a new language, and the demo's answers would slowly
/// stop matching the ones that count.
/// </para>
/// <para>
/// So there is one CEL here — cel-spec conformant, through Celly — and anything that
/// wants expression results asks for them. The Python SDK stays core-only, which is what
/// lets it test the core-only profile rules at all.
/// </para>
/// <para>
/// Reports; never rejects. A failing rule is the author's own message about a response,
/// and no error may arise from the content of a response (specification 6.1). The exit
/// code says whether the document could be read, not whether its rules are satisfied.
/// </para>
/// </remarks>
public class EvalCommand
{
    private readonly IAprSerializer _serializer;

    public EvalCommand(IAprSerializer serializer) => _serializer = serializer;

    public async Task<int> ExecuteAsync(string[] args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (path is null)
        {
            Console.Error.WriteLine("Usage: apr eval <file> [--json]");
            return 1;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        Core.Models.AprDocument document;
        try
        {
            document = _serializer.Deserialize(await File.ReadAllTextAsync(path));
        }
        catch (SerializationException ex)
        {
            Console.Error.WriteLine($"Could not read {path}: {ex.Message}");
            return 1;
        }

        var context = FormExpressions.BuildContext(document);
        var results = new List<ExpressionResult>();

        foreach (var prompt in FormExpressions.GetAllPrompts(document))
        {
            var hints = prompt.Hints;
            if (hints is null) continue;

            var any = new[] { hints.ExprValue, hints.ExprValidation, hints.ExprHidden,
                              hints.ExprExpected, hints.ExprReadOnly }
                .Any(e => !string.IsNullOrWhiteSpace(e));
            if (!any) continue;

            results.Add(new ExpressionResult(
                prompt.Id,
                prompt.Label,
                prompt.Response,
                hints.ExprValue,
                hints.ExprValue is null ? null : FormExpressions.ComputeValue(prompt, context),
                hints.ExprValidation,
                FormExpressions.Validate(prompt, context),
                hints.ExprHidden,
                hints.ExprHidden is null ? null : FormExpressions.IsHidden(prompt, context),
                hints.ExprExpected,
                hints.ExprExpected is null ? null : FormExpressions.IsExpected(prompt, context),
                hints.ExprReadOnly,
                hints.ExprReadOnly is null ? null : FormExpressions.IsReadOnly(prompt, context)));
        }

        if (args.Contains("--json") || args.Contains("-j"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { file = path, expressions = results },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                }));
            return 0;
        }

        if (results.Count == 0)
        {
            Console.WriteLine("This document has no expressions.");
            return 0;
        }

        Console.WriteLine($"Expressions in {Path.GetFileName(path)}");
        Console.WriteLine();
        foreach (var r in results)
        {
            Console.WriteLine($"  {r.Label}  ({r.PromptId})");
            Console.WriteLine($"    answer: \"{r.Response}\"");
            if (r.ValueExpression is not null)
            {
                Console.WriteLine($"    exprValue      {r.ValueExpression}");
                Console.WriteLine($"      -> {Describe(r.ComputedValue)}");
            }
            if (r.ValidationExpression is not null)
            {
                Console.WriteLine($"    exprValidation {r.ValidationExpression}");
                Console.WriteLine($"      -> {(r.ValidationMessage is null ? "satisfied" : $"says: {r.ValidationMessage}")}");
            }
            foreach (var (name, expression, value) in new (string, string?, bool?)[]
            {
                ("exprHidden", r.HiddenExpression, r.IsHidden),
                ("exprExpected", r.ExpectedExpression, r.IsExpected),
                ("exprReadOnly", r.ReadOnlyExpression, r.IsReadOnly),
            })
            {
                if (expression is null) continue;
                Console.WriteLine($"    {name,-14} {expression}");
                Console.WriteLine($"      -> {(value?.ToString().ToLowerInvariant() ?? "could not evaluate")}");
            }
            Console.WriteLine();
        }

        var failing = results.Count(r => r.ValidationMessage is not null);
        Console.WriteLine(failing == 0
            ? "Every rule the form states about itself is satisfied."
            : $"{failing} rule(s) the form states about itself are not satisfied. That is the " +
              "author's message about an answer, not a reason to reject the document.");
        return 0;
    }

    private static string Describe(string? computed) =>
        computed is null ? "could not evaluate (a field it needs may be blank or unparseable)"
                         : $"\"{computed}\"";

    /// <summary>One prompt's expressions and what they produced.</summary>
    private sealed record ExpressionResult(
        string PromptId, string Label, string Response,
        string? ValueExpression, string? ComputedValue,
        string? ValidationExpression, string? ValidationMessage,
        string? HiddenExpression, bool? IsHidden,
        string? ExpectedExpression, bool? IsExpected,
        string? ReadOnlyExpression, bool? IsReadOnly);
}
