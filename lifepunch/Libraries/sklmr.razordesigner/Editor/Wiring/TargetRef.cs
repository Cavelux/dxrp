using System;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Wiring;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( SymbolTarget ), "Symbol" )]
public abstract record TargetRef;

public sealed record SymbolTarget : TargetRef
{
    public Guid SymbolId { get; init; }
}
