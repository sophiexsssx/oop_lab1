using System.Numerics;

namespace SpreadsheetEngine;

public interface ICellValueProvider
{
    Value GetCellValue(CellAddress address);
}

public abstract record Node
{
    public abstract Value Evaluate(ICellValueProvider cells);

    public abstract IEnumerable<CellAddress> References();
}

public sealed record NumberNode(BigInteger Value) : Node
{
    public override Value Evaluate(ICellValueProvider cells) => SpreadsheetEngine.Value.FromNumber(Value);
    public override IEnumerable<CellAddress> References() => [];
}

public sealed record BoolNode(bool Value) : Node
{
    public override Value Evaluate(ICellValueProvider cells) => SpreadsheetEngine.Value.FromBool(Value);
    public override IEnumerable<CellAddress> References() => [];
}

public sealed record CellRefNode(CellAddress Address) : Node
{
    public override Value Evaluate(ICellValueProvider cells) => cells.GetCellValue(Address);
    public override IEnumerable<CellAddress> References() => [Address];
}

public sealed record NotNode(Node Operand) : Node
{
    public override Value Evaluate(ICellValueProvider cells) =>
        SpreadsheetEngine.Value.FromBool(!Operand.Evaluate(cells).AsBool());

    public override IEnumerable<CellAddress> References() => Operand.References();
}

public enum BinaryOp { Equal, Less, Greater, And, Or, Eqv, Max, Min }

public sealed record BinaryNode(BinaryOp Op, Node Left, Node Right) : Node
{
    public override Value Evaluate(ICellValueProvider cells)
    {
        Value l = Left.Evaluate(cells);
        Value r = Right.Evaluate(cells);

        return Op switch
        {
            BinaryOp.Equal => SpreadsheetEngine.Value.FromBool(l.Equals(r)),
            BinaryOp.Less => SpreadsheetEngine.Value.FromBool(l.AsNumber() < r.AsNumber()),
            BinaryOp.Greater => SpreadsheetEngine.Value.FromBool(l.AsNumber() > r.AsNumber()),
            BinaryOp.And => SpreadsheetEngine.Value.FromBool(l.AsBool() && r.AsBool()),
            BinaryOp.Or => SpreadsheetEngine.Value.FromBool(l.AsBool() || r.AsBool()),
            BinaryOp.Eqv => SpreadsheetEngine.Value.FromBool(l.AsBool() == r.AsBool()),
            BinaryOp.Max => SpreadsheetEngine.Value.FromNumber(BigInteger.Max(l.AsNumber(), r.AsNumber())),
            BinaryOp.Min => SpreadsheetEngine.Value.FromNumber(BigInteger.Min(l.AsNumber(), r.AsNumber())),
            _ => throw new InvalidOperationException()
        };
    }

    public override IEnumerable<CellAddress> References() => Left.References().Concat(Right.References());
}
