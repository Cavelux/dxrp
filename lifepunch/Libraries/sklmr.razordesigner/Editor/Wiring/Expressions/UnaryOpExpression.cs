namespace Grains.RazorDesigner.Wiring;

public sealed record UnaryOpExpression : Expression
{
    public UnaryOp Op { get; init; }
    public Expression Operand { get; init; }
}
