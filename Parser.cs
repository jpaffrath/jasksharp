namespace JaskLang;

public class Parser(List<Token> tokens)
{
    private readonly List<Token> _tokens = tokens;
    private int _current = 0;

    public List<Statement> Parse()
    {
        var statements = new List<Statement>();

        while (!IsAtEnd())
        {
            statements.Add(Statement());
        }

        return statements;
    }

    private Statement Statement()
    {
        if (Match(TokenType.Store))    return StoreStatement();
        if (Match(TokenType.If))       return IfStatement();
        if (Match(TokenType.While))    return WhileStatement();
        if (Match(TokenType.For))      return ForStatement();
        if (Match(TokenType.Function)) return FunctionStatement();
        if (Match(TokenType.Return))   return ReturnStatement();

        // try to parse as expression statement
        return new Statement.Expression(Expression());
    }

    private Statement StoreStatement()
    {
        Expression value = Expression();
        Consume(TokenType.In, "Expected 'in' after value of a store instruction");
        Token name = Consume(TokenType.Identifier, "Expected a variable name after 'in'");

        return new Statement.Store(name, value);
    }

    private Statement IfStatement()
    {
        Expression condition = Expression();

        // run until "else" or "endif"
        var thenBranch = new List<Statement>();
        while (!Check(TokenType.Else) && !Check(TokenType.EndIf) && !IsAtEnd())
        {
            thenBranch.Add(Statement());
        }

        // optional else branch
        List<Statement>? elseBranch = null;
        if (Match(TokenType.Else))
        {
            elseBranch = new List<Statement>();
            while (Check(TokenType.EndIf) == false && IsAtEnd() == false)
            {
                elseBranch.Add(Statement());
            }
        }

        Consume(TokenType.EndIf, "Expected 'endif' at the end of the if statement");

        return new Statement.If(condition, thenBranch, elseBranch);
    }

    private Statement WhileStatement()
    {
        Expression condition = Expression();

        var body = new List<Statement>();
        while (!Check(TokenType.EndWhile) && !IsAtEnd())
        {
            body.Add(Statement());
        }

        Consume(TokenType.EndWhile, "Expected 'endwhile' at the end of the while loop");

        return new Statement.While(condition, body);
    }

    private Statement ForStatement()
    {
        // for <variable> from <start> to <end> [with <increment>]
        // OR
        // for <variable> in <collection>
        Token variable = Consume(TokenType.Identifier, "Expected a variable name after 'for'");
        var body = new List<Statement>();
        
        // Check if it's a "for...in" loop
        if (Check(TokenType.In))
        {
             // consume 'in'
            Advance();
            Expression collection = Expression();

            while (!Check(TokenType.EndFor) && !IsAtEnd())
            {
                body.Add(Statement());
            }

            Consume(TokenType.EndFor, "Expected 'endfor' at the end of the for loop");

            return new Statement.ForIn(variable, collection, body);
        }

        // Traditional "for...from...to" loop
        Consume(TokenType.From, "Expected 'from' after the variable name");
        
        Expression start = Expression();
        Consume(TokenType.To, "Expected 'to' after the start value");
        Expression end = Expression();

        // optional "with" clause for custom increment
        Expression? increment = null;
        if (Match(TokenType.With))
        {
            increment = Expression();
        }

        while (!Check(TokenType.EndFor) && !IsAtEnd())
        {
            body.Add(Statement());
        }

        Consume(TokenType.EndFor, "Expected 'endfor' at the end of the for loop");

        return new Statement.For(variable, start, end, increment, body);
    }

    private Statement FunctionStatement()
    {
        // function <name> ( <params> ) ... end
        Token name = Consume(TokenType.Identifier, "Expected a function name after 'function'");
        Consume(TokenType.LParen, "Expected '(' after function name");

        var parameters = new List<(Token Name, Token Type)>();
        if (!Check(TokenType.RParen))
        {
            do
            {
                Token paramName = Consume(TokenType.Identifier, "Expected parameter name");
                Consume(TokenType.Colon, "Expected ':' after parameter name");
                Token paramType = Consume(TokenType.Identifier, "Expected parameter type");
                parameters.Add((paramName, paramType));
            }
            while (Match(TokenType.Comma));
        }

        Consume(TokenType.RParen, "Expected ')' after parameters");

        var body = new List<Statement>();
        while (!Check(TokenType.End) && !IsAtEnd())
        {
            body.Add(Statement());
        }

        Consume(TokenType.End, "Expected 'end' at the end of the function");

        return new Statement.Function(name, parameters, body);
    }

    private Statement ReturnStatement()
    {
        // return [expression]
        Expression? value = null;
        if (!Check(TokenType.End) && !Check(TokenType.EndIf) && !Check(TokenType.EndWhile) && 
            !Check(TokenType.EndFor) && !Check(TokenType.Else) && !IsAtEnd())
        {
            value = Expression();
        }

        return new Statement.Return(value);
    }

    private Expression Expression() => Equality();

    private Expression Equality()
    {
        Expression expr = Comparison();

        while (Match(TokenType.EqualEqual, TokenType.BangEqual))
        {
            Token op = Previous();
            Expression right = Comparison();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression Comparison()
    {
        Expression expr = Term();

        while (Match(TokenType.Less, TokenType.Greater, TokenType.LessEqual, TokenType.GreaterEqual))
        {
            Token op = Previous();
            Expression right = Term();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression Term()
    {
        Expression expr = Factor();

        while (Match(TokenType.Plus, TokenType.Minus))
        {
            Token op = Previous();
            Expression right = Factor();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression Factor()
    {
        Expression expr = Unary();

        while (Match(TokenType.Star, TokenType.Slash))
        {
            Token op = Previous();
            Expression right = Unary();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression Unary()
    {
        if (Match(TokenType.Minus))
        {
            Token op = Previous();
            Expression right = Unary();
            return new Expression.Unary(op, right);
        }

        return Call();
    }

    private Expression Call()
    {
        Expression expr = Primary();

        while (Match(TokenType.LParen))
        {
            var arguments = new List<Expression>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    arguments.Add(Expression());
                }
                while (Match(TokenType.Comma));
            }

            Consume(TokenType.RParen, "Expected ')' after arguments");
            expr = new Expression.Call(expr, arguments);
        }

        return expr;
    }

    private Expression Primary()
    {
        if (Match(TokenType.Number, TokenType.String)) return new Expression.Literal(Previous().Literal);
        if (Match(TokenType.True))                     return new Expression.Literal(true);
        if (Match(TokenType.False))                    return new Expression.Literal(false);
        if (Match(TokenType.Identifier))               return new Expression.Variable(Previous());

        if (Match(TokenType.LParen))
        {
            Expression expr = Expression();
            Consume(TokenType.RParen, "Expected ')' after expression");
            return new Expression.Grouping(expr);
        }

        throw Error(Peek(), "Expected an expression");
    }

    // Helper functions

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

    private bool Check(TokenType type) => IsAtEnd() == false && Peek().Type == type;

    private Token Advance()
    {
        if (IsAtEnd() == false) _current++;
        return Previous();
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw Error(Peek(), message);
    }

    private Token Peek() => _tokens[_current];

    private bool IsAtEnd() => Peek().Type == TokenType.Eof;

    private Token Previous() => _tokens[_current - 1];

    private LangException Error(Token token, string message)
    {
        string where = token.Type == TokenType.Eof ? "at the end of the file" : $"at '{token.Lexeme}'";
        return new LangException($"Syntax Error {where}: {message}", token);
    }
}
