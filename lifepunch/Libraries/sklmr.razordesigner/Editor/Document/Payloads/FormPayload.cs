using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record FormPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.Form;
}
