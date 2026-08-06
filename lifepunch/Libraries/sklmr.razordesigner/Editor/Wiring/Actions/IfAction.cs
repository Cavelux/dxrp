using System.Collections.Generic;

namespace Grains.RazorDesigner.Wiring;

public sealed record IfAction : Action
{
    public Expression Condition { get; init; }
    public IReadOnlyList<Action> Then { get; init; } = System.Array.Empty<Action>();
    public IReadOnlyList<Action> Else { get; init; } = System.Array.Empty<Action>();
}
