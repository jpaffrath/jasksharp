namespace JaskLang;

public class LangException : Exception
{
    public int Line { get; }
    public string? FilePath { get; }

    public LangException(string message)
        : base(message)
    {
        Line = 0;
    }

    public LangException(string message, int line, string? filePath = null)
        : base(FormatMessage(message, line, filePath))
    {
        Line = line;
        FilePath = filePath;
    }

    public LangException(string message, Token token, string? filePath = null)
        : base(FormatMessage(message, token.Line, filePath))
    {
        Line = token.Line;
        FilePath = filePath;
    }

    private static string FormatMessage(string message, int line, string? filePath)
    {
        string location = line > 0 ? $"[Row {line}]" : "";

        if (string.IsNullOrWhiteSpace(filePath) == false)
        {
            string normalizedPath = filePath.Replace("\\", "/");
            return string.IsNullOrWhiteSpace(location)
                ? $"[{normalizedPath}] {message}"
                : $"{location} [{normalizedPath}] {message}";
        }

        return string.IsNullOrWhiteSpace(location) ? message : $"{location} {message}";
    }
}
