namespace JaskLang;

public class Lexer
{
    private readonly string _source;
    private readonly List<Token> _tokens = [];
    private int _start = 0;
    private int _current = 0;
    private int _line = 1;

    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["store"]    = TokenType.Store,
        ["in"]       = TokenType.In,
        ["if"]       = TokenType.If,
        ["else"]     = TokenType.Else,
        ["endif"]    = TokenType.EndIf,
        ["while"]    = TokenType.While,
        ["endwhile"] = TokenType.EndWhile,
        ["for"]      = TokenType.For,
        ["from"]     = TokenType.From,
        ["to"]       = TokenType.To,
        ["with"]     = TokenType.With,
        ["endfor"]   = TokenType.EndFor,
        ["true"]     = TokenType.True,
        ["false"]    = TokenType.False,
        ["function"] = TokenType.Function,
        ["end"]      = TokenType.End,
        ["return"]   = TokenType.Return,
    };

    public Lexer(string source)
    {
        _source = source;
    }

    public List<Token> ScanTokens()
    {
        while (!IsAtEnd())
        {
            _start = _current;
            ScanToken();
        }

        _tokens.Add(new Token(TokenType.Eof, "", null, _line));
        return _tokens;
    }

    private void ScanToken()
    {
        char c = Advance();
        switch (c)
        {
            case '(': AddToken(TokenType.LParen); break;
            case ')': AddToken(TokenType.RParen); break;
            case ':': AddToken(TokenType.Colon); break;
            case ',': AddToken(TokenType.Comma); break;
            case '+': AddToken(TokenType.Plus); break;
            case '-': AddToken(TokenType.Minus); break;
            case '*': AddToken(TokenType.Star); break;
            case '/':
                if (Match('/'))
                {
                    // ignore comments until the end of the line
                    while (Peek() != '\n' && !IsAtEnd()) Advance();
                }
                else
                {
                    AddToken(TokenType.Slash);
                }
                break;
            case '=':
                if (Match('=')) AddToken(TokenType.EqualEqual);
                else throw new LangException($"[Row {_line}] Unknown character '='. Did you mean '=='?");
                break;
            case '!':
                if (Match('=')) AddToken(TokenType.BangEqual);
                else throw new LangException($"[Row {_line}] Unexpected character '!'.");
                break;
            case '<':
                AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less);
                break;
            case '>':
                AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater);
                break;
            case ' ':
            case '\r':
            case '\t':
                break;
            case '\n':
                _line++;
                break;
            case '"':
                ScanString();
                break;
            default:
                if (char.IsDigit(c))
                {
                    ScanNumber();
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    ScanIdentifier();
                }
                else
                {
                    throw new LangException($"[Row {_line}] Unknown character '{c}'.");
                }
                break;
        }
    }

    private void ScanString()
    {
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\n') _line++;
            Advance();
        }

        if (IsAtEnd())
            throw new LangException($"[Row {_line}] Unclosed string.");

        // closing "
        Advance();

        string value = _source.Substring(_start + 1, _current - _start - 2);
        AddToken(TokenType.String, value);
    }

    private void ScanNumber()
    {
        while (char.IsDigit(Peek())) Advance();

        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            Advance();
            while (char.IsDigit(Peek())) Advance();
        }

        string text = _source.Substring(_start, _current - _start);
        AddToken(TokenType.Number, double.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
    }

    private void ScanIdentifier()
    {
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Advance();

        string text = _source.Substring(_start, _current - _start);
        TokenType type = Keywords.TryGetValue(text, out var kw) ? kw : TokenType.Identifier;
        AddToken(type);
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_current] != expected) return false;
        _current++;
        return true;
    }

    private char Peek() => IsAtEnd() ? '\0' : _source[_current];
    private char PeekNext() => _current + 1 >= _source.Length ? '\0' : _source[_current + 1];
    private char Advance() => _source[_current++];
    private bool IsAtEnd() => _current >= _source.Length;

    private void AddToken(TokenType type, object? literal = null)
    {
        string text = _source.Substring(_start, _current - _start);
        _tokens.Add(new Token(type, text, literal, _line));
    }
}
