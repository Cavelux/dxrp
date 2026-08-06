using System;

namespace Grains.RazorDesigner.Wiring;

public sealed record NodeRefSymbol : Symbol
{
    public string Type { get; init; } = "Panel";
    public Guid TargetNodeId { get; init; }
}
