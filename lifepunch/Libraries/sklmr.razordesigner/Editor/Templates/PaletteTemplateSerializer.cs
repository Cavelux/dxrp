using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Grains.RazorDesigner.Document;
using Sandbox;
using Sandbox.UI;
using Length = Grains.RazorDesigner.Document.Length;

namespace Grains.RazorDesigner.Templates;

public sealed class PaletteTemplateException : Exception
{
	public PaletteTemplateException( string message ) : base( message ) { }
	public PaletteTemplateException( string message, Exception inner ) : base( message, inner ) { }
}

public static class PaletteTemplateSerializer
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const int CurrentVersion = 1;

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};

	private static FieldDescriptor[] _fields;
	private static FieldDescriptor[] Fields => _fields ??= BuildFieldDescriptors();

	public static string Serialize( PaletteTemplate t )
	{
		if ( t is null ) throw new ArgumentNullException( nameof( t ) );

		var doc = new JsonObject
		{
			["version"] = CurrentVersion,
			["name"] = t.Name,
			["icon"] = t.IconName,
			["wrappedInContainer"] = t.WrappedInContainer,
			["roots"] = WriteRecordList( t.Roots ),
		};
		return doc.ToJsonString( JsonOpts );
	}

	public static PaletteTemplate Deserialize( string json, string filePath )
	{
		if ( string.IsNullOrEmpty( json ) )
			throw new PaletteTemplateException( "empty JSON" );

		JsonNode parsed;
		try
		{
			parsed = JsonNode.Parse( json );
		}
		catch ( JsonException ex )
		{
			throw new PaletteTemplateException( $"malformed JSON: {ex.Message}", ex );
		}
		if ( parsed is not JsonObject root )
			throw new PaletteTemplateException( "deserialised to null" );

		int version = TryReadInt( root["version"] );
		if ( version != CurrentVersion )
			throw new PaletteTemplateException( $"unsupported schema version {version} (expected {CurrentVersion})" );

		var name = TryReadString( root["name"] );
		if ( string.IsNullOrWhiteSpace( name ) )
			throw new PaletteTemplateException( "name field missing or empty" );

		return new PaletteTemplate(
			Name: name,
			IconName: TryReadString( root["icon"] ) ?? "",
			WrappedInContainer: TryReadBool( root["wrappedInContainer"] ),
			Roots: ReadRecordList( root["roots"] as JsonArray ),
			FilePath: filePath );
	}

	// ---------- Walker (records) ----------

	private static JsonArray WriteRecordList( IReadOnlyList<ControlRecord> records )
	{
		var arr = new JsonArray();
		if ( records is null ) return arr;
		foreach ( var r in records )
			arr.Add( WriteRecord( r ) );
		return arr;
	}

	private static JsonObject WriteRecord( ControlRecord r )
	{
		var o = new JsonObject();
		o["type"] = r.Type.ToString();
		foreach ( var f in Fields )
			o[f.JsonName] = f.Write( f.Get( r ) );
		if ( r.IsSlot )
		{
			o["isSlot"] = true;
			o["slotName"] = r.SlotName ?? "";
		}
		if ( r.StateRules is { Count: > 0 } )
			o["stateRules"] = WriteStateRuleList( r.StateRules );
		o["children"] = WriteRecordList( r.Children );
		return o;
	}

	private static List<ControlRecord> ReadRecordList( JsonArray arr )
	{
		var list = new List<ControlRecord>();
		if ( arr is null ) return list;
		foreach ( var node in arr )
		{
			var rec = ReadRecord( node as JsonObject );
			if ( rec is null ) continue; // unknown type — already warned
			list.Add( rec );
		}
		return list;
	}

	private static ControlRecord ReadRecord( JsonObject o )
	{
		if ( o is null ) return null;
		var typeStr = TryReadString( o["type"] ) ?? "";
		if ( !Enum.TryParse<ControlType>( typeStr, ignoreCase: false, out var type ) )
		{
			Log.Warning( $"{LogPrefix} PaletteTemplateSerializer: unknown ControlType \"{typeStr}\", skipping node" );
			return null;
		}

		var rec = new ControlRecord { Type = type };

		foreach ( var f in Fields )
		{
			var node = o[f.JsonName];
			if ( node is null ) continue; // missing → keep ControlRecord property default
			var value = f.Read( node );
			if ( value is not null ) f.Set( rec, value );
		}

		if ( o["isSlot"] is JsonNode isSlotNode && TryReadBool( isSlotNode ) )
		{
			rec.IsSlot = true;
			rec.SlotName = TryReadString( o["slotName"] ) ?? "";
		}

		RunMigrations( rec );

		if ( o["stateRules"] is JsonArray stateRulesArr )
		{
			foreach ( var rule in ReadStateRuleList( stateRulesArr ) )
				rec.StateRules.Add( rule );
		}

		if ( o["children"] is JsonArray children )
		{
			foreach ( var child in ReadRecordList( children ) )
				rec.Children.Add( child );
		}

		return rec;
	}


	private static JsonArray WriteStateRuleList( IReadOnlyList<StateRule> rules )
	{
		var arr = new JsonArray();
		if ( rules is null ) return arr;
		foreach ( var rule in rules )
			arr.Add( WriteStateRule( rule ) );
		return arr;
	}

	private static JsonObject WriteStateRule( StateRule rule )
	{
		var o = new JsonObject();
		o["state"] = rule.State.ToString();
		// NthChild extras — omit when at default (Literal / 1) to match IR wire format economy.
		if ( rule.State == PseudoKind.NthChild )
		{
			o["nthChildMode"] = rule.NthChildMode.ToString();
			o["nthChildArg"]  = rule.NthChildArg;
		}
		o["delta"] = WriteAppearance( rule.Delta );
		return o;
	}

	private static List<StateRule> ReadStateRuleList( JsonArray arr )
	{
		var list = new List<StateRule>();
		if ( arr is null ) return list;
		foreach ( var node in arr )
		{
			if ( node is not JsonObject o ) continue;
			var stateStr = TryReadString( o["state"] ) ?? "";
			if ( !Enum.TryParse<PseudoKind>( stateStr, ignoreCase: true, out var state ) )
			{
				Log.Warning( $"{LogPrefix} PaletteTemplateSerializer: unknown PseudoKind \"{stateStr}\", skipping state rule" );
				continue;
			}
			var mode = NthChildMode.Literal;
			var arg  = 1;
			if ( o["nthChildMode"] is JsonNode modeNode )
			{
				var modeStr = TryReadString( modeNode ) ?? "";
				if ( Enum.TryParse<NthChildMode>( modeStr, ignoreCase: true, out var parsedMode ) )
					mode = parsedMode;
			}
			if ( o["nthChildArg"] is JsonNode argNode )
				arg = TryReadInt( argNode );
			var delta = o["delta"] is JsonObject deltaObj ? ReadAppearance( deltaObj ) : Appearance.Default;
			list.Add( new StateRule { State = state, NthChildMode = mode, NthChildArg = arg, Delta = delta } );
		}
		return list;
	}

	private static JsonNode WriteAppearance( Appearance a )
	{
		var json = JsonSerializer.Serialize( a, Grains.RazorDesigner.Serialization.IR.DesignerIRJson.Options );
		return JsonNode.Parse( json );
	}

	private static Appearance ReadAppearance( JsonObject o )
	{
		if ( o is null ) return Appearance.Default;
		return JsonSerializer.Deserialize<Appearance>(
			o.ToJsonString(),
			Grains.RazorDesigner.Serialization.IR.DesignerIRJson.Options );
	}

	// ---------- Migrations ----------

	private static void RunMigrations( ControlRecord rec )
	{
		// IconPanel glyph used to live in Content (pre-grd-zcq); route into IconName when empty.
		if ( rec.Type == ControlType.IconPanel && string.IsNullOrEmpty( rec.IconName ) )
		{
			rec.IconName = rec.Content ?? "";
		}
		if ( rec.Type == ControlType.IconPanel )
			rec.Content = "";

		// TextEntry placeholder used to live in Content (pre-grd-3oa); route into Placeholder.
		if ( rec.Type == ControlType.TextEntry && string.IsNullOrEmpty( rec.Placeholder ) )
		{
			rec.Placeholder = rec.Content ?? "";
		}
		// TextEntry never carries Content in v3+. Same belt-and-braces shape as IconPanel.
		if ( rec.Type == ControlType.TextEntry )
			rec.Content = "";

		// FontWeight missing in pre-typography templates → default 400.
		if ( rec.FontWeight == 0 ) rec.FontWeight = 400;
		// Opacity missing in pre-Tier-2 templates → default 1 (fully opaque).
		if ( rec.Opacity == 0f ) rec.Opacity = 1f;
	}

	// ---------- Static schema build ----------

	private static FieldDescriptor[] BuildFieldDescriptors()
	{
		var props = typeof( ControlRecord )
			.GetProperties( BindingFlags.Public | BindingFlags.Instance )
			.Where( p => p.CanRead && p.CanWrite )
			.Where( p => p.GetCustomAttribute<HideAttribute>() == null )
			.Where( p => !typeof( Panel ).IsAssignableFrom( p.PropertyType ) )
			.OrderBy( p => p.MetadataToken )
			.ToArray();

		var list = new List<FieldDescriptor>( props.Length );
		foreach ( var p in props )
		{
			if ( !TryResolveConverter( p.PropertyType, out var conv ) )
			{
				throw new InvalidOperationException(
					$"PaletteTemplateSerializer: no converter for ControlRecord.{p.Name} (type {p.PropertyType.FullName}). " +
					$"Register one in TryResolveConverter." );
			}

			var prop = p; // closure capture
			var jsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
				?? JsonNamingPolicy.CamelCase.ConvertName( p.Name );

			list.Add( new FieldDescriptor
			{
				JsonName = jsonName,
				Get = rec => prop.GetValue( rec ),
				Set = ( rec, val ) => prop.SetValue( rec, val ),
				Write = conv.Write,
				Read = conv.Read,
			} );
		}
		Log.Info( $"{LogPrefix} PaletteTemplateSerializer: {list.Count} ControlRecord fields mirrored." );
		return list.ToArray();
	}

	// ---------- Converters ----------

	private readonly struct Converter
	{
		public Converter( Func<object, JsonNode> write, Func<JsonNode, object> read )
		{
			Write = write;
			Read = read;
		}
		public Func<object, JsonNode> Write { get; }
		public Func<JsonNode, object> Read { get; }
	}

	private static bool TryResolveConverter( Type t, out Converter conv )
	{
		if ( t == typeof( string ) ) { conv = ConvString; return true; }
		if ( t == typeof( int ) ) { conv = ConvInt; return true; }
		if ( t == typeof( float ) ) { conv = ConvFloat; return true; }
		if ( t == typeof( bool ) ) { conv = ConvBool; return true; }
		if ( t == typeof( Length ) ) { conv = ConvLength; return true; }
		if ( t == typeof( Edges ) ) { conv = ConvEdges; return true; }
		if ( t == typeof( Color ) ) { conv = ConvColor; return true; }
		if ( t.IsEnum ) { conv = MakeEnumConverter( t ); return true; }
		conv = default;
		return false;
	}

	private static readonly Converter ConvString = new(
		write: v => JsonValue.Create( (string)v ?? "" ),
		read: n => TryReadString( n ) );

	private static readonly Converter ConvInt = new(
		write: v => JsonValue.Create( (int)v ),
		read: n => (object)TryReadInt( n ) );

	private static readonly Converter ConvFloat = new(
		write: v => JsonValue.Create( (float)v ),
		read: n => (object)TryReadFloat( n ) );

	private static readonly Converter ConvBool = new(
		write: v => JsonValue.Create( (bool)v ),
		read: n => (object)TryReadBool( n ) );

	private static readonly Converter ConvLength = new(
		write: v => JsonValue.Create( ((Length)v).ToCss() ),
		read: n => Length.TryParse( TryReadString( n ) ?? "", out var l ) ? (object)l : null );

	private static readonly Converter ConvEdges = new(
		write: v =>
		{
			var e = (Edges)v;
			if ( e.IsUniform ) return JsonValue.Create( e.Top.ToCss() );
			return new JsonObject
			{
				["top"]    = e.Top.ToCss(),
				["right"]  = e.Right.ToCss(),
				["bottom"] = e.Bottom.ToCss(),
				["left"]   = e.Left.ToCss(),
			};
		},
		read: n =>
		{
			if ( n is null ) return null;
			if ( n is JsonObject o )
			{
				var top    = ParseEdgeSide( o["top"] );
				var right  = ParseEdgeSide( o["right"] );
				var bottom = ParseEdgeSide( o["bottom"] );
				var left   = ParseEdgeSide( o["left"] );
				return (object)new Edges( top, right, bottom, left );
			}
			var s = TryReadString( n ) ?? "";
			return Length.TryParse( s, out var len ) ? (object)Edges.Uniform( len ) : null;
		} );

	private static Length ParseEdgeSide( JsonNode n )
	{
		if ( n is null ) return Length.Px( 0 );
		var s = TryReadString( n ) ?? "";
		return Length.TryParse( s, out var len ) ? len : Length.Px( 0 );
	}

	private static readonly Converter ConvColor = new(
		write: v => JsonValue.Create( ((Color)v).Hex ),
		read: n =>
		{
			var s = TryReadString( n );
			if ( string.IsNullOrEmpty( s ) ) return null;
			var parsed = Color.Parse( s );
			return parsed.HasValue ? (object)parsed.Value : null;
		} );

	private static Converter MakeEnumConverter( Type enumType )
	{
		return new Converter(
			write: v => JsonValue.Create( v?.ToString() ?? "" ),
			read: n =>
			{
				var s = TryReadString( n );
				if ( string.IsNullOrEmpty( s ) ) return null;
				return Enum.TryParse( enumType, s, ignoreCase: true, out var v ) ? v : null;
			} );
	}


	private static string TryReadString( JsonNode n )
	{
		if ( n is null ) return null;
		try { return n.GetValue<string>(); } catch { return n.ToString(); }
	}

	private static int TryReadInt( JsonNode n )
	{
		if ( n is null ) return 0;
		try { return n.GetValue<int>(); }
		catch
		{
			try { return (int)n.GetValue<double>(); }
			catch { return int.TryParse( n.ToString(), out var v ) ? v : 0; }
		}
	}

	private static float TryReadFloat( JsonNode n )
	{
		if ( n is null ) return 0f;
		try { return n.GetValue<float>(); }
		catch
		{
			try { return (float)n.GetValue<double>(); }
			catch { return float.TryParse( n.ToString(), out var v ) ? v : 0f; }
		}
	}

	private static bool TryReadBool( JsonNode n )
	{
		if ( n is null ) return false;
		try { return n.GetValue<bool>(); } catch { return false; }
	}

	private sealed class FieldDescriptor
	{
		public string JsonName;
		public Func<ControlRecord, object> Get;
		public Action<ControlRecord, object> Set;
		public Func<object, JsonNode> Write;
		public Func<JsonNode, object> Read;
	}
}
