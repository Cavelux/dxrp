using System.Collections.Generic;

namespace Grains.RazorDesigner.Wiring;

public sealed record WiringEnvelope
{
    public string Namespace { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string BaseClass { get; init; } = "PanelComponent";
    public IReadOnlyList<Symbol> Symbols { get; init; } = System.Array.Empty<Symbol>();
    public IReadOnlyList<string> Usings { get; init; } = System.Array.Empty<string>();

    public static readonly WiringEnvelope Empty = new();
}
