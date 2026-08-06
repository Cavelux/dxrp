using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record SplitContainerPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.SplitContainer;
}
