namespace Grains.RazorDesigner.Wiring;

public sealed record InlineAction : Action
{
    public string Code { get; init; } = "";
}
