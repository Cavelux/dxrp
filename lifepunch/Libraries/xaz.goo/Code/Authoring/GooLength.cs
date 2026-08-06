using System;
using System.Text.Json;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>Serializable Length surrogate required because the engine drops Length's public fields.</summary>
public struct GooLength : IJsonConvert, IEquatable<GooLength>
{
	public float Value { get; set; }
	public LengthUnit Unit { get; set; }

	public static implicit operator Length( GooLength l ) => new() { Value = l.Value, Unit = l.Unit };
	public static implicit operator GooLength( Length l ) => new() { Value = l.Value, Unit = l.Unit };

	public static object JsonRead( ref Utf8JsonReader reader, Type typeToConvert )
	{
		// Accept hand-authored values such as "400px", "50%", and "auto".
		if ( reader.TokenType == JsonTokenType.String )
			return (GooLength)(Length.Parse( reader.GetString() ) ?? default);

		var result = new GooLength();
		if ( reader.TokenType == JsonTokenType.StartObject )
		{
			// The canonical object form also lets legacy empty objects fall through as Auto.
			while ( reader.Read() && reader.TokenType != JsonTokenType.EndObject )
			{
				var name = reader.GetString();
				reader.Read();
				if ( name == "Value" ) result.Value = reader.GetSingle();
				else if ( name == "Unit" && Enum.TryParse<LengthUnit>( reader.GetString(), out var u ) ) result.Unit = u;
				else reader.Skip();
			}
		}
		return result;
	}

	public static void JsonWrite( object value, Utf8JsonWriter writer )
	{
		var l = (GooLength)value;
		writer.WriteStartObject();
		writer.WriteNumber( "Value", l.Value );
		writer.WriteString( "Unit", l.Unit.ToString() );
		writer.WriteEndObject();
	}

	public readonly bool Equals( GooLength other ) => Value == other.Value && Unit == other.Unit;
	public readonly override bool Equals( object obj ) => obj is GooLength o && Equals( o );
	public readonly override int GetHashCode() => HashCode.Combine( Value, Unit );
	public readonly override string ToString() => ((Length)this).ToString();
}

/// <summary>Outset shadow that round-trips scene JSON.</summary>
public struct GooShadow : IJsonConvert, IEquatable<GooShadow>
{
	public float OffsetX { get; set; }
	public float OffsetY { get; set; }
	public float Blur { get; set; }
	public float Spread { get; set; }
	public Color Color { get; set; }

	public static implicit operator Shadow( GooShadow s ) =>
		new() { OffsetX = s.OffsetX, OffsetY = s.OffsetY, Blur = s.Blur, Spread = s.Spread, Color = s.Color };

	public static implicit operator GooShadow( Shadow s ) =>
		new() { OffsetX = s.OffsetX, OffsetY = s.OffsetY, Blur = s.Blur, Spread = s.Spread, Color = s.Color };

	// The blob facades take a Shadows stack; the inspector authors one shadow, so lift it here.
	// Two user-defined conversions cannot chain implicitly, hence the direct operator.
	public static implicit operator Goo.Shadows( GooShadow s ) => new( (Shadow)s );

	public static object JsonRead( ref Utf8JsonReader reader, Type typeToConvert )
	{
		var result = new GooShadow();
		if ( reader.TokenType == JsonTokenType.StartObject )
		{
			while ( reader.Read() && reader.TokenType != JsonTokenType.EndObject )
			{
				var name = reader.GetString();
				reader.Read();
				switch ( name )
				{
					case "OffsetX": result.OffsetX = reader.GetSingle(); break;
					case "OffsetY": result.OffsetY = reader.GetSingle(); break;
					case "Blur": result.Blur = reader.GetSingle(); break;
					case "Spread": result.Spread = reader.GetSingle(); break;
					case "Color": result.Color = Color.Parse( reader.GetString() ) ?? default; break;
					default: reader.Skip(); break;
				}
			}
		}
		return result;
	}

	public static void JsonWrite( object value, Utf8JsonWriter writer )
	{
		var s = (GooShadow)value;
		writer.WriteStartObject();
		writer.WriteNumber( "OffsetX", s.OffsetX );
		writer.WriteNumber( "OffsetY", s.OffsetY );
		writer.WriteNumber( "Blur", s.Blur );
		writer.WriteNumber( "Spread", s.Spread );
		writer.WriteString( "Color", s.Color.ToString() );
		writer.WriteEndObject();
	}

	public readonly bool Equals( GooShadow other ) =>
		OffsetX == other.OffsetX && OffsetY == other.OffsetY && Blur == other.Blur
		&& Spread == other.Spread && Color == other.Color;
	public readonly override bool Equals( object obj ) => obj is GooShadow o && Equals( o );
	public readonly override int GetHashCode() => HashCode.Combine( OffsetX, OffsetY, Blur, Spread, Color );
}
