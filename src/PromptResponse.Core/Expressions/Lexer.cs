using System.Globalization;
using System.Text;

namespace PromptResponse.Core.Expressions;

internal enum TokType
{
    Number, String, Identifier, True, False, Null,
    Plus, Minus, Star, Slash, Percent,
    Eq, Ne, Lt, Le, Gt, Ge,
    And, Or, Not,
    Question, Colon, Comma, Dot,
    LParen, RParen, LBracket, RBracket,
    End,
}

internal readonly record struct Token(TokType Type, string Text, CelValue Value);

/// <summary>
/// Tokenizes an expression string into the CEL subset's tokens. Pure; throws
/// <see cref="ExpressionException"/> on an illegal character or unterminated
/// string.
/// </summary>
internal sealed class Lexer
{
    private readonly string _src;
    private int _pos;

    public Lexer(string src) => _src = src;

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        Token t;
        do
        {
            t = Next();
            tokens.Add(t);
        }
        while (t.Type != TokType.End);
        return tokens;
    }

    private char Cur => _pos < _src.Length ? _src[_pos] : '\0';
    private char Peek => _pos + 1 < _src.Length ? _src[_pos + 1] : '\0';

    private Token Next()
    {
        while (_pos < _src.Length && char.IsWhiteSpace(Cur))
        {
            _pos++;
        }
        if (_pos >= _src.Length)
        {
            return new Token(TokType.End, string.Empty, CelValue.Null);
        }

        var c = Cur;
        if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek)))
        {
            return Number();
        }
        if (c == '\'' || c == '"')
        {
            return Str(c);
        }
        if (char.IsLetter(c) || c == '_')
        {
            return Ident();
        }
        return Operator();
    }

    private Token Number()
    {
        var start = _pos;
        while (char.IsDigit(Cur))
        {
            _pos++;
        }
        if (Cur == '.')
        {
            _pos++;
            while (char.IsDigit(Cur))
            {
                _pos++;
            }
        }
        var text = _src[start.._pos];
        var num = double.Parse(text, CultureInfo.InvariantCulture);
        return new Token(TokType.Number, text, CelValue.Of(num));
    }

    private Token Str(char quote)
    {
        _pos++; // opening quote
        var sb = new StringBuilder();
        while (_pos < _src.Length && Cur != quote)
        {
            if (Cur == '\\')
            {
                _pos++;
                sb.Append(Cur switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => Cur,
                });
                _pos++;
            }
            else
            {
                sb.Append(Cur);
                _pos++;
            }
        }
        if (_pos >= _src.Length)
        {
            throw new ExpressionException("Unterminated string literal.");
        }
        _pos++; // closing quote
        return new Token(TokType.String, sb.ToString(), CelValue.Of(sb.ToString()));
    }

    private Token Ident()
    {
        var start = _pos;
        while (char.IsLetterOrDigit(Cur) || Cur == '_')
        {
            _pos++;
        }
        var text = _src[start.._pos];
        return text switch
        {
            "true" => new Token(TokType.True, text, CelValue.Of(true)),
            "false" => new Token(TokType.False, text, CelValue.Of(false)),
            "null" => new Token(TokType.Null, text, CelValue.Null),
            _ => new Token(TokType.Identifier, text, CelValue.Null),
        };
    }

    private Token Operator()
    {
        var c = Cur;
        var n = Peek;
        switch (c)
        {
            case '+': _pos++; return Tok(TokType.Plus, "+");
            case '-': _pos++; return Tok(TokType.Minus, "-");
            case '*': _pos++; return Tok(TokType.Star, "*");
            case '/': _pos++; return Tok(TokType.Slash, "/");
            case '%': _pos++; return Tok(TokType.Percent, "%");
            case '?': _pos++; return Tok(TokType.Question, "?");
            case ':': _pos++; return Tok(TokType.Colon, ":");
            case ',': _pos++; return Tok(TokType.Comma, ",");
            case '.': _pos++; return Tok(TokType.Dot, ".");
            case '(': _pos++; return Tok(TokType.LParen, "(");
            case ')': _pos++; return Tok(TokType.RParen, ")");
            case '[': _pos++; return Tok(TokType.LBracket, "[");
            case ']': _pos++; return Tok(TokType.RBracket, "]");
            case '=' when n == '=': _pos += 2; return Tok(TokType.Eq, "==");
            case '!' when n == '=': _pos += 2; return Tok(TokType.Ne, "!=");
            case '<' when n == '=': _pos += 2; return Tok(TokType.Le, "<=");
            case '>' when n == '=': _pos += 2; return Tok(TokType.Ge, ">=");
            case '<': _pos++; return Tok(TokType.Lt, "<");
            case '>': _pos++; return Tok(TokType.Gt, ">");
            case '&' when n == '&': _pos += 2; return Tok(TokType.And, "&&");
            case '|' when n == '|': _pos += 2; return Tok(TokType.Or, "||");
            case '!': _pos++; return Tok(TokType.Not, "!");
            default:
                throw new ExpressionException($"Unexpected character '{c}' in expression.");
        }
    }

    private static Token Tok(TokType type, string text) => new(type, text, CelValue.Null);
}
