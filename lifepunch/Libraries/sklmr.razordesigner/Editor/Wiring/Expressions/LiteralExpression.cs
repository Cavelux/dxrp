using System.Text.Json;

namespace Grains.RazorDesigner.Wiring;

public sealed record LiteralExpression : Expression
{
    public JsonElement Value { get; init; }
}
