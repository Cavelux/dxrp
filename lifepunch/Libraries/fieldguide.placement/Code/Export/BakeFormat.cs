using System.Globalization;
using System.Text;

namespace FieldGuide.Placement;

/// <summary>
/// The text rules every export path shares: how a float becomes a C# literal, how a string is escaped,
/// and how a display name becomes a code-safe id. One home for them so the JSON file, the C# snippet,
/// and the panel's Copy button cannot drift apart in formatting.
/// </summary>
public static class BakeFormat
{
	/// <summary>The <see cref="ITweakTarget.FrameName"/> default: "this object's parent", nothing more
	/// specific known.</summary>
	public const string LocalFrame = "local";

	/// <summary>A float as a C# literal body: up to six decimals, invariant culture so a decimal never
	/// localises to a comma and breaks the pasted code. Callers append the "f" suffix.</summary>
	public static string F( float v ) => v.ToString( "0.######", CultureInfo.InvariantCulture );

	/// <summary>Escape a string for a C# / JSON double-quoted literal.</summary>
	public static string Escape( string s ) => (s ?? "").Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" );

	/// <summary>
	/// Squash a display name into a code-safe id: lowercase, non-alphanumerics collapsed to single
	/// underscores, no leading or trailing underscore. "Back Pack" becomes "back_pack", "Hat (left)"
	/// becomes "hat_left". Falls back to "item" for an empty or fully-stripped name.
	/// </summary>
	public static string SymbolFrom( string displayName )
	{
		if ( string.IsNullOrWhiteSpace( displayName ) ) return "item";

		var sb = new StringBuilder( displayName.Length );
		bool pendingUnderscore = false;
		foreach ( char c in displayName )
		{
			if ( char.IsLetterOrDigit( c ) )
			{
				if ( pendingUnderscore && sb.Length > 0 ) sb.Append( '_' );
				pendingUnderscore = false;
				sb.Append( char.ToLowerInvariant( c ) );
			}
			else
			{
				pendingUnderscore = true;
			}
		}

		return sb.Length == 0 ? "item" : sb.ToString();
	}
}
