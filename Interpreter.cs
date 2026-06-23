namespace JaskLang;

public class ReturnException : Exception
{
    public object? Value { get; }
    public ReturnException(object? value) : base() { Value = value; }
}

public class Interpreter
{
    // dictionary for functions: name -> (parameters, body)
    private readonly Dictionary<string, (List<(Token Name, Token Type)> Params, List<Statement> Body)> _functions = [];

    // stack for environments to manage scopes
    private readonly Stack<Dictionary<string, object?>> _scopes = new();
    
    // global environment
    private Dictionary<string, object?> _globalEnvironment = [];

    public Interpreter()
    {
        _scopes.Push(_globalEnvironment);
    }

    public void Interpret(List<Statement> statements)
    {
        foreach (var statement in statements)
        {
            Execute(statement);
        }
    }

    private void Execute(Statement statement)
    {
        switch (statement)
        {
            case Statement.Store s:
                CurrentEnvironment[s.Name.Lexeme] = Evaluate(s.Value);
                break;

            case Statement.If i:
                if (IsTruthy(Evaluate(i.Condition)))
                {
                    foreach (var s in i.ThenBranch) Execute(s);
                }
                else if (i.ElseBranch != null)
                {
                    foreach (var s in i.ElseBranch) Execute(s);
                }
                break;

            case Statement.While w:
                while (IsTruthy(Evaluate(w.Condition)))
                {
                    foreach (var s in w.Body) Execute(s);
                }
                break;

            case Statement.For f:
                double from = CheckNumberStmt(f.Variable, Evaluate(f.Start), "value for 'from' in loop");
                double to   = CheckNumberStmt(f.Variable, Evaluate(f.End),   "value for 'to' in loop");
                
                if (from <= to)
                {
                    // ascending loop
                    for (double i = from; i <= to; )
                    {
                        CurrentEnvironment[f.Variable.Lexeme] = i;
                        foreach (var s in f.Body) Execute(s);
                        
                        if (f.Increment != null)
                        {
                            // evaluate custom increment expression with current loop variable
                            object? result = Evaluate(f.Increment);
                            i = CheckNumberStmt(f.Variable, result, "value for 'with' in loop");
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
                else
                {
                    // descending loop
                    for (double i = from; i >= to; )
                    {
                        CurrentEnvironment[f.Variable.Lexeme] = i;
                        foreach (var s in f.Body) Execute(s);
                        
                        if (f.Increment != null)
                        {
                            // evaluate custom increment expression with current loop variable
                            object? result = Evaluate(f.Increment);
                            i = CheckNumberStmt(f.Variable, result, "value for 'with' in loop");
                        }
                        else
                        {
                            i--;
                        }
                    }
                }
                break;

            case Statement.ForIn fi:
                object? collectionObj = Evaluate(fi.Collection);
                if (collectionObj is not List<object?> list)
                {
                    throw new LangException($"'for...in' loop expects a list, but got '{GetValueType(collectionObj)}'", fi.Variable);
                }

                foreach (var item in list)
                {
                    CurrentEnvironment[fi.Variable.Lexeme] = item;
                    foreach (var s in fi.Body) Execute(s);
                }
                break;

            case Statement.Function f:
                _functions[f.Name.Lexeme] = (f.Params, f.Body);
                break;

            case Statement.Expression e:
                Evaluate(e.Value);
                break;

            case Statement.Return r:
                object? returnValue = r.Value != null ? Evaluate(r.Value) : null;
                throw new ReturnException(returnValue);

            default:
                throw new LangException($"Unknown statement: {statement}");
        }
    }

    private Dictionary<string, object?> CurrentEnvironment => _scopes.Peek();

    private object? Evaluate(Expression expression)
    {
        return expression switch
        {
            Expression.Literal  l => l.Value,
            Expression.Grouping g => Evaluate(g.Inner),
            Expression.Variable v => LookupVariable(v.Name),
            Expression.Unary    u => EvaluateUnary(u),
            Expression.Binary   b => EvaluateBinary(b),
            Expression.Call     c => EvaluateCall(c),
            _ => throw new LangException($"Unknown expression: {expression}")
        };
    }

    private object? EvaluateCall(Expression.Call call)
    {
        Expression.Variable? funcExpr = call.Callee as Expression.Variable;
        if (funcExpr == null)
        {
            throw new LangException("Can only call functions by name");
        }

        string funcName = funcExpr.Name.Lexeme;

        switch (funcName)
        {
            case "print":
                return CallInternalFunctionPrint(call);
            case "type":
                return CallInternalFunctionType(call);
            case "list":
                return CallInternalFunctionList(call);
            case "listAdd":
                return CallInternalFunctionListAdd(call);
            case "listGet":
                return CallInternalFunctionListGet(call);
            case "clock":
                return CallInternalFunctionClock(call);
            case "readInput":
                return CallInternalFunctionReadInput(call);
            case "exit":
                return CallInternalFunctionExit(call);
            default:
                break;
        }

        if (_functions.TryGetValue(funcName, out var funcDef) == false)
        {
            throw new LangException($"Unknown function '{funcName}'", funcExpr.Name);
        }

        var (parameters, body) = funcDef;

        if (call.Arguments.Count != parameters.Count)
        {
            throw new LangException($"Function '{funcName}' expects {parameters.Count} arguments, but got {call.Arguments.Count}", funcExpr.Name);
        }

        // create a new environment for the function call
        var functionEnv = new Dictionary<string, object?>();
        
        //  bind parameters to arguments with type checking
        for (int i = 0; i < parameters.Count; i++)
        {
            object? argValue = Evaluate(call.Arguments[i]);
            string paramType = parameters[i].Type.Lexeme;
            
            // check if the argument value matches the expected type
            if (IsValueOfType(argValue, paramType) == false)
            {
                string expectedType = paramType;
                string actualType = GetValueType(argValue);
                throw new LangException($"Function '{funcName}': Parameter '{parameters[i].Name.Lexeme}' expects type '{expectedType}', but got '{actualType}'", funcExpr.Name);
            }

            functionEnv[parameters[i].Name.Lexeme] = argValue;
        }

        // call function body in the new environment
        _scopes.Push(functionEnv);
        try
        {
            foreach (var stmt in body)
            {
                Execute(stmt);
            }
        }
        catch (ReturnException ex)
        {
            return ex.Value;
        }
        finally
        {
            _scopes.Pop();
        }

        // function returns nil
        return null;
    }

    // Internal functions

    private object? CallInternalFunctionPrint(Expression.Call call)
    {
        if (call.Arguments.Count < 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'print' expects at least 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        // print all arguments
        var parts = new List<string>();
        foreach (var arg in call.Arguments)
        {
            parts.Add(Stringify(Evaluate(arg)));
        }

        Console.Write(string.Join("", parts));

        return null;
    }

    private object? CallInternalFunctionType(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'type' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }
        
        object? value = Evaluate(call.Arguments[0]);

        return GetValueType(value);
    }

    private object? CallInternalFunctionList(Expression.Call call)
    {
        var list = new List<object?>();

        foreach (var arg in call.Arguments)
        {
            list.Add(Evaluate(arg));
        }

        return list;
    }

    private object? CallInternalFunctionListAdd(Expression.Call call)
    {
        if (call.Arguments.Count < 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listAdd' expects at least 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        // first argument must be a list
        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listAdd' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();

        // add arguments to the list
        for (int i = 1; i < call.Arguments.Count; i++)
        {   
            object? value = Evaluate(call.Arguments[i]);
            newList.Add(value);
        }

        return newList;
    }

    private object? CallInternalFunctionListGet(Expression.Call call)
    {
        if (call.Arguments.Count != 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' expects 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? indexObj = Evaluate(call.Arguments[1]);
        if (indexObj is not double indexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' expects second argument to be a number, but got '{GetValueType(indexObj)}'", funcExpr!.Name);
        }

        int index = (int)indexDouble;
        if (index < 0 || index >= list.Count)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGet' index {index} is out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        return list[index];
    }

    private object? CallInternalFunctionClock(Expression.Call call)
    {
        if (call.Arguments.Count != 0)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'clock' expects 0 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    private object? CallInternalFunctionReadInput(Expression.Call call)
    {
        if (call.Arguments.Count > 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'readInput' expects 0 or 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        // if there's one argument, print it as a prompt
        if (call.Arguments.Count == 1)
        {
            object? promptValue = Evaluate(call.Arguments[0]);
            Console.Write(Stringify(promptValue));
        }

        return Console.ReadLine();
    }

    private object? CallInternalFunctionExit(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'exit' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? argValue = Evaluate(call.Arguments[0]);
        if (argValue is not double d)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'exit' expects an integer argument, but got '{GetValueType(argValue)}'", funcExpr!.Name);
        }

        Environment.Exit((int)d);

        // this line will never be reached
        return null;
    }

    // Helper functions
    private static bool IsValueOfType(object? value, string typeName)
    {
        return typeName.ToLower() switch
        {
            "number" => value is double,
            "string" => value is string,
            "bool" => value is bool,
            "list" => value is List<object?>,
            "any" => value is object,
            _ => throw new LangException($"Unknown type '{typeName}'.")
        };
    }

    private static string GetValueType(object? value)
    {
        return value switch
        {
            double => "number",
            string => "string",
            bool => "bool",
            List<object?> => "list",
            null => "nil",
            _ => value.GetType().Name
        };
    }

    private object? LookupVariable(Token name)
    {
        if (CurrentEnvironment.TryGetValue(name.Lexeme, out var value)) return value;
        throw new LangException($"Unknown variable '{name.Lexeme}'.", name);
    }

    private object EvaluateUnary(Expression.Unary u)
    {
        object? right = Evaluate(u.Right);
        return u.Op.Type switch
        {
            TokenType.Minus => -CheckNumber(u.Op, right),
            _ => throw new LangException($"Unknown unary operator '{u.Op.Lexeme}'.", u.Op)
        };
    }

    private object EvaluateBinary(Expression.Binary b)
    {
        object? left = Evaluate(b.Left);
        object? right = Evaluate(b.Right);

        switch (b.Op.Type)
        {
            case TokenType.Plus:
                // add two numbers, otherwise concatenate (e.g. for strings)
                if (left is double ld && right is double rd)
                {
                    return ld + rd;
                }

                return Stringify(left) + Stringify(right);

            case TokenType.Minus:        return CheckNumber(b.Op, left) - CheckNumber(b.Op, right);
            case TokenType.Star:         return CheckNumber(b.Op, left) * CheckNumber(b.Op, right);
            case TokenType.Greater:      return CheckNumber(b.Op, left) > CheckNumber(b.Op, right);
            case TokenType.GreaterEqual: return CheckNumber(b.Op, left) >= CheckNumber(b.Op, right);
            case TokenType.Less:         return CheckNumber(b.Op, left) < CheckNumber(b.Op, right);
            case TokenType.LessEqual:    return CheckNumber(b.Op, left) <= CheckNumber(b.Op, right);
            case TokenType.EqualEqual:   return IsEqual(left, right);
            case TokenType.BangEqual:    return !IsEqual(left, right);
            case TokenType.Slash:
                double divisor = CheckNumber(b.Op, right);
                if (divisor == 0)
                {
                    throw new LangException("Division by zero", b.Op);
                }

                return CheckNumber(b.Op, left) / divisor;

            default:
                throw new LangException($"Unknown operator '{b.Op.Lexeme}'", b.Op);
        }
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

    private static bool IsTruthy(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is bool b)
        {
            return b;
        }

        return true;
    }

    private static bool IsEqual(object? a, object? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    private static string Stringify(object? value)
    {
        if (value is null)
        {
            return "nil";
        }

        if (value is bool b)
        {
            return b ? "true" : "false";
        }

        if (value is double d)
        {
            // trim integers (4 instead of 4.0)
            if (d == (long)d)
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
