using System;

namespace Grains.RazorDesigner.Wiring;

public sealed record SymbolRefExpression : Expression
{
    public Guid SymbolId { get; init; }
}
