using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record CheckboxPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.Checkbox;

    // Checkbox label text. Overrides Payload.Content (neutral default "").
    public override string Content { get; init; } = "";

    public override Length CheckboxSize { get; init; } = Length.Px( 16 );
}
