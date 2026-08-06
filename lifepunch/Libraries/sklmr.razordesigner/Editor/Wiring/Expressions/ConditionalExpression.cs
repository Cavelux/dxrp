namespace Grains.RazorDesigner.Wiring;

// The C# `cond ? then : else` ternary, as a typed IR record.
public sealed record ConditionalExpression : Expression
{
    public Expression Condition { get; init; }
    public Expression Then { get; init; }
    public Expression Else { get; init; }
}
