namespace JaskLang;

public class LangException : Exception
{
    public int Line { get; }

    public LangException(string message)
        : base(message)
    {
        Line = 0;
    }

    public LangException(string message, int line)
        : base($"[Row {line}] {message}")
    {
        Line = line;
    }

    public LangException(string message, Token token)
        : base($"[Row {token.Line}] {message}")
    {
        Line = token.Line;
    }
}
