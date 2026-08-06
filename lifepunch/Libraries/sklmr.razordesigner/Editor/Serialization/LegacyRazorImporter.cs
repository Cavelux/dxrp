using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Validation;
using Sandbox;

namespace Grains.RazorDesigner.Serialization;

public static class LegacyRazorImporter
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const int SchemaVersion = 3;

	private static readonly Regex SchemaMarker = new(
		@"@\*\s*Grains\.RazorDesigner\s+schema=(?<v>\d+)\s*\*@",
		RegexOptions.Compiled );

	// Built lazily on first call. Inverse of ContractScanner.Table.Get(t).LibraryTag.
	private static Dictionary<string, ControlType> _tagToType;

	private static Dictionary<string, ControlType> TagToType
	{
		get
		{
			if ( _tagToType is not null ) return _tagToType;
			var map = new Dictionary<string, ControlType>( StringComparer.OrdinalIgnoreCase );
			foreach ( ControlType t in Enum.GetValues( typeof( ControlType ) ) )
			{
				var tag = Grains.RazorDesigner.Contracts.ContractScanner.Table.Get( t ).LibraryTag;
				if ( !string.IsNullOrEmpty( tag ) ) map[tag] = t;
			}
			_tagToType = map;
			return map;
		}
	}

	public static (DesignerDocument Document, List<ValidationDiagnostic> Diagnostics) Import( string razorPath )
	{
		var diags = new List<ValidationDiagnostic>();

		if ( string.IsNullOrEmpty( razorPath ) )
		{
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Error, "legacy-import-failed", "razorPath is empty" ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: razorPath is empty" );
			return (null, diags);
		}

		if ( !File.Exists( razorPath ) )
		{
			var msg = $"file does not exist: {razorPath}";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Error, "legacy-import-failed", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
			return (null, diags);
		}

		string razorText;
		try
		{
			razorText = File.ReadAllText( razorPath );
		}
		catch ( IOException ex )
		{
			var msg = $"cannot read .razor: {ex.Message}";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Error, "legacy-import-failed", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
			return (null, diags);
		}

		// Schema gate.
		var match = SchemaMarker.Match( razorText );
		if ( !match.Success )
		{
			var msg = $"not a Razor Designer file (missing '@* Grains.RazorDesigner schema=N *@' marker)";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Error, "legacy-import-failed", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
			return (null, diags);
		}
		var fileSchema = int.Parse( match.Groups["v"].Value, CultureInfo.InvariantCulture );
		if ( fileSchema > SchemaVersion )
		{
			var msg = $"newer schema version (file={fileSchema} > designer={SchemaVersion}); please update Designer";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Error, "legacy-import-failed", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
			return (null, diags);
		}

		// Locate <root ...>.
		var rootOpen = FindRootOpen( razorText );
		if ( rootOpen < 0 )
		{
			var msg = "markup contains no <root> element";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Error, "legacy-import-failed", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
			return (null, diags);
		}

		var fresh = new DesignerDocument();

		var cursor = rootOpen;
		SkipPastTagOpen( razorText, ref cursor );
		ParseChildren( razorText, ref cursor, fresh.RootRecord, diags, "root" );

		var nameMap = new Dictionary<string, ControlRecord>( StringComparer.Ordinal );
		nameMap[fresh.RootRecord.ClassName] = fresh.RootRecord;
		foreach ( var r in fresh.WalkAll() )
		{
			if ( !string.IsNullOrEmpty( r.ClassName ) )
				nameMap[r.ClassName] = r;
		}

		// Companion .razor.scss — same path with `.scss` appended (matches OnSave).
		var scssPath = razorPath + ".scss";
		if ( !File.Exists( scssPath ) )
		{
			var msg = $"companion stylesheet not found at {scssPath}; loaded markup only (records keep type defaults)";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
		}
		else
		{
			string scssText;
			try
			{
				scssText = File.ReadAllText( scssPath );
				ParseScssBlocks( scssText, ( sel, body ) => ProcessScssRule( sel, body, fresh.RootRecord, nameMap, diags ) );
			}
			catch ( IOException ex )
			{
				var msg = $"cannot read .razor.scss: {ex.Message}; loaded markup only";
				diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
				Log.Warning( $"{LogPrefix} LegacyRazorImporter.Import: {msg}" );
			}
		}

		Log.Info( $"{LogPrefix} LegacyRazorImporter.Import: parsed {razorPath} (schema={fileSchema}; {diags.Count} diagnostic(s))" );
		return (fresh, diags);
	}

	// --- DescribeLoss -------------------------------------------------------

	public static IReadOnlyList<string> DescribeLoss( DesignerDocument imported, DesignerDocument current )
	{
		var lines = new List<string>();

		if ( imported == null )
		{
			lines.Add( "imported document is null — re-import produced no document" );
			return lines;
		}
		if ( current == null )
		{
			lines.Add( "current document is null — nothing to compare against" );
			return lines;
		}

		// Collect all records from each tree (excluding the hidden root itself).
		var importedNodes = imported.WalkAll().ToList();
		var currentNodes  = current.WalkAll().ToList();

		var importedCount = importedNodes.Count;
		var currentCount  = currentNodes.Count;

		if ( importedCount != currentCount )
		{
			lines.Add( $"imported tree has {importedCount} node(s) vs current {currentCount} node(s)" );
		}

		// Build ClassName sets for each side.
		var importedNames = new HashSet<string>(
			importedNodes.Select( r => r.ClassName ).Where( n => !string.IsNullOrEmpty( n ) ),
			StringComparer.Ordinal );
		var currentNames  = new HashSet<string>(
			currentNodes.Select( r => r.ClassName ).Where( n => !string.IsNullOrEmpty( n ) ),
			StringComparer.Ordinal );

		foreach ( var name in currentNames )
		{
			if ( !importedNames.Contains( name ) )
				lines.Add( $"node '{name}' present in current IR but not in .razor — will be lost" );
		}
		foreach ( var name in importedNames )
		{
			if ( !currentNames.Contains( name ) )
				lines.Add( $"node '{name}' present in .razor but not in current IR — will be added" );
		}

		var importedByName = importedNodes
			.Where( r => !string.IsNullOrEmpty( r.ClassName ) )
			.GroupBy( r => r.ClassName )
			.ToDictionary( g => g.Key, g => g.First(), StringComparer.Ordinal );
		var currentByName  = currentNodes
			.Where( r => !string.IsNullOrEmpty( r.ClassName ) )
			.GroupBy( r => r.ClassName )
			.ToDictionary( g => g.Key, g => g.First(), StringComparer.Ordinal );

		foreach ( var kvp in importedByName )
		{
			if ( !currentByName.TryGetValue( kvp.Key, out var cur ) ) continue;
			var imp = kvp.Value;

			// Content / icon fields (payload-facing).
			if ( imp.Content != cur.Content )
				lines.Add( $"node '{imp.ClassName}': content differs ('{cur.Content}' → '{imp.Content}')" );
			if ( imp.IconName != cur.IconName )
				lines.Add( $"node '{imp.ClassName}': icon differs ('{cur.IconName}' → '{imp.IconName}')" );
			if ( imp.Placeholder != cur.Placeholder )
				lines.Add( $"node '{imp.ClassName}': placeholder differs ('{cur.Placeholder}' → '{imp.Placeholder}')" );

			// Key appearance dimensions.
			if ( imp.Width != cur.Width )
				lines.Add( $"node '{imp.ClassName}': width differs ({cur.Width} → {imp.Width})" );
			if ( imp.Height != cur.Height )
				lines.Add( $"node '{imp.ClassName}': height differs ({cur.Height} → {imp.Height})" );
		}

		return lines;
	}

	// --- Markup parsing ----------------------------------------------------

	private static int FindRootOpen( string source )
	{
		// Match `<root` followed by attribute-or-close char.
		for ( int i = 0; i < source.Length - 5; i++ )
		{
			if ( source[i] != '<' ) continue;
			if ( i + 4 >= source.Length ) break;
			if ( source[i + 1] == 'r' && source[i + 2] == 'o' && source[i + 3] == 'o' && source[i + 4] == 't' )
			{
				var after = i + 5;
				if ( after >= source.Length ) return -1;
				var c = source[after];
				if ( char.IsWhiteSpace( c ) || c == '>' || c == '/' ) return i;
			}
		}
		return -1;
	}

	private static void SkipPastTagOpen( string source, ref int pos )
	{
		while ( pos < source.Length && source[pos] != '>' ) pos++;
		if ( pos < source.Length ) pos++;
	}

	private static void SkipWhitespace( string source, ref int pos )
	{
		while ( pos < source.Length && char.IsWhiteSpace( source[pos] ) ) pos++;
	}

	private static void ParseChildren( string source, ref int pos, ControlRecord parent, List<ValidationDiagnostic> diags, string parentTag )
	{
		while ( pos < source.Length )
		{
			SkipWhitespace( source, ref pos );
			if ( pos >= source.Length ) return;

			if ( source[pos] != '<' )
			{
				pos++;
				continue;
			}

			// Closing tag for parent? </parentTag>
			if ( pos + 1 < source.Length && source[pos + 1] == '/' )
			{
				// Skip past closing tag.
				while ( pos < source.Length && source[pos] != '>' ) pos++;
				if ( pos < source.Length ) pos++;
				return;
			}

			ParseElement( source, ref pos, parent, diags );
		}
	}

	private static void ParseElement( string source, ref int pos, ControlRecord parent, List<ValidationDiagnostic> diags )
	{
		// pos at '<'
		pos++; // skip '<'
		var tagStart = pos;
		while ( pos < source.Length && IsTagChar( source[pos] ) ) pos++;
		var tag = source.Substring( tagStart, pos - tagStart );

		var attrs = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		bool selfClose = false;

		while ( pos < source.Length )
		{
			SkipWhitespace( source, ref pos );
			if ( pos >= source.Length ) break;

			if ( source[pos] == '/' && pos + 1 < source.Length && source[pos + 1] == '>' )
			{
				selfClose = true;
				pos += 2;
				break;
			}
			if ( source[pos] == '>' )
			{
				pos++;
				break;
			}

			var nameStart = pos;
			while ( pos < source.Length && source[pos] != '=' && !char.IsWhiteSpace( source[pos] )
				&& source[pos] != '>' && source[pos] != '/' )
				pos++;
			var attrName = source.Substring( nameStart, pos - nameStart );

			SkipWhitespace( source, ref pos );
			string attrValue = "";
			if ( pos < source.Length && source[pos] == '=' )
			{
				pos++;
				SkipWhitespace( source, ref pos );
				if ( pos < source.Length && source[pos] == '"' )
				{
					pos++;
					var valStart = pos;
					while ( pos < source.Length && source[pos] != '"' ) pos++;
					attrValue = Unescape( source.Substring( valStart, pos - valStart ) );
					if ( pos < source.Length ) pos++; // closing quote
				}
			}
			if ( !string.IsNullOrEmpty( attrName ) )
				attrs[attrName] = attrValue;
		}

		// Resolve type.
		if ( !TagToType.TryGetValue( tag, out var type ) )
		{
			var msg = $"unknown tag <{tag}> — skipped";
			diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
			Log.Warning( $"{LogPrefix} LegacyRazorImporter.Parse: {msg}" );
			if ( !selfClose ) SkipToCloseTag( source, ref pos, tag );
			return;
		}

		var defaults = ControlDefaults.For( type );
		var record = new ControlRecord
		{
			Type = type,
			ClassName = attrs.TryGetValue( "class", out var cls ) ? cls : "",
			Width = defaults.DefaultWidth,
			Height = defaults.DefaultHeight,
			Content = defaults.DefaultContent,
			IconName = defaults.DefaultIcon,
			FlexGrow = defaults.DefaultFlexGrow,
			Direction = defaults.DefaultDirection,
			Wrap = defaults.DefaultWrap,
		};

		if ( attrs.TryGetValue( "slot", out var slotName ) && !string.IsNullOrEmpty( slotName ) )
		{
			record.IsSlot = true;
			record.SlotName = slotName;
		}

		if ( attrs.TryGetValue( "src", out var src ) )
			record.Source = src;

		if ( attrs.TryGetValue( "placeholder", out var ph ) )
			record.Placeholder = ph;

		parent.Children.Add( record );

		if ( selfClose ) return;

		if ( Grains.RazorDesigner.Contracts.ContractScanner.Table.Get( type ).IsContainer )
		{
			ParseChildren( source, ref pos, record, diags, tag );
		}
		else
		{
			// Read inner text up to </tag>.
			var textStart = pos;
			while ( pos < source.Length && source[pos] != '<' ) pos++;
			var raw = source.Substring( textStart, pos - textStart ).Trim();
			var text = Unescape( raw );

			if ( !string.IsNullOrEmpty( text ) )
			{
				if ( type == ControlType.IconPanel )
					record.IconName = text;
				else
					record.Content = text;
			}

			// Skip </tag>.
			if ( pos < source.Length && source[pos] == '<' )
			{
				while ( pos < source.Length && source[pos] != '>' ) pos++;
				if ( pos < source.Length ) pos++;
			}
		}
	}

	private static void SkipToCloseTag( string source, ref int pos, string tag )
	{
		// Brace-balanced skip until matching </tag>. Tolerant of nested same-name tags.
		int depth = 1;
		while ( pos < source.Length && depth > 0 )
		{
			if ( source[pos] != '<' ) { pos++; continue; }
			if ( pos + 1 < source.Length && source[pos + 1] == '/' )
			{
				// closing tag
				var nameStart = pos + 2;
				int nameEnd = nameStart;
				while ( nameEnd < source.Length && IsTagChar( source[nameEnd] ) ) nameEnd++;
				var name = source.Substring( nameStart, nameEnd - nameStart );
				while ( pos < source.Length && source[pos] != '>' ) pos++;
				if ( pos < source.Length ) pos++;
				if ( string.Equals( name, tag, StringComparison.OrdinalIgnoreCase ) ) depth--;
			}
			else
			{
				// opening tag — check self-close
				var nameStart = pos + 1;
				int nameEnd = nameStart;
				while ( nameEnd < source.Length && IsTagChar( source[nameEnd] ) ) nameEnd++;
				var name = source.Substring( nameStart, nameEnd - nameStart );
				bool same = string.Equals( name, tag, StringComparison.OrdinalIgnoreCase );
				bool selfClose = false;
				while ( pos < source.Length && source[pos] != '>' )
				{
					if ( source[pos] == '/' && pos + 1 < source.Length && source[pos + 1] == '>' ) { selfClose = true; break; }
					pos++;
				}
				if ( pos < source.Length && source[pos] != '>' ) pos++;
				if ( pos < source.Length ) pos++;
				if ( same && !selfClose ) depth++;
			}
		}
	}

	private static bool IsTagChar( char c ) => char.IsLetterOrDigit( c ) || c == '-' || c == '_';

	private static string Unescape( string s )
	{
		if ( string.IsNullOrEmpty( s ) ) return s;
		return s
			.Replace( "&quot;", "\"" )
			.Replace( "&gt;", ">" )
			.Replace( "&lt;", "<" )
			.Replace( "&amp;", "&" );
	}

	// --- SCSS parsing ------------------------------------------------------

	private static void ParseScssBlocks( string css, Action<string, string> onRule )
	{
		int pos = 0;
		while ( pos < css.Length )
		{
			SkipScssNoise( css, ref pos );
			if ( pos >= css.Length ) break;

			// Read selector until '{'.
			var selStart = pos;
			while ( pos < css.Length && css[pos] != '{' )
			{
				if ( IsScssCommentStart( css, pos ) ) { SkipOneScssComment( css, ref pos ); continue; }
				pos++;
			}
			if ( pos >= css.Length ) break;
			var selector = css.Substring( selStart, pos - selStart ).Trim();
			pos++; // skip '{'

			// Brace-balanced body.
			var bodyStart = pos;
			int depth = 1;
			while ( pos < css.Length && depth > 0 )
			{
				if ( IsScssCommentStart( css, pos ) ) { SkipOneScssComment( css, ref pos ); continue; }
				if ( css[pos] == '{' ) depth++;
				else if ( css[pos] == '}' ) { depth--; if ( depth == 0 ) break; }
				pos++;
			}
			var body = css.Substring( bodyStart, pos - bodyStart );
			if ( pos < css.Length ) pos++; // skip '}'

			if ( !string.IsNullOrEmpty( selector ) )
				onRule( selector, body );
		}
	}

	private static void SkipScssNoise( string css, ref int pos )
	{
		while ( pos < css.Length )
		{
			if ( char.IsWhiteSpace( css[pos] ) ) { pos++; continue; }
			if ( IsScssCommentStart( css, pos ) ) { SkipOneScssComment( css, ref pos ); continue; }
			break;
		}
	}

	private static bool IsScssCommentStart( string css, int pos )
	{
		if ( pos + 1 >= css.Length ) return false;
		return css[pos] == '/' && ( css[pos + 1] == '/' || css[pos + 1] == '*' );
	}

	private static void SkipOneScssComment( string css, ref int pos )
	{
		if ( css[pos + 1] == '/' )
		{
			while ( pos < css.Length && css[pos] != '\n' ) pos++;
			return;
		}
		// /* ... */
		pos += 2;
		while ( pos + 1 < css.Length && !( css[pos] == '*' && css[pos + 1] == '/' ) ) pos++;
		if ( pos + 1 < css.Length ) pos += 2;
	}

	private static void ProcessScssRule( string selector, string body, ControlRecord rootRecord, Dictionary<string, ControlRecord> nameMap, List<ValidationDiagnostic> diags )
	{
		var sel = selector.Trim();
		if ( sel.StartsWith( "." ) )
		{
			var className = sel.Substring( 1 ).Trim();
			if ( nameMap.TryGetValue( className, out var record ) )
			{
				ApplyDeclsAndRecurse( body, record, rootRecord, nameMap, diags );
			}
			else
			{
				var msg = $"scss rule for unknown class '.{className}' — recursing for nested rules";
				diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
				Log.Warning( $"{LogPrefix} LegacyRazorImporter.Scss: {msg}" );
				ParseScssBlocks( body, ( s, b ) => ProcessScssRule( s, b, rootRecord, nameMap, diags ) );
			}
		}
		else
		{
			ApplyDeclsAndRecurse( body, rootRecord, rootRecord, nameMap, diags );
		}
	}

	private static void ApplyDeclsAndRecurse( string body, ControlRecord record, ControlRecord rootRecord, Dictionary<string, ControlRecord> nameMap, List<ValidationDiagnostic> diags )
	{
		ParseRuleBody( body,
			onDecl: ( prop, val ) => DispatchDecl( record, prop, val, diags ),
			onNestedRule: ( sel, nestedBody ) =>
			{
				var s = sel.Trim();
				if ( s.StartsWith( ">" ) && record.Type == ControlType.Checkbox && s.Contains( ".checkmark" ) )
				{
					ParseRuleBody( nestedBody,
						onDecl: ( p, v ) =>
						{
							if ( string.Equals( p, "width", StringComparison.OrdinalIgnoreCase )
								&& Length.TryParse( v, out var len ) )
							{
								record.CheckboxSize = len;
							}
						},
						onNestedRule: ( ss, bb ) =>
						{
							var msg = $"unexpected nested rule inside .checkmark: {ss}";
							diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
							Log.Warning( $"{LogPrefix} LegacyRazorImporter.Scss: {msg}" );
						} );
					return;
				}
				ProcessScssRule( sel, nestedBody, rootRecord, nameMap, diags );
			} );
	}

	private static void ParseRuleBody( string body, Action<string, string> onDecl, Action<string, string> onNestedRule )
	{
		int pos = 0;
		while ( pos < body.Length )
		{
			SkipScssNoise( body, ref pos );
			if ( pos >= body.Length ) break;

			// Lookahead: find next ';' or '{', honoring comments.
			int scan = pos;
			while ( scan < body.Length && body[scan] != ';' && body[scan] != '{' )
			{
				if ( IsScssCommentStart( body, scan ) ) { SkipOneScssComment( body, ref scan ); continue; }
				scan++;
			}

			if ( scan >= body.Length ) break;

			if ( body[scan] == ';' )
			{
				var declText = body.Substring( pos, scan - pos ).Trim();
				pos = scan + 1;
				var colon = declText.IndexOf( ':' );
				if ( colon > 0 )
				{
					var prop = declText.Substring( 0, colon ).Trim();
					var val = declText.Substring( colon + 1 ).Trim();
					onDecl( prop, val );
				}
			}
			else // '{'
			{
				var selector = body.Substring( pos, scan - pos ).Trim();
				pos = scan + 1;
				var bodyStart = pos;
				int depth = 1;
				while ( pos < body.Length && depth > 0 )
				{
					if ( IsScssCommentStart( body, pos ) ) { SkipOneScssComment( body, ref pos ); continue; }
					if ( body[pos] == '{' ) depth++;
					else if ( body[pos] == '}' ) { depth--; if ( depth == 0 ) break; }
					pos++;
				}
				var nestedBody = body.Substring( bodyStart, pos - bodyStart );
				if ( pos < body.Length ) pos++;
				onNestedRule( selector, nestedBody );
			}
		}
	}

	// --- Decl dispatch -----------------------------------------------------

	private static void DispatchDecl( ControlRecord r, string prop, string val, List<ValidationDiagnostic> diags )
	{
		switch ( prop.ToLowerInvariant() )
		{
			case "width": if ( Length.TryParse( val, out var w ) ) r.Width = w; else WarnDecl( prop, val, diags ); break;
			case "height": if ( Length.TryParse( val, out var h ) ) r.Height = h; else WarnDecl( prop, val, diags ); break;
			case "flex-basis": if ( Length.TryParse( val, out var fb ) ) r.FlexBasis = fb; else WarnDecl( prop, val, diags ); break;
			case "flex-grow": if ( TryParseFloat( val, out var fg ) ) r.FlexGrow = fg; else WarnDecl( prop, val, diags ); break;
			case "flex-shrink": if ( TryParseFloat( val, out var fs ) ) r.FlexShrink = fs; else WarnDecl( prop, val, diags ); break;

			case "flex-direction":
				if ( TryParseDirection( val, out var dir ) ) r.Direction = dir;
				else WarnDecl( prop, val, diags );
				break;
			case "justify-content":
				if ( TryParseJustify( val, out var jc ) ) r.Justify = jc;
				else WarnDecl( prop, val, diags );
				break;
			case "align-items":
				if ( TryParseAlign( val, out var ai ) ) r.Align = ai;
				else WarnDecl( prop, val, diags );
				break;
			case "flex-wrap":
				if ( TryParseWrap( val, out var fw ) ) r.Wrap = fw;
				else WarnDecl( prop, val, diags );
				break;

			case "gap":
				if ( TryParsePxFloat( val, out var gap ) ) r.Gap = gap;
				else WarnDecl( prop, val, diags );
				break;

			case "padding":
				if ( Edges.TryParse( val, out var pad ) ) r.Padding = pad;
				else WarnDecl( prop, val, diags );
				break;

			// Typography group
			case "font-family":
				r.FontFamily = val;
				r.OverrideTypography = true;
				break;
			case "font-size":
				if ( Length.TryParse( val, out var fontSize ) ) r.FontSize = fontSize;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideTypography = true;
				break;
			case "font-weight":
				if ( int.TryParse( val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fwt ) ) r.FontWeight = fwt;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideTypography = true;
				break;
			case "color":
				if ( TryParseColor( val, out var col ) ) r.Color = col;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideTypography = true;
				break;
			case "text-align":
				if ( TryParseTextAlign( val, out var ta ) ) r.TextAlign = ta;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideTypography = true;
				break;

			// Background group
			case "background-color":
				if ( TryParseColor( val, out var bgc ) ) r.BackgroundColor = bgc;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideBackground = true;
				break;
			case "background-image":
				r.BackgroundImage = string.Equals( val, "none", StringComparison.OrdinalIgnoreCase ) ? "" : val;
				r.OverrideBackground = true;
				break;

			// Border group
			case "border-color":
				if ( TryParseColor( val, out var bc ) ) r.BorderColor = bc;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideBorder = true;
				break;
			case "border-radius":
				if ( Length.TryParse( val, out var br ) ) r.BorderRadius = br;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideBorder = true;
				break;
			case "border-width":
				if ( Length.TryParse( val, out var bw ) ) r.BorderWidth = bw;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideBorder = true;
				break;

			// Effects group
			case "box-shadow":
				if ( TryParseBoxShadow( val, out var bsX, out var bsY, out var bsBlur, out var bsColor, out var bsInset ) )
				{
					r.BoxShadowX = bsX;
					r.BoxShadowY = bsY;
					r.BoxShadowBlur = bsBlur;
					r.BoxShadowColor = bsColor;
					r.BoxShadowInset = bsInset;
				}
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideEffects = true;
				break;
			case "opacity":
				if ( TryParseFloat( val, out var op ) ) r.Opacity = op;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideEffects = true;
				break;

			// Constraints group
			case "margin":
				if ( Edges.TryParse( val, out var mg ) ) r.Margin = mg;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideConstraints = true;
				break;
			case "min-width":
				if ( Length.TryParse( val, out var mnw ) ) r.MinWidth = mnw;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideConstraints = true;
				break;
			case "max-width":
				if ( Length.TryParse( val, out var mxw ) ) r.MaxWidth = mxw;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideConstraints = true;
				break;
			case "min-height":
				if ( Length.TryParse( val, out var mnh ) ) r.MinHeight = mnh;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideConstraints = true;
				break;
			case "max-height":
				if ( Length.TryParse( val, out var mxh ) ) r.MaxHeight = mxh;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideConstraints = true;
				break;

			// Interaction group
			case "cursor":
				if ( TryParseCursor( val, out var cur ) ) r.Cursor = cur;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideInteraction = true;
				break;
			case "overflow":
				if ( TryParseOverflow( val, out var ov ) ) r.Overflow = ov;
				else { WarnDecl( prop, val, diags ); break; }
				r.OverrideInteraction = true;
				break;

			default:
				var msg = $"unknown css property '{prop}: {val}' on .{r.ClassName} — dropped";
				diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
				Log.Warning( $"{LogPrefix} LegacyRazorImporter.Scss: {msg}" );
				break;
		}
	}

	private static void WarnDecl( string prop, string val, List<ValidationDiagnostic> diags )
	{
		var msg = $"could not parse '{prop}: {val}' — kept default";
		diags.Add( new ValidationDiagnostic( null, DiagnosticSeverity.Warn, "legacy-import", msg ) );
		Log.Warning( $"{LogPrefix} LegacyRazorImporter.Scss: {msg}" );
	}

	private static bool TryParseFloat( string s, out float v ) =>
		float.TryParse( s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v );

	private static bool TryParsePxFloat( string s, out float v )
	{
		var t = s.Trim();
		if ( t.EndsWith( "px", StringComparison.OrdinalIgnoreCase ) )
			t = t.Substring( 0, t.Length - 2 ).Trim();
		return float.TryParse( t, NumberStyles.Float, CultureInfo.InvariantCulture, out v );
	}

	private static bool TryParseDirection( string s, out FlexDirection v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "row": v = FlexDirection.Row; return true;
			case "column": v = FlexDirection.Column; return true;
			default: v = FlexDirection.Row; return false;
		}
	}

	private static bool TryParseJustify( string s, out JustifyContent v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "flex-start": case "start": v = JustifyContent.Start; return true;
			case "center": v = JustifyContent.Center; return true;
			case "flex-end": case "end": v = JustifyContent.End; return true;
			case "space-between": v = JustifyContent.SpaceBetween; return true;
			case "space-around": v = JustifyContent.SpaceAround; return true;
			default: v = JustifyContent.Start; return false;
		}
	}

	private static bool TryParseAlign( string s, out AlignItems v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "flex-start": case "start": v = AlignItems.Start; return true;
			case "center": v = AlignItems.Center; return true;
			case "flex-end": case "end": v = AlignItems.End; return true;
			case "stretch": v = AlignItems.Stretch; return true;
			default: v = AlignItems.Stretch; return false;
		}
	}

	private static bool TryParseWrap( string s, out FlexWrap v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "nowrap": v = FlexWrap.NoWrap; return true;
			case "wrap": v = FlexWrap.Wrap; return true;
			case "wrap-reverse": v = FlexWrap.WrapReverse; return true;
			default: v = FlexWrap.NoWrap; return false;
		}
	}

	private static bool TryParseTextAlign( string s, out TextAlignment v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "left": v = TextAlignment.Left; return true;
			case "center": v = TextAlignment.Center; return true;
			case "right": v = TextAlignment.Right; return true;
			default: v = TextAlignment.Left; return false;
		}
	}

	private static bool TryParseCursor( string s, out CursorKind v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "auto": v = CursorKind.Auto; return true;
			case "default": v = CursorKind.Default; return true;
			case "pointer": v = CursorKind.Pointer; return true;
			case "text": v = CursorKind.Text; return true;
			case "grab": v = CursorKind.Grab; return true;
			case "grabbing": v = CursorKind.Grabbing; return true;
			case "wait": v = CursorKind.Wait; return true;
			case "crosshair": v = CursorKind.Crosshair; return true;
			case "move": v = CursorKind.Move; return true;
			case "not-allowed": v = CursorKind.NotAllowed; return true;
			case "none": v = CursorKind.None; return true;
			default: v = CursorKind.Auto; return false;
		}
	}

	private static bool TryParseOverflow( string s, out OverflowKind v )
	{
		switch ( s.Trim().ToLowerInvariant() )
		{
			case "visible": v = OverflowKind.Visible; return true;
			case "hidden": v = OverflowKind.Hidden; return true;
			case "scroll": v = OverflowKind.Scroll; return true;
			case "clip": v = OverflowKind.Clip; return true;
			case "clip-whole": v = OverflowKind.ClipWhole; return true;
			default: v = OverflowKind.Visible; return false;
		}
	}

	private static bool TryParseBoxShadow( string s, out Length x, out Length y, out Length blur, out Color color, out bool inset )
	{
		x = Length.Px( 0 ); y = Length.Px( 0 ); blur = Length.Px( 0 ); color = Color.Black; inset = false;

		var parts = s.Trim().Split( (char[])null, StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length < 4 ) return false;

		int n = parts.Length;
		if ( string.Equals( parts[n - 1], "inset", StringComparison.OrdinalIgnoreCase ) )
		{
			inset = true;
			n--;
		}
		if ( n < 4 ) return false;

		if ( !Length.TryParse( parts[0], out x ) ) return false;
		if ( !Length.TryParse( parts[1], out y ) ) return false;
		if ( !Length.TryParse( parts[2], out blur ) ) return false;
		if ( !TryParseColor( parts[3], out color ) ) return false;
		return true;
	}

	private static bool TryParseColor( string s, out Color color )
	{
		color = Color.White;
		if ( string.IsNullOrWhiteSpace( s ) ) return false;
		var t = s.Trim();
		if ( !t.StartsWith( "#" ) ) return false;
		var hex = t.Substring( 1 );

		int r, g, b, a = 255;
		if ( hex.Length == 6 )
		{
			if ( !TryHex( hex.Substring( 0, 2 ), out r ) ) return false;
			if ( !TryHex( hex.Substring( 2, 2 ), out g ) ) return false;
			if ( !TryHex( hex.Substring( 4, 2 ), out b ) ) return false;
		}
		else if ( hex.Length == 8 )
		{
			if ( !TryHex( hex.Substring( 0, 2 ), out r ) ) return false;
			if ( !TryHex( hex.Substring( 2, 2 ), out g ) ) return false;
			if ( !TryHex( hex.Substring( 4, 2 ), out b ) ) return false;
			if ( !TryHex( hex.Substring( 6, 2 ), out a ) ) return false;
		}
		else if ( hex.Length == 3 )
		{
			// #RGB shorthand — duplicate each nibble.
			if ( !TryHex( "" + hex[0] + hex[0], out r ) ) return false;
			if ( !TryHex( "" + hex[1] + hex[1], out g ) ) return false;
			if ( !TryHex( "" + hex[2] + hex[2], out b ) ) return false;
		}
		else
		{
			return false;
		}

		color = new Color( r / 255f, g / 255f, b / 255f, a / 255f );
		return true;
	}

	private static bool TryHex( string s, out int v ) =>
		int.TryParse( s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v );
}
