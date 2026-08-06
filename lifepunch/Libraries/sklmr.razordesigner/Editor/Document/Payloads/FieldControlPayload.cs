using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record FieldControlPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.FieldControl;
}
