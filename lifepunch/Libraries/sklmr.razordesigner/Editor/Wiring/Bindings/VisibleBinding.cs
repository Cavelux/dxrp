namespace Grains.RazorDesigner.Wiring;

public sealed record VisibleBinding : Binding
{
    public Expression Condition { get; init; }
}
