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
        if (Match(TokenType.Set))      return SetStatement();
        if (Match(TokenType.If))       return IfStatement();
        if (Match(TokenType.While))    return WhileStatement();
        if (Match(TokenType.For))      return ForStatement();
        if (Match(TokenType.Function)) return FunctionStatement();
        if (Match(TokenType.Struct))   return StructStatement();
        if (Match(TokenType.Use))      return UseStatement();
        if (Match(TokenType.Return))   return ReturnStatement();

        // try to parse as expression statement
        return new Statement.Expression(Expression());
    }

    private Statement SetStatement()
    {
        Token name = Consume(TokenType.Identifier, "Expected a variable name after 'set'");
        Consume(TokenType.To, "Expected 'to' after variable name");
        Expression source = Expression();

        // check for struct update: set <target> to <source> update <field> = <value> [update ...]*
        if (Check(TokenType.Update))
        {
            var updates = new List<(Token Field, Expression Value)>();

            while (Match(TokenType.Update))
            {
                Token field = Consume(TokenType.Identifier, "Expected a field name after 'update'");
                Consume(TokenType.Assign, "Expected '=' after field name in update");
                Expression value = Expression();
                updates.Add((field, value));
            }

            return new Statement.StructUpdate(source, updates, name);
        }

        return new Statement.Set(name, source);
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

    private Statement StructStatement()
    {
        Token name = Consume(TokenType.Identifier, "Expected a struct name after 'struct'");

        var body = new List<Statement>();
        while (!Check(TokenType.EndStruct) && !IsAtEnd())
        {
            body.Add(Statement());
        }

        Consume(TokenType.EndStruct, "Expected 'endstruct' at the end of the struct definition");

        return new Statement.Struct(name, body);
    }

    private Statement UseStatement()
    {
        Expression modulePath = Expression();
        return new Statement.Use(modulePath);
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

    private Expression Expression() => Or();

    private Expression Or()
    {
        Expression expr = And();

        while (Match(TokenType.Or))
        {
            Token op = Previous();
            Expression right = And();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression And()
    {
        Expression expr = Not();

        while (Match(TokenType.And))
        {
            Token op = Previous();
            Expression right = Not();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression Not()
    {
        if (Match(TokenType.Not))
        {
            Token op = Previous();
            Expression right = Not(); // right-associative: not not x is valid
            return new Expression.Unary(op, right);
        }

        return Equality();
    }

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

        // modulo has the same priority as multiplication and division
        while (Match(TokenType.Star, TokenType.Slash, TokenType.Modulo))
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
            // detect named-argument call: Name(param = value, ...)
            if (expr is Expression.Variable callee && !Check(TokenType.RParen) && CheckNext(TokenType.Assign))
            {
                var namedArgs = new List<(Token ParamName, Expression Value)>();
                do
                {
                    Token paramName = Consume(TokenType.Identifier, "Expected a parameter name");
                    Consume(TokenType.Assign, "Expected '=' after parameter name");
                    Expression value = Expression();
                    namedArgs.Add((paramName, value));
                }
                while (Match(TokenType.Comma));

                Consume(TokenType.RParen, "Expected ')' after named arguments");
                return new Expression.NamedCall(callee.Name, namedArgs);
            }

            // regular call
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
        if (Match(TokenType.Nil))                      return new Expression.Literal(null);
        if (Match(TokenType.Identifier))
        {
            Token ident = Previous();
            // check for struct field access: myStruct->myField
            if (Match(TokenType.Arrow))
            {
                Token member = Consume(TokenType.Identifier, "Expected a member name after '->'");
                return new Expression.MemberAccess(ident, member);
            }
            return new Expression.Variable(ident);
        }

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

    private bool CheckNext(TokenType type) => _current + 1 < _tokens.Count && _tokens[_current + 1].Type == type;

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
