using System.Collections.Generic;

namespace Grains.RazorDesigner.Wiring;

public sealed record MethodSymbol : Symbol
{
    public SymbolVisibility Visibility { get; init; } = SymbolVisibility.Private;
    public string ReturnType { get; init; } = "void";
    public IReadOnlyList<MethodParameter> Parameters { get; init; } = System.Array.Empty<MethodParameter>();
    public IReadOnlyList<Action> Body { get; init; } = System.Array.Empty<Action>();
}
