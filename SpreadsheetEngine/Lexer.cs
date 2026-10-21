using System.Numerics;
using System.Text;

namespace SpreadsheetEngine;

public enum TokenType
{
    Number,
    CellRef,
    True,
    False,
    Not,
    And,
    Or,
    Eqv,
    Max,
    Min,
    Equal,
    Less,
    Greater,
    LParen,
    RParen,
    Comma,
    End
}

public sealed record Token(TokenType Type, string Text, int Position, BigInteger Number = default, CellAddress Cell = default);

public static class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["not"] = TokenType.Not,
        ["and"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["eqv"] = TokenType.Eqv,
        ["max"] = TokenType.Max,
        ["min"] = TokenType.Min,
    };

    private static readonly Dictionary<char, char> CyrillicLookalikes = new()
    {
        ['А'] = 'A', ['В'] = 'B', ['С'] = 'C', ['Е'] = 'E', ['Н'] = 'H', ['І'] = 'I', ['К'] = 'K',
        ['М'] = 'M', ['О'] = 'O', ['Р'] = 'P', ['Т'] = 'T', ['Х'] = 'X',
        ['а'] = 'a', ['с'] = 'c', ['е'] = 'e', ['і'] = 'i', ['о'] = 'o', ['р'] = 'p', ['х'] = 'x',
    };

    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];
            int pos = i + 1;

            if (char.IsWhiteSpace(c)) { i++; continue; }

            switch (c)
            {
                case '(': tokens.Add(new Token(TokenType.LParen, "(", pos)); i++; continue;
                case ')': tokens.Add(new Token(TokenType.RParen, ")", pos)); i++; continue;
                case ',': tokens.Add(new Token(TokenType.Comma, ",", pos)); i++; continue;
                case '=': tokens.Add(new Token(TokenType.Equal, "=", pos)); i++; continue;
                case '<': tokens.Add(new Token(TokenType.Less, "<", pos)); i++; continue;
                case '>': tokens.Add(new Token(TokenType.Greater, ">", pos)); i++; continue;
            }

            if (char.IsAsciiDigit(c))
            {
                int start = i;
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                if (i < text.Length && IsIdentChar(text[i]))
                    throw new SyntaxErrorException("після числа не може йти літера", i + 1);
                string digits = text[start..i];
                tokens.Add(new Token(TokenType.Number, digits, pos, BigInteger.Parse(digits)));
                continue;
            }

            if (IsIdentChar(c))
            {
                int start = i;
                var sb = new StringBuilder();
                while (i < text.Length && (IsIdentChar(text[i]) || char.IsAsciiDigit(text[i])))
                {
                    sb.Append(Normalize(text[i]));
                    i++;
                }
                string word = sb.ToString();
                string original = text[start..i];

                if (Keywords.TryGetValue(word, out var kw))
                    tokens.Add(new Token(kw, original, pos));
                else if (CellAddress.TryParse(word, out var addr))
                    tokens.Add(new Token(TokenType.CellRef, addr.Name, pos, Cell: addr));
                else
                    throw new SyntaxErrorException(
                        $"невідомий ідентифікатор \"{original}\" (очікувалось ім'я клітинки, напр. A1, " +
                        "або одне з: max, min, not, and, or, eqv, true, false)", pos, original.Length);
                continue;
            }

            throw new SyntaxErrorException($"неприпустимий символ '{c}'", pos);
        }

        tokens.Add(new Token(TokenType.End, "", text.Length + 1));
        return tokens;
    }

    private static bool IsIdentChar(char c) => char.IsAsciiLetter(c) || CyrillicLookalikes.ContainsKey(c);

    private static char Normalize(char c) => CyrillicLookalikes.TryGetValue(c, out var latin) ? latin : c;
}
