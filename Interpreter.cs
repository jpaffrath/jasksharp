namespace JaskLang;

public class ReturnException : Exception
{
    public object? Value { get; }
    public ReturnException(object? value) : base() { Value = value; }
}

public partial class Interpreter
{
    // dictionary for functions: "name(type1,type2,...)" -> (parameters, body)
    private readonly Dictionary<string, (List<(Token Name, Token Type)> Params, List<Statement> Body)> _functions = [];

    // dictionary for struct definitions: name -> body statements
    private readonly Dictionary<string, List<Statement>> _structs = [];

    // stack for environments to manage scopes
    private readonly Stack<Dictionary<string, object?>> _scopes = new();
    
    private Dictionary<string, object?> _globalEnvironment = [];

    private Dictionary<string, object?> CurrentEnvironment => _scopes.Peek();

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
}