using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record LabelPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.Label;

    // Text content of the label. Overrides Payload.Content (neutral default "").
    public override string Content { get; init; } = "";
}
