namespace JaskLang;

public partial class Interpreter
{
    private static string FunctionKey(string name, IEnumerable<string> paramTypes)
        => $"{name}({string.Join(",", paramTypes)})";

    private static string FunctionKey(string name, List<(Token Name, Token Type)> parameters)
        => FunctionKey(name, parameters.Select(p => p.Type.Lexeme));
    
    private static bool IsValueOfType(object? value, string typeName)
    {
        return typeName.ToLower() switch
        {
            "number" => value is double,
            "string" => value is string,
            "boolean" => value is bool,
            "list" => value is List<object?>,
            "any" => value is object || value == null,
            _ => value is StructInstance si && si.TypeName == typeName
        };
    }

    private static string GetValueType(object? value)
    {
        return value switch
        {
            double => "number",
            string => "string",
            bool => "boolean",
            List<object?> => "list",
            StructInstance si => si.TypeName,
            null => "nil",
            _ => value.GetType().Name
        };
    }

    private static double CheckNumber(Token op, object? value)
    {
        if (value is double d)
        {
            return d;
        }

        throw new LangException($"Operator '{op.Lexeme}' expects a number, but received '{Stringify(value)}'", op);
    }

    private static double CheckNumberStmt(Token context, object? value, string label)
    {
        if (value is double d)
        {
            return d;
        }

        throw new LangException($"{label} must be a number, but is a '{Stringify(value)}'", context);
    }

    public static string Stringify(object? value)
    {
        if (value is null) return "nil";
        if (value is bool b) return b ? "true" : "false";
        if (value is StructInstance si) return si.ToString();

        if (value is double d)
        {
            // trim integers (4 instead of 4.0)
            if (d >= long.MinValue && d <= long.MaxValue && d == Math.Floor(d))
            {
                return ((long)d).ToString();
            }

            return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value is List<object?> list)
        {
            var elements = list.Select(Stringify).ToList();
            return "[" + string.Join(", ", elements) + "]";
        }

        return value.ToString() ?? "nil";
    }
}