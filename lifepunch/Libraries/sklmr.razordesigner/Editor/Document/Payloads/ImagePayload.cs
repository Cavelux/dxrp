using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record ImagePayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.Image;

    // Image asset path (asset.RelativePath). Overrides Payload.Source (neutral default "").
    public override string Source { get; init; } = "";
}
