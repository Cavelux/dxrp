using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldGuide.Tips;

/// <summary>
/// Writes and reads the <c>.tip</c> file format: the exact JSON the s&amp;box editor puts on disk for a
/// <see cref="TipResource"/>. What the Tips Studio's Copy button puts on the clipboard, and what its Editor
/// menu action writes into <c>Assets/tips/</c>, is a file the editor will happily open, edit and re-save.
///
/// THE SHAPE, derived from the shipped assets under <c>Assets/</c> and from <see cref="TipResource"/> itself:
/// every <c>[Property]</c> in declaration order, then the two editor bookkeeping keys. Every trigger field is
/// written whether or not its kind reads it, because that is what a GameResource does (it serializes the
/// object, not the interesting parts of it), and a hand-written file missing those keys would gain them the
/// first time the editor saved it, producing a spurious diff.
///
/// <code>
/// {
///   "Id": "jump",
///   "Text": "Press *Space* to jump.",
///   "TextPad": "",
///   "Icon": "",
///   "Priority": 100,
///   "PrerequisiteTipIds": [],
///   "Completion": { "Kind": "InputAction", "Action": "Jump", ... },
///   "MaxShowSeconds": 0,
///   "Relevance": { "Kind": "Always", ... },
///   "__references": [],
///   "__version": 0
/// }
/// </code>
///
/// Enums go out as their names through hand-written maps rather than a converter, so the on-disk vocabulary
/// is a stated format instead of a by-product of how the enum happens to be spelled today. The round trip
/// (write then read) is asserted headlessly in <c>tools/tips_harness</c>.
/// </summary>
public static class TipStudioJson
{
	/// <summary>Two-space indentation, matching what the editor writes, so a Studio-authored file and an
	/// editor-saved one diff cleanly against each other.</summary>
	private static readonly JsonSerializerOptions WriteOptions = new()
	{
		WriteIndented = true,
	};

	private static readonly JsonSerializerOptions ReadOptions = new()
	{
		PropertyNameCaseInsensitive = false,
	};

	/// <summary>The file name a draft bakes out to, <c>&lt;id&gt;.tip</c>, with anything awkward for a file
	/// name folded to an underscore. A blank id yields null: an unnamed tip has nowhere to land.</summary>
	public static string FileNameFor( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return null;

		var chars = id.Trim().ToCharArray();
		for ( var i = 0; i < chars.Length; i++ )
		{
			var c = chars[i];
			var ok = ( c >= 'a' && c <= 'z' ) || ( c >= 'A' && c <= 'Z' ) || ( c >= '0' && c <= '9' ) || c == '_' || c == '-';
			if ( !ok )
				chars[i] = '_';
		}

		return new string( chars ) + ".tip";
	}

	/// <summary>Serialize a draft as <c>.tip</c> JSON. Never throws: a null draft writes an empty tip.</summary>
	public static string Write( TipStudioDraft draft )
		=> RelaxEscapes( JsonSerializer.Serialize( ToWire( draft ), WriteOptions ) );

	/// <summary>
	/// Put back the characters the default JSON writer escapes but the editor does not. An icon of "✅" is
	/// written to a <c>.tip</c> by the editor as that character; the default encoder emits <c>✅</c>,
	/// which parses to the same string but is not the same FILE, so the first inspector save of a
	/// Studio-written tip would show a diff on a line nobody touched. The same goes for the apostrophes and
	/// angle brackets the default encoder escapes out of HTML caution, which a tip line is full of.
	///
	/// Done as a pass over the finished text rather than by swapping in a relaxed encoder, so the writer
	/// needs no API beyond the JSON serializer a sibling kit already ships. Escapes JSON actually requires
	/// (the quote, the backslash, and the control characters below 0x20) are left exactly as they are, and an
	/// escaped backslash is stepped over as a pair so <c>\\u0041</c> stays literal.
	/// </summary>
	private static string RelaxEscapes( string json )
	{
		if ( string.IsNullOrEmpty( json ) || json.IndexOf( '\\' ) < 0 )
			return json;

		var sb = new System.Text.StringBuilder( json.Length );
		var i = 0;

		while ( i < json.Length )
		{
			var c = json[i];
			if ( c != '\\' || i + 1 >= json.Length )
			{
				sb.Append( c );
				i++;
				continue;
			}

			if ( json[i + 1] == 'u' && i + 5 < json.Length && TryHex( json, i + 2, out var code )
				&& code >= 0x20 && code != '"' && code != '\\' )
			{
				sb.Append( (char)code );
				i += 6;
				continue;
			}

			// Any other escape (including \\) is copied as a PAIR, so its second character can never be read
			// as the start of a new escape.
			sb.Append( c );
			sb.Append( json[i + 1] );
			i += 2;
		}

		return sb.ToString();
	}

	private static bool TryHex( string text, int start, out int value )
	{
		value = 0;
		for ( var i = start; i < start + 4; i++ )
		{
			var c = text[i];
			int digit;
			if ( c >= '0' && c <= '9' )
				digit = c - '0';
			else if ( c >= 'a' && c <= 'f' )
				digit = c - 'a' + 10;
			else if ( c >= 'A' && c <= 'F' )
				digit = c - 'A' + 10;
			else
				return false;

			value = ( value << 4 ) | digit;
		}

		return true;
	}

	/// <summary>Parse <c>.tip</c> JSON back into a draft. Returns null on malformed input rather than
	/// throwing, so a hand-edited file with a stray comma reports a problem instead of taking the panel
	/// down with it.</summary>
	public static TipStudioDraft Read( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) )
			return null;

		try
		{
			var wire = JsonSerializer.Deserialize<TipWire>( json, ReadOptions );
			return wire is null ? null : FromWire( wire );
		}
		catch ( Exception )
		{
			return null;
		}
	}

	// ------------------------------------------------------------------
	// Wire types. Property ORDER here is the on-disk field order.
	// ------------------------------------------------------------------

	private sealed class TipWire
	{
		public string Id { get; set; } = "";
		public string Text { get; set; } = "";
		public string TextPad { get; set; } = "";
		public string Icon { get; set; } = "";
		public int Priority { get; set; }
		public List<string> PrerequisiteTipIds { get; set; } = new();
		public TriggerWire Completion { get; set; } = new();
		public float MaxShowSeconds { get; set; }
		public TriggerWire Relevance { get; set; } = new();

		[JsonPropertyName( "__references" )]
		public List<string> References { get; set; } = new();

		[JsonPropertyName( "__version" )]
		public int Version { get; set; }
	}

	private sealed class TriggerWire
	{
		public string Kind { get; set; } = "Always";
		public string Action { get; set; } = "";
		public string Key { get; set; } = "";
		public string Name { get; set; } = "";
		public float Threshold { get; set; }
		public float Seconds { get; set; }
		public string AnalogSource { get; set; } = "AnalogMove";
		public float Magnitude { get; set; }
		public List<TriggerWire> Children { get; set; } = new();
	}

	private static TipWire ToWire( TipStudioDraft draft )
	{
		draft ??= new TipStudioDraft();

		return new TipWire
		{
			Id = draft.Id ?? "",
			Text = draft.Text ?? "",
			TextPad = draft.TextPad ?? "",
			Icon = draft.Icon ?? "",
			Priority = draft.Priority,
			PrerequisiteTipIds = draft.PrerequisiteTipIds is null
				? new List<string>()
				: new List<string>( draft.PrerequisiteTipIds ),
			Completion = ToWire( draft.Completion ),
			MaxShowSeconds = draft.MaxShowSeconds,
			Relevance = ToWire( draft.Relevance ),
		};
	}

	private static TriggerWire ToWire( TipStudioTrigger spec )
	{
		spec ??= new TipStudioTrigger();

		var wire = new TriggerWire
		{
			Kind = TipStudioTrigger.KindName( spec.Kind ),
			Action = spec.Action ?? "",
			Key = spec.Key ?? "",
			Name = spec.Name ?? "",
			Threshold = spec.Threshold,
			Seconds = spec.Seconds,
			AnalogSource = TipStudioTrigger.SourceName( spec.AnalogSource ),
			Magnitude = spec.Magnitude,
		};

		if ( spec.Children is not null )
			foreach ( var child in spec.Children )
				wire.Children.Add( ToWire( child ) );

		return wire;
	}

	private static TipStudioDraft FromWire( TipWire wire ) => new()
	{
		Id = wire.Id ?? "",
		Text = wire.Text ?? "",
		TextPad = wire.TextPad ?? "",
		Icon = wire.Icon ?? "",
		Priority = wire.Priority,
		PrerequisiteTipIds = wire.PrerequisiteTipIds is null
			? new List<string>()
			: new List<string>( wire.PrerequisiteTipIds ),
		Completion = FromWire( wire.Completion ),
		MaxShowSeconds = wire.MaxShowSeconds,
		Relevance = FromWire( wire.Relevance ),
	};

	private static TipStudioTrigger FromWire( TriggerWire wire )
	{
		if ( wire is null )
			return new TipStudioTrigger();

		var spec = new TipStudioTrigger
		{
			Kind = TipStudioTrigger.ParseKind( wire.Kind ),
			Action = wire.Action ?? "",
			Key = wire.Key ?? "",
			Name = wire.Name ?? "",
			Threshold = wire.Threshold,
			Seconds = wire.Seconds,
			AnalogSource = TipStudioTrigger.ParseSource( wire.AnalogSource ),
			Magnitude = wire.Magnitude,
		};

		if ( wire.Children is not null )
			foreach ( var child in wire.Children )
				spec.Children.Add( FromWire( child ) );

		return spec;
	}
}
