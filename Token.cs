namespace JaskLang;

public enum TokenType
{
    // literals
    Number,
    Identifier,
    String,

    // keywords
    Store,
    In,
    If,
    Else,
    EndIf,
    While,
    EndWhile,
    For,
    From,
    To,
    With,
    EndFor,
    True,
    False,
    Function,
    End,
    Return,

    // symbols
    Plus,
    Minus,
    Star,
    Slash,
    LParen,
    RParen,
    Colon,
    Comma,

    // comparison operators
    EqualEqual,
    BangEqual,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,

    Eof
}

public class Token(TokenType type, string lexeme, object? literal, int line)
{
    public TokenType Type { get; } = type;
    public string Lexeme { get; } = lexeme;
    public object? Literal { get; } = literal;
    public int Line { get; } = line;

    public override string ToString() => $"{Type} '{Lexeme}'";
}
