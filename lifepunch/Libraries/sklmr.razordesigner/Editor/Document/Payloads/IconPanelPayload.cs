using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Document;

public sealed record IconPanelPayload : Payload
{
    [JsonIgnore]
    public override ControlType Kind => ControlType.IconPanel;

    public override string IconName { get; init; } = "";
}
