
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sandbox;

namespace Grains.RazorDesigner.Serialization.IR;

public static class DesignerIRJson
{
	public static readonly JsonSerializerOptions Options = Build();

	private static JsonSerializerOptions Build()
	{
		var options = new JsonSerializerOptions
		{
			WriteIndented                = true,
			PropertyNamingPolicy         = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
			Encoder                      = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

			TypeInfoResolver = new DefaultJsonTypeInfoResolver
			{
				Modifiers =
				{
					static typeInfo =>
					{
						if ( typeInfo.Kind != JsonTypeInfoKind.Object ) return;
						var sorted = typeInfo.Properties
							.OrderBy( p => p.Name, StringComparer.Ordinal )
							.ToList();
						typeInfo.Properties.Clear();
						foreach ( var p in sorted )
							typeInfo.Properties.Add( p );
					}
				}
			},
		};

		options.Converters.Add( new JsonStringEnumConverter() );

		options.Converters.Add( new ColorJsonConverter() );

		return options;
	}
}

public sealed class ColorJsonConverter : JsonConverter<Color>
{
	public override Color Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		var hex = reader.GetString();
		if ( string.IsNullOrEmpty( hex ) )
		{
			Log.Warning( "[Grains.RazorDesigner] ColorJsonConverter.Read: empty string, defaulting to Color.White" );
			return Color.White;
		}

		var parsed = Color.Parse( hex );
		if ( parsed is null )
		{
			Log.Warning( $"[Grains.RazorDesigner] ColorJsonConverter.Read: could not parse \"{hex}\", defaulting to Color.White" );
			return Color.White;
		}

		return parsed.Value;
	}

	public override void Write( Utf8JsonWriter writer, Color value, JsonSerializerOptions options )
	{
		writer.WriteStringValue( value.Hex );
	}
}
