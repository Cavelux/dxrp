using System;

namespace FieldGuide.Tips;

/// <summary>
/// Which input device the player last used. Backed by <c>Input.UsingController</c>, which the engine
/// flaps to the last-used device (there is no event; it is polled per frame). The coach exposes the
/// live value as <see cref="TipsCoach.ActiveDevice"/> and the display folds it into its BuildHash so a
/// device flip re-renders the active tip with the right wording and chips immediately.
/// </summary>
public enum TipDevice
{
	/// <summary>The player is on keyboard and mouse (the default when no controller has been used).</summary>
	KeyboardMouse,

	/// <summary>The player is on a game controller (the last button pressed was a pad button).</summary>
	Gamepad,
}

/// <summary>
/// Pure, engine-free render-time helpers for device-aware tip text: which wording a tip shows for a
/// device, and how a keycap label remaps when the player is on a pad. Kept free of any <c>Sandbox</c>
/// reference so the exact rules a game sees on screen can be exercised headlessly (the display calls
/// these; a harness calls them with fakes and asserts the truth table).
/// </summary>
public static class TipDeviceText
{
	/// <summary>
	/// The pad-mode wording for a tip: its <paramref name="textPad"/> when authored, else its
	/// <paramref name="text"/>. This is the single-text fallback (build plan point 3): a tip that leaves
	/// TextPad unset reads identically from either device, so the "either" idiom
	/// (<c>"Press *W* or `RT`"</c>) keeps rendering both chips with no auto-stripping.
	/// </summary>
	public static string PadTextOr( string text, string textPad )
		=> string.IsNullOrEmpty( textPad ) ? ( text ?? "" ) : textPad;

	/// <summary>
	/// The wording a tip shows for <paramref name="device"/>: <paramref name="text"/> on keyboard/mouse,
	/// and <see cref="PadTextOr(string, string)"/> on a pad. A single seam so the device-to-text choice is
	/// identical in the display and the harness.
	/// </summary>
	public static string ForDevice( string text, string textPad, TipDevice device )
		=> device == TipDevice.Gamepad ? PadTextOr( text, textPad ) : ( text ?? "" );

	/// <summary>
	/// Remap a keyboard keycap label for pad mode (build plan point 4), generalizing World Builder's
	/// <c>Cap()</c>. Applied to keycap chips at render time only when the player is on a pad:
	/// <list type="bullet">
	/// <item><paramref name="padLabelFor"/> null: no map is set, the label passes through unchanged (the
	/// single-text behaviour, so a game that never sets a map is unaffected).</item>
	/// <item>the map returns the same or a different non-empty string: that label is shown (an unmapped
	/// label the game's map passes through stays unchanged; a mapped one, e.g. "RMB" to "LT", swaps).</item>
	/// <item>the map returns null or empty: the chip is unpressable on a pad and is skipped (the caller
	/// renders nothing for it), matching WB's "skip the unpressable chip" behaviour.</item>
	/// </list>
	/// Returns the label to render, or null to skip the chip.
	/// </summary>
	public static string PadCap( string keyLabel, Func<string, string> padLabelFor )
	{
		if ( padLabelFor is null )
			return keyLabel; // no map set: passthrough

		var mapped = padLabelFor( keyLabel );
		return string.IsNullOrEmpty( mapped ) ? null : mapped; // null/empty = unpressable on pad, skip
	}
}
