using System.Collections.Generic;
using System.Text.Json.Serialization;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.Projection.Tests;

public sealed class CSharpFixture
{
    [JsonPropertyName( "goldenFormatVersion" )]
    public int GoldenFormatVersion { get; set; }

    [JsonPropertyName( "name" )]
    public string Name { get; set; } = "";

    [JsonPropertyName( "namespaceFallback" )]
    public string NamespaceFallback { get; set; } = "Grains";

    [JsonPropertyName( "classNameFallback" )]
    public string ClassNameFallback { get; set; } = "Test";

    [JsonPropertyName( "wiring" )]
    public WiringEnvelope Wiring { get; set; } = WiringEnvelope.Empty;

    [JsonPropertyName( "documentHasAnyBindings" )]
    public bool DocumentHasAnyBindings { get; set; }

    [JsonPropertyName( "expected" )]
    public CSharpExpectedDto Expected { get; set; }   // null = "not yet authored"
}

public sealed class CSharpExpectedDto
{
    [JsonPropertyName( "ops" )]
    public List<CSharpOpDto> Ops { get; set; } = new();
}
