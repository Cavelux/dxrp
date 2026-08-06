using System.Collections.Generic;

namespace Grains.RazorDesigner.Projection;

public readonly record struct ProjectionResult(
    IReadOnlyList<PanelOp> PanelOps,
    IReadOnlyList<string> ScssLines,
    IReadOnlyList<string> RazorAttributes,
    string RazorInnerText )
{
    public static ProjectionResult Empty { get; } = new(
        System.Array.Empty<PanelOp>(),
        System.Array.Empty<string>(),
        System.Array.Empty<string>(),
        null );
}
