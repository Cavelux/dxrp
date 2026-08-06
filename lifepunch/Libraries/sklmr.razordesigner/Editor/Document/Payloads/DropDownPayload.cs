using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record DropDownPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.DropDown;
}
