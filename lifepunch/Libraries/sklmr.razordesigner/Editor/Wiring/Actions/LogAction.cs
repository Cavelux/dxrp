namespace Grains.RazorDesigner.Wiring;

public sealed record LogAction : Action
{
    public LogLevel Level { get; init; } = LogLevel.Info;
    public Expression Message { get; init; }
}
