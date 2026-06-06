using AwesomeAssertions;
using PromptResponse.Core.Expressions;
using Xunit;

namespace PromptResponse.Core.Tests.Expressions;

/// <summary>
/// Verifies the safe CEL-subset expression evaluator: literals, operators,
/// precedence, field references, built-in functions, graceful handling of
/// missing variables, and the safety limits.
/// </summary>
public class ExpressionEvaluatorTests
{
    private static IExpressionContext Ctx(
        Dictionary<string, string>? fields = null,
        string? thisValue = null,
        string? today = null,
        Dictionary<string, string>? ctx = null) =>
        new DictionaryExpressionContext(fields ?? new(), thisValue, today, ctx);

    private static CelValue Eval(string expr, IExpressionContext? ctx = null) =>
        ExpressionEvaluator.Evaluate(expr, ctx ?? Ctx());

    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("2 * 3 + 4", 10)]       // precedence
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("10 % 3", 1)]
    [InlineData("-5 + 8", 3)]
    public void Arithmetic(string expr, double expected) =>
        Eval(expr).AsNumber().Should().Be(expected);

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 <= 2", true)]
    [InlineData("3 > 5", false)]
    [InlineData("'a' < 'b'", true)]
    [InlineData("'Employed' == 'Employed'", true)]
    [InlineData("'a' != 'b'", true)]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("!false", true)]
    [InlineData("1 == 1 ? true : false", true)]
    public void BooleanLogic(string expr, bool expected) =>
        Eval(expr).AsBool().Should().Be(expected);

    [Fact]
    public void FieldReference_ResolvesResponseString()
    {
        var ctx = Ctx(new() { ["emp_status"] = "Employed" });
        Eval("emp_status == 'Employed'", ctx).AsBool().Should().BeTrue();
        Eval("emp_status", ctx).AsString().Should().Be("Employed");
    }

    [Fact]
    public void MissingField_ResolvesToNull_AndComparesAsNotEqual()
    {
        // "missing" field → Null; Null == 'x' is false (graceful, no throw).
        Eval("nope == 'x'", Ctx()).AsBool().Should().BeFalse();
        Eval("nope", Ctx()).IsNull.Should().BeTrue();
    }

    [Theory]
    [InlineData("int('18') >= 18", true)]
    [InlineData("int('17') >= 18", false)]
    [InlineData("double('50000.5') > 50000.0", true)]
    [InlineData("size('hello') == 5", true)]
    [InlineData("size('') > 0", false)]
    [InlineData("matches('abc-123', '^[a-z]+-[0-9]+$')", true)]
    [InlineData("matches('ABC', '^[a-z]+$')", false)]
    [InlineData("string(42) == '42'", true)]
    public void BuiltinFunctions(string expr, bool expected) =>
        Eval(expr).AsBool().Should().Be(expected);

    [Fact]
    public void Timestamp_Comparison()
    {
        var ctx = Ctx(new() { ["start"] = "2020-01-15", ["end"] = "2021-06-01" });
        Eval("timestamp(end) > timestamp(start)", ctx).AsBool().Should().BeTrue();
    }

    [Fact]
    public void Today_BuiltinAndCtx()
    {
        var ctx = Ctx(
            fields: new() { ["d"] = "2000-01-01" },
            today: "2026-06-06",
            ctx: new() { ["user.role"] = "admin" });

        Eval("timestamp(d) < timestamp(_today)", ctx).AsBool().Should().BeTrue();
        Eval("ctx.user.role == 'admin'", ctx).AsBool().Should().BeTrue();
        Eval("ctx.user.missing == ''", ctx).AsBool().Should().BeFalse(); // missing ctx → Null, not ""
    }

    [Fact]
    public void SpecExample_ConditionalVisibility()
    {
        var expr = "emp_status == 'Unemployed' || emp_status == 'Retired' || emp_status == 'Student'";
        ExpressionEvaluator.EvaluateBool(expr, Ctx(new() { ["emp_status"] = "Student" })).Should().BeTrue();
        ExpressionEvaluator.EvaluateBool(expr, Ctx(new() { ["emp_status"] = "Employed" })).Should().BeFalse();
    }

    [Fact]
    public void SpecExample_CrossFieldValidation()
    {
        // empty when valid; message when end <= start
        var expr = "_this == '' || start == '' ? '' : (timestamp(_this) > timestamp(start) ? '' : 'End date must be after start date')";

        var ok = ExpressionEvaluator.Compile(expr)
            .EvaluateString(Ctx(new() { ["start"] = "2020-01-01" }, thisValue: "2021-01-01"));
        ok.Should().BeEmpty();

        var bad = ExpressionEvaluator.Compile(expr)
            .EvaluateString(Ctx(new() { ["start"] = "2021-01-01" }, thisValue: "2020-01-01"));
        bad.Should().Be("End date must be after start date");
    }

    [Fact]
    public void ListLiteral_AndSize()
    {
        Eval("size(['a', 'b', 'c'])").AsNumber().Should().Be(3);
    }

    [Fact]
    public void StringConcatenation()
    {
        Eval("'Hello, ' + 'World'").AsString().Should().Be("Hello, World");
    }

    [Theory]
    [InlineData("1 +")]              // incomplete
    [InlineData("(1 + 2")]           // unbalanced paren
    [InlineData("'unterminated")]    // bad string
    [InlineData("1 @ 2")]            // illegal char
    [InlineData("foo(")]             // bad call
    public void SyntaxErrors_Throw(string expr)
    {
        var act = () => ExpressionEvaluator.Compile(expr);
        act.Should().Throw<ExpressionException>();
    }

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("5 % 0")]
    [InlineData("'a' - 'b'")]        // type error
    [InlineData("unknownFn(1)")]     // unknown function
    [InlineData("int('not a number')")]
    public void EvalErrors_Throw(string expr)
    {
        var act = () => Eval(expr);
        act.Should().Throw<ExpressionException>();
    }

    [Fact]
    public void EvaluateBool_OnError_FallsBackGracefully()
    {
        // A broken hint must never block the form — falls back, doesn't throw.
        ExpressionEvaluator.EvaluateBool("1 / 0", Ctx(), fallback: false).Should().BeFalse();
        ExpressionEvaluator.EvaluateBool("not valid syntax )(", Ctx(), fallback: true).Should().BeTrue();
    }

    [Fact]
    public void OverlongExpression_IsRejected()
    {
        var huge = "1" + string.Concat(Enumerable.Repeat(" + 1", 2000));
        var act = () => ExpressionEvaluator.Compile(huge);
        act.Should().Throw<ExpressionException>();
    }

    [Fact]
    public void DeeplyNestedExpression_IsRejected()
    {
        var deep = string.Concat(Enumerable.Repeat("(", 200)) + "1" + string.Concat(Enumerable.Repeat(")", 200));
        var act = () => ExpressionEvaluator.Compile(deep);
        act.Should().Throw<ExpressionException>();
    }

    [Fact]
    public void ShortCircuit_DoesNotEvaluateRightOnFalseAnd()
    {
        // If && evaluated the right side, 1/0 would throw; short-circuit avoids it.
        Eval("false && (1 / 0 == 0)").AsBool().Should().BeFalse();
        Eval("true || (1 / 0 == 0)").AsBool().Should().BeTrue();
    }

    [Fact]
    public void CompiledExpression_IsReusableAcrossContexts()
    {
        var compiled = ExpressionEvaluator.Compile("age == '' ? false : int(age) >= 18");
        compiled.EvaluateBool(Ctx(new() { ["age"] = "20" })).Should().BeTrue();
        compiled.EvaluateBool(Ctx(new() { ["age"] = "15" })).Should().BeFalse();
        compiled.EvaluateBool(Ctx(new() { ["age"] = "" })).Should().BeFalse();
    }
}
