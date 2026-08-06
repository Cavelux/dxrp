using System.Collections.Generic;

namespace Grains.RazorDesigner.Wiring;

public sealed record LifecycleSymbol : Symbol
{
    public LifecycleHook Hook { get; init; }
    public IReadOnlyList<Action> Body { get; init; } = System.Array.Empty<Action>();
}
