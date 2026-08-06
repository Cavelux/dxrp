using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record TextEntryPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.TextEntry;

    public override string Placeholder { get; init; } = "";
}
