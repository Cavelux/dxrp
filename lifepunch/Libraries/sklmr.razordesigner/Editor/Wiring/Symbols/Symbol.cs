using System;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Wiring;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( StateSymbol ),      "State" )]
[JsonDerivedType( typeof( ParameterSymbol ),  "Parameter" )]
[JsonDerivedType( typeof( NodeRefSymbol ),    "NodeRef" )]
[JsonDerivedType( typeof( MethodSymbol ),     "Method" )]
[JsonDerivedType( typeof( LifecycleSymbol ),  "Lifecycle" )]
public abstract record Symbol
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
}
