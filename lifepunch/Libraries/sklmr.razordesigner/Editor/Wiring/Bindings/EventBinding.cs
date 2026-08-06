using System.Collections.Generic;

namespace Grains.RazorDesigner.Wiring;

public sealed record EventBinding : Binding
{
    public string Event { get; init; } = "";
    public IReadOnlyList<Action> Body { get; init; } = System.Array.Empty<Action>();
}
