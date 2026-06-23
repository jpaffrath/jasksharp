namespace JaskLang;

// expressions: everything that evaluates to a value (4, x, x + y, x < y, ...)
public abstract record Expression
{
    public record Literal(object? Value) : Expression;
    public record Variable(Token Name) : Expression;
    public record Unary(Token Op, Expression Right) : Expression;
    public record Binary(Expression Left, Token Op, Expression Right) : Expression;
    public record Grouping(Expression Inner) : Expression;
    public record Call(Expression Callee, List<Expression> Arguments) : Expression;
}

// everything that is executed but does not produce a value itself
public abstract record Statement
{
    public record Store(Token Name, JaskLang.Expression Value) : Statement;

    public record If(JaskLang.Expression Condition, List<Statement> ThenBranch, List<Statement>? ElseBranch) : Statement;

    public record While(JaskLang.Expression Condition, List<Statement> Body) : Statement;

    public record For(Token Variable, JaskLang.Expression Start, JaskLang.Expression End, JaskLang.Expression? Increment, List<Statement> Body) : Statement;

    public record Function(Token Name, List<(Token Name, Token Type)> Params, List<Statement> Body) : Statement;

    public record Expression(JaskLang.Expression Value) : Statement;

    public record Return(JaskLang.Expression? Value) : Statement;
}
