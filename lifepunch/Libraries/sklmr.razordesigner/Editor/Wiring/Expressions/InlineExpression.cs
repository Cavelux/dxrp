namespace Grains.RazorDesigner.Wiring;

public sealed record InlineExpression : Expression
{
    public string Code { get; init; } = "";
}
