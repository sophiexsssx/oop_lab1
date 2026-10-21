using System.Numerics;

namespace SpreadsheetEngine;

public readonly struct Value : IEquatable<Value>
{
    private readonly BigInteger _number;
    private readonly bool _bool;

    public bool IsBoolean { get; }

    private Value(BigInteger number, bool b, bool isBoolean)
    {
        _number = number;
        _bool = b;
        IsBoolean = isBoolean;
    }

    public static Value FromNumber(BigInteger n) => new(n, false, false);
    public static Value FromBool(bool b) => new(BigInteger.Zero, b, true);

    public static readonly Value True = FromBool(true);
    public static readonly Value False = FromBool(false);
    public static readonly Value Zero = FromNumber(BigInteger.Zero);

    public BigInteger AsNumber() => IsBoolean ? (_bool ? BigInteger.One : BigInteger.Zero) : _number;
    public bool AsBool() => IsBoolean ? _bool : !_number.IsZero;

    public bool Equals(Value other) =>
        IsBoolean && other.IsBoolean ? _bool == other._bool : AsNumber() == other.AsNumber();

    public override bool Equals(object? obj) => obj is Value v && Equals(v);
    public override int GetHashCode() => AsNumber().GetHashCode();

    public override string ToString() =>
        IsBoolean ? (_bool ? "ІСТИНА" : "ХИБНІСТЬ") : _number.ToString();
}
