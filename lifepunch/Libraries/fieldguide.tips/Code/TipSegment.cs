using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// What a run of tip text renders as: plain prose, a keyboard keycap, or a gamepad button chip.
/// </summary>
public enum TipSegmentKind
{
	/// <summary>Ordinary text, rendered as-is.</summary>
	Plain,

	/// <summary>A keyboard key or mouse-button name, rendered as a square keycap. Markup: <c>*W*</c>.</summary>
	Key,

	/// <summary>A gamepad button or stick, rendered as a rounded controller chip. Markup: <c>`A`</c> (backticks).</summary>
	GamepadButton,
}

/// <summary>
/// One run of tip text: either plain prose, a keyboard key that renders as a square keycap, or a
/// gamepad button that renders as a rounded controller chip. <see cref="TipsDisplay"/> walks a tip's
/// segments and styles each kind, so tip authors write a single string with markup around input names
/// instead of embedding UI.
///
/// Markup:
/// <list type="bullet">
/// <item><c>*W*</c> (asterisks) is a keyboard / mouse keycap, e.g. <c>"Hold *B* to block."</c></item>
/// <item><c>`A`</c> (backticks) is a gamepad button, e.g. <c>"Press `A` to jump."</c></item>
/// </list>
/// The two are independent, so one line can carry both for a keyboard-and-gamepad prompt:
/// <c>"Attack with *LMB* / `RT`."</c>. Everything outside the markers is plain.
/// </summary>
public readonly record struct TipSegment( string Text, TipSegmentKind Kind )
{
	/// <summary>True for a keyboard keycap run (<see cref="TipSegmentKind.Key"/>). Kept for readers that
	/// only distinguish keyboard chips from plain text.</summary>
	public bool IsKey => Kind == TipSegmentKind.Key;

	/// <summary>True for a gamepad button run (<see cref="TipSegmentKind.GamepadButton"/>).</summary>
	public bool IsGamepadButton => Kind == TipSegmentKind.GamepadButton;

	/// <summary>
	/// Split a tip line into alternating plain / key / gamepad runs. A matched pair of <c>*</c> marks a
	/// keyboard keycap; a matched pair of <c>`</c> marks a gamepad button; everything else is plain. An
	/// unmatched trailing marker degrades gracefully: its run stays plain.
	/// </summary>
	public static IReadOnlyList<TipSegment> Parse( string text )
	{
		var segments = new List<TipSegment>();
		if ( string.IsNullOrEmpty( text ) )
			return segments;

		var i = 0;
		while ( i < text.Length )
		{
			var c = text[i];

			// A marker only opens a chip if it has a matching partner later in the line; otherwise it is
			// literal text (so a lone `*` or backtick reads plainly instead of eating the rest of the line).
			if ( c == '*' || c == '`' )
			{
				var close = text.IndexOf( c, i + 1 );
				if ( close > i )
				{
					if ( close > i + 1 ) // non-empty run between the markers
					{
						var kind = c == '*' ? TipSegmentKind.Key : TipSegmentKind.GamepadButton;
						segments.Add( new TipSegment( text.Substring( i + 1, close - i - 1 ), kind ) );
					}
					i = close + 1;
					continue;
				}
			}

			// Accumulate a plain run up to the next marker (or end of line).
			var start = i;
			while ( i < text.Length && text[i] != '*' && text[i] != '`' )
				i++;

			// A marker with no partner ahead is literal: fold it into the plain run and keep going.
			if ( i < text.Length && text.IndexOf( text[i], i + 1 ) < 0 )
				i++;

			if ( i > start )
				segments.Add( new TipSegment( text.Substring( start, i - start ), TipSegmentKind.Plain ) );
		}

		return segments;
	}
}
