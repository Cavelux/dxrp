namespace Grains.RazorDesigner.Wiring;

public sealed record MethodParameter
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "object";
}
