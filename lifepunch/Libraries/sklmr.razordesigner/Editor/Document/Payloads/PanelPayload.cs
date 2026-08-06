using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record PanelPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.Panel;
}
