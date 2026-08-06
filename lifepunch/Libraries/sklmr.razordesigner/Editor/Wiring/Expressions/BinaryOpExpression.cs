namespace Grains.RazorDesigner.Wiring;

public sealed record BinaryOpExpression : Expression
{
    public BinaryOp Op { get; init; }
    public Expression Left { get; init; }
    public Expression Right { get; init; }
}
