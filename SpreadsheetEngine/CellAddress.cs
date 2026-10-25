using System.Text;

namespace SpreadsheetEngine;

public readonly record struct CellAddress(int Row, int Column)
{
    public string Name => ColumnName(Column) + (Row + 1);

    public override string ToString() => Name;

    public static string ColumnName(int column)
    {
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        var sb = new StringBuilder();
        int n = column + 1;
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + n % 26));
            n /= 26;
        }
        return sb.ToString();
    }

    public static bool TryParse(string text, out CellAddress address)
    {
        address = default;
        if (string.IsNullOrEmpty(text)) return false;

        int i = 0;
        long column = 0;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            column = column * 26 + (char.ToUpperInvariant(text[i]) - 'A' + 1);
            if (column > int.MaxValue) return false;
            i++;
        }
        if (i == 0 || i == text.Length) return false;

        long row = 0;
        int digitsStart = i;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            row = row * 10 + (text[i] - '0');
            if (row > int.MaxValue) return false;
            i++;
        }
        if (i != text.Length || i == digitsStart || row == 0) return false;

        address = new CellAddress((int)row - 1, (int)column - 1);
        return true;
    }
}
