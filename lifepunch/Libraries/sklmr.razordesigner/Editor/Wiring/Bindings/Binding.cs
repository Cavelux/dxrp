using System;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Wiring;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( TextBinding ),       "Text" )]
[JsonDerivedType( typeof( AttributeBinding ),  "Attribute" )]
[JsonDerivedType( typeof( EventBinding ),      "Event" )]
[JsonDerivedType( typeof( VisibleBinding ),    "Visible" )]
public abstract record Binding
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
