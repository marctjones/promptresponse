using System.Globalization;
using System.Text.RegularExpressions;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Tree-walking evaluator for the CEL subset. Operators are type-checked;
/// <c>matches()</c> runs with a timeout to defuse catastrophic-backtracking
/// regexes. No loops, no user functions, no side effects.
/// </summary>
internal sealed class Evaluator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly IExpressionContext _ctx;

    public Evaluator(IExpressionContext ctx) => _ctx = ctx;

    public CelValue Eval(Node node) => node switch
    {
        LiteralNode n => n.Value,
        IdentifierNode n => _ctx.Resolve(n.Name),
        MemberNode n => _ctx.Resolve(DottedPath(n)),
        ListNode n => CelValue.List(n.Items.Select(Eval).ToList()),
        UnaryNode n => EvalUnary(n),
        BinaryNode n => EvalBinary(n),
        TernaryNode n => Eval(n.Cond).IsTruthy ? Eval(n.IfTrue) : Eval(n.IfFalse),
        CallNode n => EvalCall(n),
        _ => throw new ExpressionException("Unknown expression node."),
    };

    private static string DottedPath(Node node) => node switch
    {
        IdentifierNode i => i.Name,
        MemberNode m => DottedPath(m.Target) + "." + m.Name,
        _ => throw new ExpressionException("Member access is only supported on names (e.g. ctx.a.b)."),
    };

    private CelValue EvalUnary(UnaryNode n)
    {
        var v = Eval(n.Operand);
        return n.Op switch
        {
            TokType.Not => CelValue.Of(!v.IsTruthy),
            TokType.Minus => CelValue.Of(-Num(v)),
            _ => throw new ExpressionException("Bad unary operator."),
        };
    }

    private CelValue EvalBinary(BinaryNode n)
    {
        // Short-circuit logicals.
        if (n.Op == TokType.And)
        {
            return CelValue.Of(Eval(n.Left).IsTruthy && Eval(n.Right).IsTruthy);
        }
        if (n.Op == TokType.Or)
        {
            return CelValue.Of(Eval(n.Left).IsTruthy || Eval(n.Right).IsTruthy);
        }

        var a = Eval(n.Left);
        var b = Eval(n.Right);

        switch (n.Op)
        {
            case TokType.Eq: return CelValue.Of(AreEqual(a, b));
            case TokType.Ne: return CelValue.Of(!AreEqual(a, b));
            case TokType.Lt: return CelValue.Of(Compare(a, b) < 0);
            case TokType.Le: return CelValue.Of(Compare(a, b) <= 0);
            case TokType.Gt: return CelValue.Of(Compare(a, b) > 0);
            case TokType.Ge: return CelValue.Of(Compare(a, b) >= 0);
            case TokType.Plus: return Add(a, b);
            case TokType.Minus: return CelValue.Of(Num(a) - Num(b));
            case TokType.Star: return CelValue.Of(Num(a) * Num(b));
            case TokType.Slash:
                if (Num(b) == 0) throw new ExpressionException("Division by zero.");
                return CelValue.Of(Num(a) / Num(b));
            case TokType.Percent:
                if (Num(b) == 0) throw new ExpressionException("Modulo by zero.");
                return CelValue.Of(Num(a) % Num(b));
            default: throw new ExpressionException("Bad binary operator.");
        }
    }

    private static CelValue Add(CelValue a, CelValue b)
    {
        if (a.Kind == CelKind.Number && b.Kind == CelKind.Number) return CelValue.Of(a.AsNumber() + b.AsNumber());
        if (a.Kind == CelKind.String && b.Kind == CelKind.String) return CelValue.Of(a.AsString() + b.AsString());
        if (a.Kind == CelKind.List && b.Kind == CelKind.List)
        {
            return CelValue.List(a.AsList().Concat(b.AsList()).ToList());
        }
        throw new ExpressionException($"Cannot add {a.Kind} and {b.Kind}.");
    }

    private static bool AreEqual(CelValue a, CelValue b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            CelKind.Null => true,
            CelKind.String => a.AsString() == b.AsString(),
            CelKind.Number => a.AsNumber() == b.AsNumber(),
            CelKind.Bool => a.AsBool() == b.AsBool(),
            CelKind.Timestamp => a.AsTimestamp() == b.AsTimestamp(),
            CelKind.List => a.AsList().Count == b.AsList().Count &&
                            a.AsList().Zip(b.AsList(), AreEqual).All(x => x),
            _ => false,
        };
    }

    private static int Compare(CelValue a, CelValue b)
    {
        if (a.Kind == CelKind.Number && b.Kind == CelKind.Number) return a.AsNumber().CompareTo(b.AsNumber());
        if (a.Kind == CelKind.String && b.Kind == CelKind.String) return string.CompareOrdinal(a.AsString(), b.AsString());
        if (a.Kind == CelKind.Timestamp && b.Kind == CelKind.Timestamp) return a.AsTimestamp().CompareTo(b.AsTimestamp());
        throw new ExpressionException($"Cannot compare {a.Kind} and {b.Kind}.");
    }

    private static double Num(CelValue v) => v.Kind == CelKind.Number
        ? v.AsNumber()
        : throw new ExpressionException($"Expected a number but got {v.Kind}.");

    private CelValue EvalCall(CallNode n)
    {
        CelValue Arg(int i) => Eval(n.Args[i]);
        void Arity(int count)
        {
            if (n.Args.Count != count)
            {
                throw new ExpressionException($"{n.Function}() expects {count} argument(s).");
            }
        }

        switch (n.Function)
        {
            case "int":
                Arity(1);
                return CelValue.Of(Math.Truncate(ToNumber(Arg(0), "int")));
            case "double":
                Arity(1);
                return CelValue.Of(ToNumber(Arg(0), "double"));
            case "string":
                Arity(1);
                return CelValue.Of(Arg(0).ToDisplayString());
            case "size":
                Arity(1);
                var s = Arg(0);
                return s.Kind switch
                {
                    CelKind.String => CelValue.Of((double)s.AsString().Length),
                    CelKind.List => CelValue.Of((double)s.AsList().Count),
                    _ => throw new ExpressionException("size() expects a string or list."),
                };
            case "matches":
                Arity(2);
                var input = Arg(0);
                var pattern = Arg(1);
                if (input.Kind != CelKind.String || pattern.Kind != CelKind.String)
                {
                    throw new ExpressionException("matches() expects (string, string).");
                }
                try
                {
                    return CelValue.Of(Regex.IsMatch(input.AsString(), pattern.AsString(), RegexOptions.None, RegexTimeout));
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new ExpressionException("matches() regex timed out.");
                }
                catch (ArgumentException ex)
                {
                    throw new ExpressionException("matches() invalid regex.", ex);
                }
            case "timestamp":
                Arity(1);
                return CelValue.Of(ParseTimestamp(Arg(0)));
            default:
                throw new ExpressionException($"Unknown function '{n.Function}'.");
        }
    }

    private static double ToNumber(CelValue v, string fn) => v.Kind switch
    {
        CelKind.Number => v.AsNumber(),
        CelKind.String when double.TryParse(v.AsString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
        _ => throw new ExpressionException($"{fn}() cannot convert '{v.ToDisplayString()}' to a number."),
    };

    private static DateTimeOffset ParseTimestamp(CelValue v)
    {
        if (v.Kind == CelKind.Timestamp) return v.AsTimestamp();
        if (v.Kind == CelKind.String &&
            DateTimeOffset.TryParse(v.AsString().Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts))
        {
            return ts;
        }
        throw new ExpressionException($"timestamp() cannot parse '{v.ToDisplayString()}'.");
    }
}
