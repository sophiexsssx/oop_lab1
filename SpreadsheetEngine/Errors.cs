namespace SpreadsheetEngine;

public sealed class SyntaxErrorException : Exception
{
    public int Position { get; }
    public int Length { get; }

    public SyntaxErrorException(string message, int position, int length = 1)
        : base($"Синтаксична помилка (позиція {position}): {message}")
    {
        Position = position;
        Length = Math.Max(1, length);
    }
}

public sealed class EvaluationException : Exception
{
    public EvaluationException(string message) : base(message) { }
}
