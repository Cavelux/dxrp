using System;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Wiring;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( MethodCallee ), "Method" )]
[JsonDerivedType( typeof( InlineCallee ), "Inline" )]
public abstract record CalleeRef;

public sealed record MethodCallee : CalleeRef
{
    public Guid SymbolId { get; init; }
}

public sealed record InlineCallee : CalleeRef
{
    // Raw C# (a name, dotted member path, etc.). Treated as opaque by the projector.
    public string Code { get; init; } = "";
}
