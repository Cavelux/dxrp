using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// The authoring checks the Tips Studio runs on a draft while you type: the things that produce a card that
/// looks broken, or a tip that can never retire, and that are cheap to catch before the file is written.
/// Pure (no <c>Sandbox</c> reference, no engine state), so the harness asserts the same rules the panel shows.
///
/// Every message is a NOTE, never a block. The Studio will happily bake a tip with warnings on it, because
/// several of them are legitimate on purpose (an empty <c>AllOf</c> is the documented "a world trigger
/// retires this one" shape).
/// </summary>
public static class TipStudioText
{
	/// <summary>
	/// How long a single unbroken run of prose can get before the card is at risk of the grey-block quirk:
	/// the style engine rasterizes a text run that overflows one card line as a solid filled rectangle
	/// instead of wrapped glyphs. Chips break a line into separate runs, which is why a tip full of keycaps
	/// stays safe while one long sentence does not. Roughly one line at the card's 500px width; deliberately
	/// a round number rather than a measured one, because the real threshold moves with the wording.
	/// </summary>
	public const int MaxRunLength = 50;

	/// <summary>The length of the longest PLAIN run in a tip line. Chips (<c>*keycap*</c>,
	/// <c>`padchip`</c>) are separate runs and never count toward it, which mirrors how the card lays out.</summary>
	public static int LongestRun( string text )
	{
		var longest = 0;
		foreach ( var segment in TipSegment.Parse( text ) )
		{
			if ( segment.Kind != TipSegmentKind.Plain )
				continue;

			var length = segment.Text is null ? 0 : segment.Text.Trim().Length;
			if ( length > longest )
				longest = length;
		}

		return longest;
	}

	/// <summary>True when a line carries a run long enough to risk the grey-block quirk.</summary>
	public static bool RunTooLong( string text ) => LongestRun( text ) > MaxRunLength;

	/// <summary>The note for a too-long run, or null when the line is fine.</summary>
	public static string RunWarning( string text, string label )
	{
		var longest = LongestRun( text );
		if ( longest <= MaxRunLength )
			return null;

		return $"{label}: one run is {longest} characters. A run longer than a card line can render as a grey " +
			$"block. Break the sentence, or put a key chip in it.";
	}

	/// <summary>
	/// Every note for a draft, in the order the panel lists them. An empty list means nothing to flag.
	/// </summary>
	public static IReadOnlyList<string> Warnings( TipStudioDraft draft )
	{
		var notes = new List<string>();
		if ( draft is null )
			return notes;

		if ( string.IsNullOrWhiteSpace( draft.Id ) )
			notes.Add( "No id yet. The catalog keys tips by id, and the file is named after it." );

		if ( string.IsNullOrWhiteSpace( draft.Text ) )
			notes.Add( "No text yet. This is the line the player reads." );

		var textNote = RunWarning( draft.Text, "Text" );
		if ( textNote is not null )
			notes.Add( textNote );

		if ( !string.IsNullOrEmpty( draft.TextPad ) )
		{
			var padNote = RunWarning( draft.TextPad, "Pad text" );
			if ( padNote is not null )
				notes.Add( padNote );
		}

		AddTriggerNotes( notes, draft.Completion, "Completion", isCompletion: true );
		AddTriggerNotes( notes, draft.Relevance, "Relevance", isCompletion: false );

		return notes;
	}

	private static void AddTriggerNotes( List<string> notes, TipStudioTrigger trigger, string label, bool isCompletion )
	{
		if ( trigger is null )
			return;

		switch ( trigger.Kind )
		{
			case TipTriggerKind.InputAction when string.IsNullOrWhiteSpace( trigger.Action ):
				notes.Add( $"{label} is InputAction with no action picked, so it never fires." );
				break;

			case TipTriggerKind.Key when string.IsNullOrWhiteSpace( trigger.Key ):
				notes.Add( $"{label} is Key with no key name, so it never fires." );
				break;

			case TipTriggerKind.Signal or TipTriggerKind.Ever or TipTriggerKind.Flag or TipTriggerKind.AtLeast
				when string.IsNullOrWhiteSpace( trigger.Name ):
				notes.Add( $"{label} is {TipStudioTrigger.KindName( trigger.Kind )} with no name, so it never fires." );
				break;

			case TipTriggerKind.Timer when trigger.Seconds <= 0f && isCompletion:
				notes.Add( $"{label} is Timer with 0 seconds, so the tip retires the moment it is readable. Set Seconds." );
				break;

			case TipTriggerKind.AnalogAxis when trigger.Magnitude <= 0f:
				notes.Add( $"{label} is AnalogAxis with a magnitude of 0, so a resting stick already fires it." );
				break;

			case TipTriggerKind.AllOf when CountChildren( trigger ) == 0 && isCompletion:
				notes.Add( $"{label} is an empty AllOf, which never fires. That is the right shape when a " +
					"TipTriggerObject in the scene retires this tip." );
				break;

			case TipTriggerKind.AnyOf when CountChildren( trigger ) == 0:
				notes.Add( $"{label} is an empty AnyOf, so it never fires." );
				break;
		}

		if ( trigger.Children is null )
			return;

		foreach ( var child in trigger.Children )
			AddTriggerNotes( notes, child, $"{label} child", isCompletion );
	}

	private static int CountChildren( TipStudioTrigger trigger )
		=> trigger.Children is null ? 0 : trigger.Children.Count;
}
