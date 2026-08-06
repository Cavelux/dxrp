using System;
using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// One coaching tip: a short line, when it becomes relevant, and when the player has
/// *done the thing* (so it retires itself by observed behaviour, not a timer). Tips are
/// pure data, a game authors its whole tutorial by building a list of these and handing it to
/// <see cref="TipsCatalog.Register(IReadOnlyList{TipDefinition})"/>, nothing else.
///
/// Text markup: wrap a key/button name in asterisks and <see cref="TipsDisplay"/> renders it as a
/// bolded keycap, e.g. <c>"Hold *B* to block."</c>. Everything outside the asterisks is plain text.
/// </summary>
public sealed record TipDefinition
{
	/// <summary>Stable id (persisted once completed, and used as a prerequisite key). Keep it unique.</summary>
	public required string Id { get; init; }

	/// <summary>The one-line tip. One action per tip. Wrap key names in *asterisks* to bold them.</summary>
	public required string Text { get; init; }

	/// <summary>
	/// Optional gamepad-specific wording, shown instead of <see cref="Text"/> while the player is on a
	/// controller (the display picks off <see cref="TipsCoach.ActiveDevice"/>). Null (the default) means
	/// <see cref="Text"/> serves both devices, so a tip with no pad-specific line reads identically on either
	/// (build plan point 3: the "either" idiom, <c>"Press *W* or `RT`"</c>, keeps rendering both chips; use
	/// TextPad only when the pad needs different words). Uses the same markup as <see cref="Text"/>: mark a
	/// gamepad button in `backticks`. For a whole-catalog keycap remap without authoring TextPad on every
	/// tip, set <see cref="TipsCoach.PadLabelFor"/> instead.
	/// </summary>
	public string TextPad { get; init; }

	/// <summary>A small emoji glyph shown beside the text (optional).</summary>
	public string Icon { get; init; } = "";

	/// <summary>Higher wins when several tips are eligible at once (an interrupt like "you're in a fight" outranks a calm walkthrough beat).</summary>
	public int Priority { get; init; }

	/// <summary>Tip ids that must be completed before this one is allowed to show, the backbone of the walkthrough order.</summary>
	public string[] PrerequisiteTipIds { get; init; } = Array.Empty<string>();

	/// <summary>When this returns true the tip is *relevant* and may be shown (its prerequisites permitting).</summary>
	public Func<TipContext, bool> Trigger { get; init; } = static _ => true;

	/// <summary>When this returns true the player has DONE the thing, the tip retires. This is the magic.</summary>
	public Func<TipContext, bool> CompleteWhen { get; init; } = static _ => false;

	/// <summary>Safety valve: if &gt; 0, the tip auto-completes after this many visible seconds even if <see cref="CompleteWhen"/> never fires (used for "just glance at X" beats with no behavioural signal).</summary>
	public float MaxShowSeconds { get; init; } = 0f;

	/// <summary>
	/// Optional declarative relevance trigger (v0.3). When set it NARROWS relevance: the tip is relevant
	/// only when <see cref="Trigger"/> is true AND this trigger evaluates true. Null falls back to
	/// <see cref="Trigger"/> alone, so an existing tip that leaves this unset behaves exactly as before.
	/// </summary>
	public TipTrigger Relevance { get; init; }

	/// <summary>
	/// Optional declarative completion trigger (v0.3). The tip retires when ANY completion path fires:
	/// <see cref="CompleteWhen"/> returns true, this trigger evaluates true, <see cref="MaxShowSeconds"/>
	/// elapses, or <see cref="TipsCoach.Complete(string)"/> is called for this id. Null leaves the
	/// predicate + timeout paths untouched, so this is purely additive.
	/// </summary>
	public TipTrigger Completion { get; init; }
}
