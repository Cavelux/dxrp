using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grains.RazorDesigner.Serialization.IR;

public sealed record IRDocumentEnvelope
{
	// Shared empty collections — avoids allocations for the common case of no metadata.
	private static readonly IReadOnlyDictionary<string, object> _emptyMetadata = new Dictionary<string, object>();

	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public int SchemaVersion { get; init; } = SchemaMigrations.Current;

	// Reserved theme binding hook. Always { "kind": "none" } in v1.
	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public ThemeRef ThemeRef { get; init; } = ThemeRef.None;

	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public ViewportProfile ViewportProfile { get; init; } = ViewportProfile.Auto;

	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public Grains.RazorDesigner.Wiring.WiringEnvelope Wiring { get; init; } = Grains.RazorDesigner.Wiring.WiringEnvelope.Empty;

	// Reserved i18n hook. Always { "kind": "none" } in v1.
	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public Localization Localization { get; init; } = Localization.None;

	// Reserved arbitrary metadata dict. Always {} in v1.
	[JsonIgnore( Condition = JsonIgnoreCondition.Never )]
	public IReadOnlyDictionary<string, object> Metadata { get; init; } = _emptyMetadata;

	public IRNodeEnvelope Root { get; init; }

	// Forward-compat: unknown top-level keys from future-version files ride here across read/write.
	[JsonExtensionData]
	public IDictionary<string, JsonElement> Extra { get; init; }
}

public readonly record struct ThemeRef( string Kind )
{
	public static readonly ThemeRef None = new( "none" );
}

public readonly record struct Localization( string Kind )
{
	public static readonly Localization None = new( "none" );
}

public readonly record struct ViewportProfile( string Width, string Height )
{
	public static readonly ViewportProfile Auto = new( "auto", "auto" );
}
