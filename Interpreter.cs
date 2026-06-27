namespace JaskLang;

public class ReturnException : Exception
{
    public object? Value { get; }
    public ReturnException(object? value) : base() { Value = value; }
}
// structs itself are immutable, hence IReadOnlyDictionary for fields
public class StructInstance
{
    public string TypeName { get; }
    public IReadOnlyDictionary<string, object?> Fields { get; }

    public StructInstance(string typeName, Dictionary<string, object?> fields)
    {
        TypeName = typeName;
        Fields = fields.AsReadOnly();
    }

    // returns a new StructInstance with one field replaced, leaving this instance unchanged
    public StructInstance WithField(string name, object? value)
    {
        var newFields = new Dictionary<string, object?>(Fields) { [name] = value };
        return new StructInstance(TypeName, newFields);
    }

    public override string ToString()
    {
        var fields = string.Join(", ", Fields.Select(kv => $"{kv.Key}: {Interpreter.Stringify(kv.Value)}"));
        return $"{TypeName} {{ {fields} }}";
    }
}

public delegate object? InternalFunctionDelegate(Expression.Call call);

public class Interpreter
{
    // dictionary for functions: "name(type1,type2,...)" -> (parameters, body)
    private readonly Dictionary<string, (List<(Token Name, Token Type)> Params, List<Statement> Body)> _functions = [];

    // dictionary for internal functions: name -> delegate
    private readonly Dictionary<string, InternalFunctionDelegate> _internalFunctions = [];

    // dictionary for struct definitions: name -> body statements
    private readonly Dictionary<string, List<Statement>> _structs = [];

    // stack for environments to manage scopes
    private readonly Stack<Dictionary<string, object?>> _scopes = new();
    
    // global environment
    private Dictionary<string, object?> _globalEnvironment = [];

    public Interpreter()
    {
        _scopes.Push(_globalEnvironment);
        initInternalFunctions();
    }

    public void Interpret(List<Statement> statements)
    {
        foreach (var statement in statements)
        {
            Execute(statement);
        }
    }

    private void initInternalFunctions()
    {
        // standard functions
        _internalFunctions["print"] = CallInternalFunctionPrint;
        _internalFunctions["type"]  = CallInternalFunctionType;
        _internalFunctions["clock"] = CallInternalFunctionClock;
        _internalFunctions["exit"]  = CallInternalFunctionExit;
        _internalFunctions["stringFrom"]  = CallInternalFunctionStringFrom;

        // list functions
        _internalFunctions["list"]         = CallInternalFunctionList;
        _internalFunctions["listSize"]     = CallInternalFunctionListSize;
        _internalFunctions["listAdd"]      = CallInternalFunctionListAdd;
        _internalFunctions["listGet"]      = CallInternalFunctionListGet;
        _internalFunctions["listGetRange"] = CallInternalFunctionListGetRange;
        _internalFunctions["listSet"]      = CallInternalFunctionListSet;
        _internalFunctions["listRemove"]   = CallInternalFunctionListRemove;
        _internalFunctions["listReverse"]  = CallInternalFunctionListReverse;
        _internalFunctions["listExtend"]   = CallInternalFunctionListExtend;

        // IO functions
        _internalFunctions["readInput"] = CallInternalFunctionReadInput; 
    }

    private void Execute(Statement statement)
    {
        switch (statement)
        {
            case Statement.Set s:
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
                _functions[FunctionKey(f.Name.Lexeme, f.Params)] = (f.Params, f.Body);
                break;

            case Statement.Struct s:
                _structs[s.Name.Lexeme] = s.Body;
                break;

            case Statement.StructUpdate su:
                object? sourceObj = Evaluate(su.Source);
                if (sourceObj is not StructInstance sourceInstance)
                {
                    throw new LangException($"'update' expects a struct instance, but got '{GetValueType(sourceObj)}'", su.Target);
                }

                // fold each update over the instance, producing a new copy each time
                StructInstance updated = sourceInstance;
                foreach (var (field, valueExpr) in su.Updates)
                {
                    if (!updated.Fields.ContainsKey(field.Lexeme))
                    {
                        throw new LangException($"Struct '{updated.TypeName}' has no field '{field.Lexeme}'", field);
                    }
                    updated = updated.WithField(field.Lexeme, Evaluate(valueExpr));
                }

                CurrentEnvironment[su.Target.Lexeme] = updated;
                break;

            case Statement.Expression e:
                Evaluate(e.Value);
                break;

            case Statement.Use u:
                object? value = Evaluate(u.Value);

                if (value is not string)
                {
                    throw new LangException($"'use' statement expects a string as module path, but got '{GetValueType(value)}'");
                }

                string modulePath = (string)value;

                if (File.Exists(modulePath) == false)
                {
                    throw new LangException($"Module at '{modulePath}' could not be loaded");
                }
                else
                {
                    var lexer = new Lexer(File.ReadAllText(modulePath));
                    var tokens = lexer.ScanTokens();
                    var parser = new Parser(tokens);
                    var statements = parser.Parse();
                    Interpret(statements);
                }
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
            Expression.Literal      l => l.Value,
            Expression.Grouping     g => Evaluate(g.Inner),
            Expression.Variable     v => LookupVariable(v.Name),
            Expression.Unary        u => EvaluateUnary(u),
            Expression.Binary       b => EvaluateShortCircuit(b),
            Expression.Call         c => EvaluateCall(c),
            Expression.NamedCall   nc => EvaluateNamedCall(nc),
            Expression.StructCall  sc => EvaluateStructCall(sc),
            Expression.MemberAccess m => EvaluateMemberAccess(m),
            _ => throw new LangException($"Unknown expression: {expression}")
        };
    }

    // handles and/or with short-circuit evaluation, delegates everything else to EvaluateBinary
    private object? EvaluateShortCircuit(Expression.Binary b)
    {
        if (b.Op.Type == TokenType.And)
        {
            object? left = Evaluate(b.Left);
            if (!IsTruthy(left)) return false; // short-circuit: left is false, skip right
            return IsTruthy(Evaluate(b.Right));
        }

        if (b.Op.Type == TokenType.Or)
        {
            object? left = Evaluate(b.Left);
            if (IsTruthy(left)) return true; // short-circuit: left is true, skip right
            return IsTruthy(Evaluate(b.Right));
        }

        return EvaluateBinary(b);
    }

    private object? EvaluateCall(Expression.Call call)
    {
        Expression.Variable? funcExpr = call.Callee as Expression.Variable;
        if (funcExpr == null)
        {
            throw new LangException("Can only call functions by name");
        }

        string funcName = funcExpr.Name.Lexeme;

        // check for user-defined overloads first (they take priority over internals)
        bool hasUserOverload = _functions.Keys.Any(k => k.StartsWith(funcName + "("));

        // if no user overload exists, delegate to internal immediately (avoids double arg evaluation)
        if (!hasUserOverload && _internalFunctions.TryGetValue(funcName, out var internalFunc))
        {
            return internalFunc(call);
        }

        // new struct with no overwritten fields: MyStruct()
        if (_structs.TryGetValue(funcName, out var structBody))
        {
            if (call.Arguments.Count != 0)
            {
                throw new LangException($"Struct '{funcName}' instantiation with positional arguments is not supported. Use named fields: {funcName}(field = value, ...)", funcExpr.Name);
            }

            var fields = new Dictionary<string, object?>();

            // run the struct body in a temporary scope, capturing Store results as fields
            _scopes.Push(new Dictionary<string, object?>());
            try
            {
                foreach (var stmt in structBody)
                {
                    Execute(stmt);
                }
                // copy everything stored in that scope into the fields dictionary
                foreach (var kv in _scopes.Peek())
                {
                    fields[kv.Key] = kv.Value;
                }
            }
            finally
            {
                _scopes.Pop();
            }

            return new StructInstance(funcName, fields);
        }

        // evaluate arguments first so we can match overloads by compatible types
        var argValues = call.Arguments.Select(a => Evaluate(a)).ToList();

        var overloads = _functions
            .Where(kv => kv.Key.StartsWith(funcName + "("))
            .Select(kv => kv.Value)
            .ToList();

        if (overloads.Count == 0)
            throw new LangException($"Unknown function '{funcName}'", funcExpr.Name);

        // pick first overload whose arity and parameter types are all compatible
        var match = overloads.FirstOrDefault(o =>
            o.Params.Count == argValues.Count &&
            o.Params.Zip(argValues, (p, v) => IsValueOfType(v, p.Type.Lexeme)).All(x => x));

        if (match == default)
        {
            // no user overload matched — fall back to internal if one exists
            if (_internalFunctions.TryGetValue(funcName, out var internalFunction))
                return internalFunction(call);

            bool anyArity = overloads.Any(o => o.Params.Count == argValues.Count);
            if (anyArity)
                throw new LangException(
                    $"Function '{funcName}' has no overload matching types ({string.Join(", ", argValues.Select(GetValueType))})", funcExpr.Name);
            throw new LangException(
                $"Function '{funcName}' has no overload that takes {argValues.Count} argument(s)", funcExpr.Name);
        }

        var (parameters, body) = match;

        var functionEnv = new Dictionary<string, object?>();
        for (int i = 0; i < parameters.Count; i++)
            functionEnv[parameters[i].Name.Lexeme] = argValues[i];

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

    private object? EvaluateNamedCall(Expression.NamedCall call)
    {
        string name = call.Name.Lexeme;

        // if it's a struct, delegate to struct instantiation
        if (_structs.ContainsKey(name))
        {
            var fieldInits = call.Args.Select(a => (a.ParamName, a.Value)).ToList();
            return EvaluateStructCall(new Expression.StructCall(call.Name, fieldInits));
        }

        // find all overloads for this name
        var overloads = _functions
            .Where(kv => kv.Key.StartsWith(name + "("))
            .Select(kv => kv.Value)
            .ToList();

        if (overloads.Count == 0)
            throw new LangException($"Unknown function '{name}'", call.Name);

        // evaluate args up front so we can match on types too
        var evaluatedArgs = call.Args
            .Select(a => (a.ParamName, Value: Evaluate(a.Value)))
            .ToList();

        var suppliedNames = call.Args.Select(a => a.ParamName.Lexeme).ToHashSet();

        // find overload matching both param names and compatible types
        var match = overloads.FirstOrDefault(o =>
            o.Params.Count == call.Args.Count &&
            o.Params.Select(p => p.Name.Lexeme).ToHashSet().SetEquals(suppliedNames) &&
            o.Params.All(p =>
            {
                var arg = evaluatedArgs.First(a => a.ParamName.Lexeme == p.Name.Lexeme);
                return IsValueOfType(arg.Value, p.Type.Lexeme);
            }));

        if (match == default)
            throw new LangException(
                $"Function '{name}' has no overload matching named parameters ({string.Join(", ", suppliedNames)})", call.Name);

        // bind in parameter declaration order
        var functionEnv = new Dictionary<string, object?>();
        foreach (var param in match.Params)
        {
            var arg = evaluatedArgs.First(a => a.ParamName.Lexeme == param.Name.Lexeme);
            functionEnv[param.Name.Lexeme] = arg.Value;
        }

        _scopes.Push(functionEnv);
        try
        {
            foreach (var stmt in match.Body)
                Execute(stmt);
        }
        catch (ReturnException ex)
        {
            return ex.Value;
        }
        finally
        {
            _scopes.Pop();
        }

        return null;
    }

        private object? EvaluateStructCall(Expression.StructCall call)
    {
        string structName = call.Name.Lexeme;

        if (!_structs.TryGetValue(structName, out var structBody))
        {
            throw new LangException($"Unknown struct '{structName}'", call.Name);
        }

        // run the body to get default field values
        var fields = new Dictionary<string, object?>();
        _scopes.Push(new Dictionary<string, object?>());

        try
        {
            foreach (var stmt in structBody)
            {
                Execute(stmt);
            }

            foreach (var kv in _scopes.Peek())
            {
                fields[kv.Key] = kv.Value;
            }
        }
        finally
        {
            _scopes.Pop();
        }

        // apply named field initializers, validating each field name
        foreach (var (field, valueExpr) in call.FieldInits)
        {
            if (!fields.ContainsKey(field.Lexeme))
            {
                throw new LangException($"Struct '{structName}' has no field '{field.Lexeme}'", field);
            }

            fields[field.Lexeme] = Evaluate(valueExpr);
        }

        return new StructInstance(structName, fields);
    }

    private object? EvaluateMemberAccess(Expression.MemberAccess m)
    {
        object? obj = LookupVariable(m.StructName);

        if (obj is not StructInstance instance)
        {
            throw new LangException($"Variable '{m.StructName.Lexeme}' is not a struct instance", m.StructName);
        }

        if (!instance.Fields.TryGetValue(m.Member.Lexeme, out var fieldValue))
        {
            throw new LangException($"Struct '{instance.TypeName}' has no member '{m.Member.Lexeme}'", m.Member);
        }

        return fieldValue;
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

    private object? CallInternalFunctionListSize(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSize' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSize' expects a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        return (double)list.Count;
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

    private object? CallInternalFunctionListGetRange(Expression.Call call)
    {
        if (call.Arguments.Count != 3)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects 3 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? startIndexObj = Evaluate(call.Arguments[1]);
        if (startIndexObj is not double startIndexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects second argument to be a number, but got '{GetValueType(startIndexObj)}'", funcExpr!.Name);
        }

        object? endIndexObj = Evaluate(call.Arguments[2]);
        if (endIndexObj is not double endIndexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' expects third argument to be a number, but got '{GetValueType(endIndexObj)}'", funcExpr!.Name);
        }

        int startIndex = (int)startIndexDouble;
        int endIndex = (int)endIndexDouble;

        if (startIndex < 0 || endIndex >= list.Count || startIndex > endIndex)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listGetRange' indices [{startIndex}, {endIndex}] are out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        return list.GetRange(startIndex, endIndex - startIndex + 1);
    }

    private object? CallInternalFunctionListSet(Expression.Call call)
    {
        if (call.Arguments.Count != 3)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' expects 3 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? indexObj = Evaluate(call.Arguments[1]);
        if (indexObj is not double indexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' expects second argument to be a number, but got '{GetValueType(indexObj)}'", funcExpr!.Name);
        }

        int index = (int)indexDouble;
        if (index < 0 || index >= list.Count)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listSet' index {index} is out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        object? value = Evaluate(call.Arguments[2]);

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();
        newList[index] = value;

        return newList;
    }

    private object? CallInternalFunctionListRemove(Expression.Call call)
    {
        if (call.Arguments.Count != 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' expects 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        object? indexObj = Evaluate(call.Arguments[1]);
        if (indexObj is not double indexDouble)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' expects second argument to be a number, but got '{GetValueType(indexObj)}'", funcExpr!.Name);
        }

        int index = (int)indexDouble;
        if (index < 0 || index >= list.Count)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listRemove' index {index} is out of bounds for list of size {list.Count}", funcExpr!.Name);
        }

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();
        newList.RemoveAt(index);

        return newList;
    }

    private object? CallInternalFunctionListReverse(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listReverse' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj = Evaluate(call.Arguments[0]);
        if (listObj is not List<object?> list)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listReverse' expects first argument to be a list, but got '{GetValueType(listObj)}'", funcExpr!.Name);
        }

        // create a copy of the list to avoid modifying the original
        var newList = list.ToList();
        newList.Reverse();

        return newList;
    }

    private object? CallInternalFunctionListExtend(Expression.Call call)
    {
        if (call.Arguments.Count != 2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listExtend' expects 2 arguments, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? listObj1 = Evaluate(call.Arguments[0]);
        if (listObj1 is not List<object?> list1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listExtend' expects first argument to be a list, but got '{GetValueType(listObj1)}'", funcExpr!.Name);
        }

        object? listObj2 = Evaluate(call.Arguments[1]);
        if (listObj2 is not List<object?> list2)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'listExtend' expects second argument to be a list, but got '{GetValueType(listObj2)}'", funcExpr!.Name);
        }

        // create a copy of the first list to avoid modifying the original
        var newList = list1.ToList();
        newList.AddRange(list2);

        return newList;
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

    private object? CallInternalFunctionStringFrom(Expression.Call call)
    {
        if (call.Arguments.Count != 1)
        {
            Expression.Variable? funcExpr = call.Callee as Expression.Variable;
            throw new LangException($"Function 'stringFrom' expects 1 argument, but got {call.Arguments.Count}", funcExpr!.Name);
        }

        object? argValue = Evaluate(call.Arguments[0]);
        return Stringify(argValue);
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

    private object? LookupVariable(Token name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name.Lexeme, out var value)) {
                return value;
            }
        }

        throw new LangException($"Unknown variable '{name.Lexeme}'.", name);
    }

    private object EvaluateUnary(Expression.Unary u)
    {
        object? right = Evaluate(u.Right);
        return u.Op.Type switch
        {
            TokenType.Minus => -CheckNumber(u.Op, right),
            TokenType.Not   => !IsTruthy(right),
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
            case TokenType.Modulo:       return CheckNumber(b.Op, left) % CheckNumber(b.Op, right);
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

    public static string Stringify(object? value)
    {
        if (value is null) return "nil";
        if (value is bool b) return b ? "true" : "false";
        if (value is StructInstance si) return si.ToString();

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