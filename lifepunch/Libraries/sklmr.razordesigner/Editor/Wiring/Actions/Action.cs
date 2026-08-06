using System;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Wiring;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( SetAction ),              "Set" )]
[JsonDerivedType( typeof( CallAction ),             "Call" )]
[JsonDerivedType( typeof( IfAction ),               "If" )]
[JsonDerivedType( typeof( StateHasChangedAction ),  "StateHasChanged" )]
[JsonDerivedType( typeof( LogAction ),              "Log" )]
[JsonDerivedType( typeof( ReturnAction ),           "Return" )]
[JsonDerivedType( typeof( InlineAction ),           "Inline" )]
public abstract record Action
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
