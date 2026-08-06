using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record ButtonPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.Button;

    // Button label text. Overrides Payload.Content (neutral default "").
    public override string Content { get; init; } = "";
}
