namespace Grains.RazorDesigner.Wiring;

// `return Value;` or bare `return;` (Value == null). Legal in MethodSymbol bodies.
public sealed record ReturnAction : Action
{
    public Expression Value { get; init; }
}
