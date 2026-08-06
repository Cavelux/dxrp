namespace Grains.RazorDesigner.Wiring;

public sealed record MemberAccessExpression : Expression
{
    public Expression Receiver { get; init; }
    public string Member { get; init; } = "";
}
