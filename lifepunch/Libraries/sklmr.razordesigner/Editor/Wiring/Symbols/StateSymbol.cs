namespace Grains.RazorDesigner.Wiring;

public sealed record StateSymbol : Symbol
{
    public string Type { get; init; } = "object";
    public Expression Initial { get; init; }
    public SymbolVisibility Visibility { get; init; } = SymbolVisibility.Private;
}
