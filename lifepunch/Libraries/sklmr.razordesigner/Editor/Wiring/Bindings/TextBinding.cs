namespace Grains.RazorDesigner.Wiring;

public sealed record TextBinding : Binding
{
    public Expression Expression { get; init; }
}
