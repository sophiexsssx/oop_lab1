namespace SpreadsheetEngine;

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;
    private int _depth;

    public const int MaxDepth = 100;

    private Parser(List<Token> tokens) => _tokens = tokens;

    public static Node Parse(string expression)
    {
        var tokens = Lexer.Tokenize(expression);
        var parser = new Parser(tokens);

        if (parser.Current.Type == TokenType.End)
            throw new SyntaxErrorException("порожній вираз", 1);

        Node result = parser.ParseExpression();

        if (parser.Current.Type != TokenType.End)
        {
            var t = parser.Current;
            string hint = t.Type == TokenType.RParen ? "зайва закриваюча дужка" : $"неочікувана лексема \"{t.Text}\"";
            if (IsComparison(t.Type)) hint = "порівняння не можна записувати ланцюжком — використайте дужки та and";
            throw new SyntaxErrorException(hint, t.Position, t.Text.Length);
        }
        return result;
    }

    public static SyntaxErrorException? Validate(string expression)
    {
        try { Parse(expression); return null; }
        catch (SyntaxErrorException e) { return e; }
    }

    private Token Current => _tokens[_pos];

    private Token Advance() => _tokens[_pos++];

    private Token Expect(TokenType type, string what)
    {
        if (Current.Type != type)
            throw new SyntaxErrorException($"очікувалось {what}, а знайдено {Describe(Current)}",
                Current.Position, Current.Text.Length);
        return Advance();
    }

    private Node ParseExpression()
    {
        if (++_depth > MaxDepth)
            throw new SyntaxErrorException($"вираз вкладений занадто глибоко (більше {MaxDepth} рівнів)", Current.Position);
        try { return ParseEqv(); }
        finally { _depth--; }
    }

    private Node ParseEqv()
    {
        Node left = ParseOr();
        while (Current.Type == TokenType.Eqv)
        {
            Advance();
            left = new BinaryNode(BinaryOp.Eqv, left, ParseOr());
        }
        return left;
    }

    private Node ParseOr()
    {
        Node left = ParseAnd();
        while (Current.Type == TokenType.Or)
        {
            Advance();
            left = new BinaryNode(BinaryOp.Or, left, ParseAnd());
        }
        return left;
    }

    private Node ParseAnd()
    {
        Node left = ParseNot();
        while (Current.Type == TokenType.And)
        {
            Advance();
            left = new BinaryNode(BinaryOp.And, left, ParseNot());
        }
        return left;
    }

    private Node ParseNot()
    {
        if (Current.Type == TokenType.Not)
        {
            Advance();
            if (++_depth > MaxDepth)
                throw new SyntaxErrorException($"вираз вкладений занадто глибоко (більше {MaxDepth} рівнів)", Current.Position);
            try { return new NotNode(ParseNot()); }
            finally { _depth--; }
        }
        return ParseComparison();
    }

    private Node ParseComparison()
    {
        Node left = ParseOperand();
        if (IsComparison(Current.Type))
        {
            var op = Advance().Type switch
            {
                TokenType.Equal => BinaryOp.Equal,
                TokenType.Less => BinaryOp.Less,
                _ => BinaryOp.Greater
            };
            left = new BinaryNode(op, left, ParseOperand());
        }
        return left;
    }

    private Node ParseOperand()
    {
        Token t = Current;
        switch (t.Type)
        {
            case TokenType.Number:
                Advance();
                return new NumberNode(t.Number);

            case TokenType.CellRef:
                Advance();
                return new CellRefNode(t.Cell);

            case TokenType.True:
                Advance();
                return new BoolNode(true);

            case TokenType.False:
                Advance();
                return new BoolNode(false);

            case TokenType.LParen:
            {
                Advance();
                Node inner = ParseExpression();
                if (Current.Type != TokenType.RParen)
                    throw new SyntaxErrorException(
                        $"очікувалась закриваюча дужка ')' до дужки на позиції {t.Position}, а знайдено {Describe(Current)}",
                        Current.Position, Current.Text.Length);
                Advance();
                return inner;
            }

            case TokenType.Max:
            case TokenType.Min:
            {
                Advance();
                string name = t.Type == TokenType.Max ? "max" : "min";
                Expect(TokenType.LParen, $"'(' після {name}");
                Node a = ParseExpression();
                Expect(TokenType.Comma, $"',' — функція {name} приймає рівно два аргументи");
                Node b = ParseExpression();
                if (Current.Type == TokenType.Comma)
                    throw new SyntaxErrorException($"функція {name} приймає рівно два аргументи", Current.Position);
                Expect(TokenType.RParen, $"')' після аргументів {name}");
                return new BinaryNode(t.Type == TokenType.Max ? BinaryOp.Max : BinaryOp.Min, a, b);
            }

            case TokenType.Not:
                throw new SyntaxErrorException("тут 'not' треба взяти в дужки, напр. A1 = (not B1)", t.Position, t.Text.Length);

            default:
                throw new SyntaxErrorException(
                    $"очікувалось число, клітинка, true/false, max/min або '(', а знайдено {Describe(t)}",
                    t.Position, t.Text.Length);
        }
    }

    private static bool IsComparison(TokenType t) => t is TokenType.Equal or TokenType.Less or TokenType.Greater;

    private static string Describe(Token t) => t.Type == TokenType.End ? "кінець виразу" : $"\"{t.Text}\"";
}
