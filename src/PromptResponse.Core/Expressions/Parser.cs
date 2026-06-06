namespace PromptResponse.Core.Expressions;

internal abstract record Node;
internal sealed record LiteralNode(CelValue Value) : Node;
internal sealed record IdentifierNode(string Name) : Node;
internal sealed record ListNode(IReadOnlyList<Node> Items) : Node;
internal sealed record MemberNode(Node Target, string Name) : Node;
internal sealed record CallNode(string Function, IReadOnlyList<Node> Args) : Node;
internal sealed record UnaryNode(TokType Op, Node Operand) : Node;
internal sealed record BinaryNode(TokType Op, Node Left, Node Right) : Node;
internal sealed record TernaryNode(Node Cond, Node IfTrue, Node IfFalse) : Node;

/// <summary>
/// Recursive-descent parser for the CEL subset. Precedence (low → high):
/// ternary, <c>||</c>, <c>&amp;&amp;</c>, equality, comparison, additive,
/// multiplicative, unary, primary. Depth-limited to bound the work an
/// adversarial expression can cause.
/// </summary>
internal sealed class Parser
{
    private const int MaxDepth = 64;

    private readonly List<Token> _tokens;
    private int _pos;
    private int _depth;

    public Parser(List<Token> tokens) => _tokens = tokens;

    public Node ParseProgram()
    {
        var node = ParseTernary();
        if (Cur.Type != TokType.End)
        {
            throw new ExpressionException($"Unexpected token '{Cur.Text}' after expression.");
        }
        return node;
    }

    private Token Cur => _tokens[_pos];

    private Token Advance() => _tokens[_pos++];

    private bool Match(TokType type)
    {
        if (Cur.Type == type)
        {
            _pos++;
            return true;
        }
        return false;
    }

    private void Expect(TokType type, string what)
    {
        if (!Match(type))
        {
            throw new ExpressionException($"Expected {what} but found '{Cur.Text}'.");
        }
    }

    private IDisposable Deepen()
    {
        if (++_depth > MaxDepth)
        {
            throw new ExpressionException("Expression nesting too deep.");
        }
        return new DepthScope(this);
    }

    private sealed class DepthScope(Parser p) : IDisposable
    {
        public void Dispose() => p._depth--;
    }

    private Node ParseTernary()
    {
        using var _ = Deepen();
        var cond = ParseOr();
        if (Match(TokType.Question))
        {
            var ifTrue = ParseTernary();
            Expect(TokType.Colon, "':'");
            var ifFalse = ParseTernary();
            return new TernaryNode(cond, ifTrue, ifFalse);
        }
        return cond;
    }

    private Node ParseOr()
    {
        var left = ParseAnd();
        while (Cur.Type == TokType.Or)
        {
            Advance();
            left = new BinaryNode(TokType.Or, left, ParseAnd());
        }
        return left;
    }

    private Node ParseAnd()
    {
        var left = ParseEquality();
        while (Cur.Type == TokType.And)
        {
            Advance();
            left = new BinaryNode(TokType.And, left, ParseEquality());
        }
        return left;
    }

    private Node ParseEquality()
    {
        var left = ParseComparison();
        while (Cur.Type is TokType.Eq or TokType.Ne)
        {
            var op = Advance().Type;
            left = new BinaryNode(op, left, ParseComparison());
        }
        return left;
    }

    private Node ParseComparison()
    {
        var left = ParseAdditive();
        while (Cur.Type is TokType.Lt or TokType.Le or TokType.Gt or TokType.Ge)
        {
            var op = Advance().Type;
            left = new BinaryNode(op, left, ParseAdditive());
        }
        return left;
    }

    private Node ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Cur.Type is TokType.Plus or TokType.Minus)
        {
            var op = Advance().Type;
            left = new BinaryNode(op, left, ParseMultiplicative());
        }
        return left;
    }

    private Node ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Cur.Type is TokType.Star or TokType.Slash or TokType.Percent)
        {
            var op = Advance().Type;
            left = new BinaryNode(op, left, ParseUnary());
        }
        return left;
    }

    private Node ParseUnary()
    {
        if (Cur.Type is TokType.Not or TokType.Minus)
        {
            var op = Advance().Type;
            return new UnaryNode(op, ParseUnary());
        }
        return ParsePostfix();
    }

    private Node ParsePostfix()
    {
        var node = ParsePrimary();
        while (Match(TokType.Dot))
        {
            if (Cur.Type != TokType.Identifier)
            {
                throw new ExpressionException("Expected a name after '.'.");
            }
            node = new MemberNode(node, Advance().Text);
        }
        return node;
    }

    private Node ParsePrimary()
    {
        using var _ = Deepen();
        var t = Cur;
        switch (t.Type)
        {
            case TokType.Number:
            case TokType.String:
            case TokType.True:
            case TokType.False:
            case TokType.Null:
                Advance();
                return new LiteralNode(t.Value);

            case TokType.Identifier:
                Advance();
                if (Match(TokType.LParen))
                {
                    return new CallNode(t.Text, ParseArguments());
                }
                return new IdentifierNode(t.Text);

            case TokType.LParen:
                Advance();
                var inner = ParseTernary();
                Expect(TokType.RParen, "')'");
                return inner;

            case TokType.LBracket:
                Advance();
                return new ListNode(ParseList());

            default:
                throw new ExpressionException($"Unexpected token '{t.Text}'.");
        }
    }

    private List<Node> ParseArguments()
    {
        var args = new List<Node>();
        if (Cur.Type != TokType.RParen)
        {
            do
            {
                args.Add(ParseTernary());
            }
            while (Match(TokType.Comma));
        }
        Expect(TokType.RParen, "')'");
        return args;
    }

    private List<Node> ParseList()
    {
        var items = new List<Node>();
        if (Cur.Type != TokType.RBracket)
        {
            do
            {
                items.Add(ParseTernary());
            }
            while (Match(TokType.Comma));
        }
        Expect(TokType.RBracket, "']'");
        return items;
    }
}
