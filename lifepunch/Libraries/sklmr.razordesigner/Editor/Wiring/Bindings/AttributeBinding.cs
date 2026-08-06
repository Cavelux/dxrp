namespace Grains.RazorDesigner.Wiring;

public sealed record AttributeBinding : Binding
{
    public string Attribute { get; init; } = "";
    public AttributeMode Mode { get; init; } = AttributeMode.Replace;
    public Expression Expression { get; init; }
}
