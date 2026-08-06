using System;
using System.Collections.Generic;
using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>Marks an authoring property as eligible for an optional inspector token binding.</summary>
[AttributeUsage( AttributeTargets.Property )]
public sealed class GooTokenAttribute : Attribute { }

public enum GooTokenKind
{
	Color,
	Length,
	Float,
	Integer,
	Boolean,
	String,
	Texture,
}

/// <summary>One inspector-authored token entry. Only the value matching Kind is shown.</summary>
public sealed class GooTokenEntry
{
	[Property, Order( 0 )] public string Name { get; set; } = "Token";
	[Property, Order( 1 )] public GooTokenKind Kind { get; set; }

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.Color )]
	public Color ColorValue { get; set; } = Color.White;

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.Length )]
	public GooLength LengthValue { get; set; }

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.Float )]
	public float FloatValue { get; set; }

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.Integer )]
	public int IntegerValue { get; set; }

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.Boolean )]
	public bool BooleanValue { get; set; }

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.String )]
	public string StringValue { get; set; } = "";

	[Property, Order( 2 ), Title( "Value" ), ShowIf( nameof( Kind ), GooTokenKind.Texture )]
	public Texture? TextureValue { get; set; }

	internal bool TryGetValue( out object value )
	{
		value = Kind switch
		{
			GooTokenKind.Color => ColorValue,
			GooTokenKind.Length => (Length)LengthValue,
			GooTokenKind.Float => FloatValue,
			GooTokenKind.Integer => IntegerValue,
			GooTokenKind.Boolean => BooleanValue,
			GooTokenKind.String => StringValue,
			GooTokenKind.Texture when TextureValue is not null => TextureValue,
			_ => null!,
		};
		return value is not null;
	}

}

/// <summary>Defines tokens for this authored Goo subtree; nested scopes override matching names.</summary>
[Title( "Goo Token Scope" )]
[Category( "Goo" )]
[Icon( "data_object" )]
public sealed class GooTokenScope : Component
{
	[Property]
	public List<GooTokenEntry> Tokens { get; set; } = new();

	internal void Apply( Action body )
	{
		var values = new Dictionary<string, object>( StringComparer.Ordinal );
		foreach ( var token in Tokens )
		{
			if ( token is null || string.IsNullOrWhiteSpace( token.Name ) || !token.TryGetValue( out var value ) )
				continue;
			values[token.Name.Trim()] = value;
		}

		Goo.Tokens.Scope( values, () =>
		{
			body();
			return true;
		} );
	}
}
