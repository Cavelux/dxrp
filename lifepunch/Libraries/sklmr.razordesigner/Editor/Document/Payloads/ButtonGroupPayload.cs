using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record ButtonGroupPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.ButtonGroup;
}
