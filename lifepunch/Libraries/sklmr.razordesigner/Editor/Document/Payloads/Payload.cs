using System.Text.Json.Serialization;
using Grains.RazorDesigner.Projection; // IPayload

namespace Grains.RazorDesigner.Document;

[JsonPolymorphic( TypeDiscriminatorPropertyName = "$type" )]
[JsonDerivedType( typeof( PanelPayload ),        "PanelPayload" )]
[JsonDerivedType( typeof( SplitContainerPayload ), "SplitContainerPayload" )]
[JsonDerivedType( typeof( LabelPayload ),        "LabelPayload" )]
[JsonDerivedType( typeof( ImagePayload ),        "ImagePayload" )]
[JsonDerivedType( typeof( IconPanelPayload ),    "IconPanelPayload" )]
[JsonDerivedType( typeof( ButtonPayload ),       "ButtonPayload" )]
[JsonDerivedType( typeof( ButtonGroupPayload ),  "ButtonGroupPayload" )]
[JsonDerivedType( typeof( CheckboxPayload ),     "CheckboxPayload" )]
[JsonDerivedType( typeof( TextEntryPayload ),    "TextEntryPayload" )]
[JsonDerivedType( typeof( DropDownPayload ),     "DropDownPayload" )]
[JsonDerivedType( typeof( FormPayload ),         "FormPayload" )]
[JsonDerivedType( typeof( FieldPayload ),        "FieldPayload" )]
[JsonDerivedType( typeof( FieldControlPayload ), "FieldControlPayload" )]
public abstract record Payload : IPayload
{
    [JsonIgnore]
    public abstract ControlType Kind { get; }


    // Label/Button text; Checkbox label.
    public virtual string Content { get; init; } = "";

    // TextEntry greyed hint text shown when the field is empty.
    public virtual string Placeholder { get; init; } = "";

    // Image src= path.
    public virtual string Source { get; init; } = "";

    // IconPanel glyph name.
    public virtual string IconName { get; init; } = "";

    public virtual Length CheckboxSize { get; init; } = Length.Px( 16 );
}
