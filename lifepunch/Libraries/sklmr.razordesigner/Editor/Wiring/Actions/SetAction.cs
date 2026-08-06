namespace Grains.RazorDesigner.Wiring;

// `Target = Value;` — assignment to a Symbol field.
public sealed record SetAction : Action
{
    public TargetRef Target { get; init; }
    public Expression Value { get; init; }
}
