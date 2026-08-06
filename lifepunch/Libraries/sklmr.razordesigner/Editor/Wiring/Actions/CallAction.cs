using System.Collections.Generic;

namespace Grains.RazorDesigner.Wiring;

public sealed record CallAction : Action
{
    public CalleeRef Callee { get; init; }
    public IReadOnlyList<Expression> Args { get; init; } = System.Array.Empty<Expression>();
    public bool Await { get; init; }
}
