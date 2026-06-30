using JaskLang;

static void Run(Interpreter interpreter, string source)
{
    try
    {
        var lexer = new Lexer(source);
        var tokens = lexer.ScanTokens();

        var parser = new Parser(tokens);
        var statements = parser.Parse();

        interpreter.Interpret(statements);
    }
    catch (LangException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    }
}

// first arg should be a .jask file, otherwise run in interactive mode
if (args.Length == 1)
{
    string file = args[0];

    if (File.Exists(file) == false)
    {
        Console.Error.WriteLine($"File '{file}' not found.");
        return;
    }

    if (Path.GetExtension(file) != ".jask")
    {
        Console.Error.WriteLine($"File '{file}' is not a jask file.");
        return;
    }

     Run(new Interpreter(), File.ReadAllText(file));
}
else
{
    RunInteractiveMode();
}

static void RunInteractiveMode()
{
    Console.WriteLine("jask interpreter version 2.0.0");
    var interpreter = new Interpreter();

    while (true)
    {
        Console.Write("jask ~> ");

        string? line = Console.ReadLine();
        if (line is null || line.Trim() == "exit")
        {
            break;
        }

        Run(interpreter, line);
    }
}
