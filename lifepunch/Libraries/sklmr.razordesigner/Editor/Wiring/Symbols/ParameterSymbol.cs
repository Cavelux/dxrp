namespace Grains.RazorDesigner.Wiring;

public sealed record ParameterSymbol : Symbol
{
    public string Type { get; init; } = "object";
    public Expression Initial { get; init; }
}
