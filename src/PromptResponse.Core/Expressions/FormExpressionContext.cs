using Celly;
using Celly.Checking;
using Celly.Types;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// The CEL evaluation environment for one document: what every field is declared to
/// be, and what it currently holds.
/// </summary>
/// <remarks>
/// <para>
/// Built once per document and reused across every expression in it. The type
/// environment comes from each prompt's <c>expectedDataType</c>, which is what lets an
/// author write <c>quantity * unit_price</c> instead of
/// <c>double(quantity) * double(unit_price)</c>, and what lets a type checker tell the
/// author their expression is wrong before a filler ever sees the form.
/// </para>
/// <para>
/// <c>_this</c> is declared with the type of the prompt being evaluated, so environments
/// are cached per <c>_this</c> type rather than rebuilt per prompt.
/// </para>
/// </remarks>
public sealed class FormExpressionContext
{
    private readonly IReadOnlyList<VariableDecl> _fieldDecls;
    private readonly Dictionary<string, object?> _bindings;
    private readonly Dictionary<string, CelType> _declaredTypes;
    private readonly Dictionary<string, CelEnv> _envByThisType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CelProgram?> _programs = new(StringComparer.Ordinal);

    private FormExpressionContext(
        IReadOnlyList<VariableDecl> fieldDecls,
        Dictionary<string, object?> bindings,
        Dictionary<string, CelType> declaredTypes)
    {
        _fieldDecls = fieldDecls;
        _bindings = bindings;
        _declaredTypes = declaredTypes;
    }

    /// <summary>Builds the environment for a document.</summary>
    /// <param name="document">The document whose prompts declare the field types.</param>
    /// <param name="today">Optional ISO date bound to <c>_today</c>.</param>
    /// <param name="ctx">Optional host-supplied string map bound to <c>ctx</c>.</param>
    public static FormExpressionContext Create(
        AprDocument document,
        string? today = null,
        IReadOnlyDictionary<string, string>? ctx = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var decls = new List<VariableDecl>();
        var bindings = new Dictionary<string, object?>(StringComparer.Ordinal);
        var types = new Dictionary<string, CelType>(StringComparer.Ordinal);

        foreach (var prompt in FormExpressions.GetAllPrompts(document))
        {
            if (string.IsNullOrEmpty(prompt.Id) || types.ContainsKey(prompt.Id))
            {
                continue;   // a duplicate id is a validation error; the first declaration wins here
            }

            var declared = CelBinding.TypeFor(prompt.Hints?.ExpectedDataType);
            types[prompt.Id] = declared;
            decls.Add(new VariableDecl(prompt.Id, declared));

            // A value that will not bind is left out entirely, so referencing it is a
            // CEL error rather than a silent default.
            var bound = CelBinding.Bind(prompt.Response, declared);
            if (bound is not null)
            {
                bindings[prompt.Id] = bound;
            }
        }

        // The activation the specification defines: _this, _id, _now, _today and
        // ctx. _now and _today are caller-supplied and never read from the host
        // clock, so evaluating the same form twice with the same inputs gives the
        // same result.
        decls.Add(new VariableDecl("_today", CelType.String));
        decls.Add(new VariableDecl("_now", CelType.Timestamp));
        if (!string.IsNullOrWhiteSpace(today))
        {
            var instant = CelBinding.Bind(today, CelType.Timestamp);
            if (instant is not null)
            {
                bindings["_now"] = instant;
            }
            // _today is the date as YYYY-MM-DD, a string rather than a timestamp.
            bindings["_today"] = today!.Length >= 10 ? today[..10] : today;
        }

        // ctx carries host-supplied strings. Declared map<string, string> so that
        // an expression over it type-checks the same way in every implementation.
        decls.Add(new VariableDecl("ctx", CelType.Map(CelType.String, CelType.String)));
        bindings["ctx"] = ctx?.ToDictionary(k => (object)k.Key, v => (object?)v.Value)
                          ?? new Dictionary<object, object?>();

        return new FormExpressionContext(decls, bindings, types);
    }

    /// <summary>The CEL type a field was declared with, for diagnostics and testing.</summary>
    public string DeclaredTypeOf(string promptId) =>
        _declaredTypes.TryGetValue(promptId, out var t) ? t.ToString() ?? "string" : "string";

    /// <summary>Whether a field's stored response bound successfully to its declared type.</summary>
    public bool IsBound(string promptId) => _bindings.ContainsKey(promptId);

    private CelEnv EnvFor(CelType thisType)
    {
        var key = thisType.ToString() ?? "string";
        if (_envByThisType.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var decls = new List<VariableDecl>(_fieldDecls)
        {
            new("_this", thisType),
            new("_id", CelType.String),
        };
        var env = CelEnv.Create(new CelEnvSettings { Declarations = decls });
        _envByThisType[key] = env;
        return env;
    }

    /// <summary>
    /// Compiles and evaluates an expression for a prompt, returning null when it cannot
    /// be evaluated at all: a parse failure, a type error, or an unbindable field.
    /// </summary>
    internal Celly.Values.CelValue? Evaluate(string expression, Prompt prompt)
    {
        var thisType = CelBinding.TypeFor(prompt.Hints?.ExpectedDataType);
        var cacheKey = (thisType.ToString() ?? "string") + " " + expression;

        if (!_programs.TryGetValue(cacheKey, out var program))
        {
            try
            {
                program = EnvFor(thisType).Compile(expression);
            }
            catch (Exception)
            {
                program = null;   // a broken expression is an authoring bug, never the filler's problem
            }
            _programs[cacheKey] = program;
        }
        if (program is null)
        {
            return null;
        }

        var bindings = new Dictionary<string, object?>(_bindings, StringComparer.Ordinal);
        var thisValue = CelBinding.Bind(prompt.Response, thisType);
        if (thisValue is not null)
        {
            bindings["_this"] = thisValue;
        }
        bindings["_id"] = prompt.Id;

        try
        {
            return program.Eval(bindings);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Type-checks an expression against this document, for authoring-time feedback.
    /// Returns the diagnostics, empty when the expression is sound.
    /// </summary>
    /// <remarks>
    /// This is where strictness belongs (specification section 7.3): the author is told
    /// their expression will not work, before publication. A filler is never blocked by
    /// it, because at fill time the same expression degrades to the stored response.
    /// </remarks>
    public IReadOnlyList<string> Check(string expression, string? expectedDataTypeOfThis = null)
    {
        try
        {
            var env = EnvFor(CelBinding.TypeFor(expectedDataTypeOfThis));
            var parsed = env.Parse(expression);
            if (parsed.Ast is null)
            {
                return [$"parse error in: {expression}"];
            }
            var checkResult = env.Check(parsed.Ast);
            return checkResult.HasErrors
                ? checkResult.Issues.Select(i => i.ToString() ?? "error").ToList()
                : [];
        }
        catch (Exception ex)
        {
            return [ex.Message];
        }
    }
}
