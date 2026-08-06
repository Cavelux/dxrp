using System;
using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// The observable condition a declarative trigger watches. A single kind drives both relevance
/// ("may this tip show?") and completion ("has the player done the thing?"). The kinds carry no
/// delegates, so the same shape serializes onto a <c>.tip</c> asset (see <see cref="TipResource"/>).
/// </summary>
public enum TipTriggerKind
{
	/// <summary>Always true. The relevance default; never used for completion.</summary>
	Always,

	/// <summary>Any of <see cref="TipTrigger.Actions"/> was pressed this frame (Input.Pressed).</summary>
	InputAction,

	/// <summary>Any of <see cref="TipTrigger.Keys"/> was pressed this frame (Input.Keyboard.Pressed): keyboard keys plus "mouse1" / "mouse2".</summary>
	Key,

	/// <summary>A named string signal has been latched this session (via <see cref="TipsCoach.Signal(string)"/>).</summary>
	Signal,

	/// <summary>A custom behaviour was latched this session, read through <see cref="TipContext.Ever(string)"/>.</summary>
	Ever,

	/// <summary><see cref="TipContext.Flag(string)"/> is true this frame.</summary>
	Flag,

	/// <summary><see cref="TipContext.Number(string)"/> is at least <see cref="TipTrigger.Threshold"/>.</summary>
	AtLeast,

	/// <summary>The tip has been visible at least <see cref="TipTrigger.Seconds"/> seconds.</summary>
	Timer,

	/// <summary>An analog stick (move or look) is pushed at or past <see cref="TipTrigger.Threshold"/> magnitude (0..1). Completes a gamepad tip whose action has no digital press, e.g. a stick-throttle "accelerate".</summary>
	AnalogAxis,

	/// <summary>Any child trigger fires.</summary>
	AnyOf,

	/// <summary>Every child trigger fires.</summary>
	AllOf,
}

/// <summary>
/// Which analog stick an <see cref="TipTriggerKind.AnalogAxis"/> trigger watches. Both read the default
/// input device, so the same trigger works on a controller stick or whatever else the engine maps to
/// the move / look axes.
/// </summary>
public enum TipTriggerAnalogSource
{
	/// <summary>The movement stick (Input.AnalogMove): forward / strafe.</summary>
	AnalogMove,

	/// <summary>The look stick (Input.AnalogLook): aim / camera.</summary>
	AnalogLook,
}

/// <summary>
/// A declarative trigger. It holds no delegates, so it doubles as the serialized shape authored on a
/// <c>.tip</c> asset and evaluated by <see cref="TipsCoach"/> each frame against <see cref="Sandbox.Input"/>
/// and the pushed (or synthesized) <see cref="TipContext"/>.
///
/// Build one with the ergonomic factories rather than hand-filling the record, e.g.
/// <c>TipTrigger.Action( "Jump" )</c> or <c>TipTrigger.Any( TipTrigger.Action( "Jump" ), TipTrigger.KeyPress( "space" ) )</c>.
/// Compose with <see cref="Any(TipTrigger[])"/> / <see cref="All(TipTrigger[])"/>.
/// </summary>
public sealed record TipTrigger
{
	/// <summary>Which observable condition this trigger watches.</summary>
	public TipTriggerKind Kind { get; init; } = TipTriggerKind.Always;

	/// <summary>Input.config action names (<see cref="TipTriggerKind.InputAction"/>). Any one pressed fires the trigger.</summary>
	public string[] Actions { get; init; } = Array.Empty<string>();

	/// <summary>Raw key / mouse-button names (<see cref="TipTriggerKind.Key"/>), e.g. "w", "space", "mouse1". Any one fires.</summary>
	public string[] Keys { get; init; } = Array.Empty<string>();

	/// <summary>Key for <see cref="TipTriggerKind.Signal"/> / <see cref="TipTriggerKind.Ever"/> / <see cref="TipTriggerKind.Flag"/> / <see cref="TipTriggerKind.AtLeast"/>.</summary>
	public string Key { get; init; } = "";

	/// <summary>Threshold for <see cref="TipTriggerKind.AtLeast"/> (Number key at least Threshold).</summary>
	public float Threshold { get; init; }

	/// <summary>Seconds for <see cref="TipTriggerKind.Timer"/>.</summary>
	public float Seconds { get; init; }

	/// <summary>Which stick an <see cref="TipTriggerKind.AnalogAxis"/> trigger watches (magnitude compared against <see cref="Threshold"/>).</summary>
	public TipTriggerAnalogSource AnalogSource { get; init; } = TipTriggerAnalogSource.AnalogMove;

	/// <summary>Children for <see cref="TipTriggerKind.AnyOf"/> / <see cref="TipTriggerKind.AllOf"/>.</summary>
	public TipTrigger[] Children { get; init; } = Array.Empty<TipTrigger>();

	// Ergonomic factories so code authors do not hand-fill records.

	/// <summary>Completes on any of the given Input.config actions (keyboard, mouse or pad, whatever they are bound to).</summary>
	public static TipTrigger Action( params string[] actions ) => new() { Kind = TipTriggerKind.InputAction, Actions = actions };

	/// <summary>Completes on any of the given raw key / mouse-button names, e.g. "space", "mouse1".</summary>
	public static TipTrigger KeyPress( params string[] keys ) => new() { Kind = TipTriggerKind.Key, Keys = keys };

	/// <summary>Completes once the named string signal has been latched this session.</summary>
	public static TipTrigger Named( string signal ) => new() { Kind = TipTriggerKind.Signal, Key = signal };

	/// <summary>Completes once the tip has been visible at least <paramref name="seconds"/> seconds.</summary>
	public static TipTrigger After( float seconds ) => new() { Kind = TipTriggerKind.Timer, Seconds = seconds };

	/// <summary>Completes once the given analog stick is pushed at or past <paramref name="threshold"/> magnitude (0..1), e.g. a stick-throttle "accelerate" on a pad.</summary>
	public static TipTrigger Analog( TipTriggerAnalogSource source, float threshold ) => new() { Kind = TipTriggerKind.AnalogAxis, AnalogSource = source, Threshold = threshold };

	/// <summary>Fires when any child fires.</summary>
	public static TipTrigger Any( params TipTrigger[] any ) => new() { Kind = TipTriggerKind.AnyOf, Children = any };

	/// <summary>Fires when every child fires.</summary>
	public static TipTrigger All( params TipTrigger[] all ) => new() { Kind = TipTriggerKind.AllOf, Children = all };
}

/// <summary>
/// The ONE rule that turns a set of authored trigger fields into a runtime <see cref="TipTrigger"/>: which
/// fields a kind reads, and which it ignores. Two authoring surfaces feed it, the <c>.tip</c> asset's
/// <see cref="TipTriggerSpec"/> inspector and the Tips Studio's <see cref="TipStudioTrigger"/> picker, so the
/// mapping lives here once instead of being written twice and drifting. Pure (no <c>Sandbox</c> reference),
/// so the harness exercises the same code the editor and the panel do.
/// </summary>
public static class TipTriggerBuild
{
	/// <summary>
	/// Build the runtime trigger a kind's fields describe. Fields that do not belong to
	/// <paramref name="kind"/> are ignored, so an author who fills a box and then changes the kind never
	/// ships a stale value. An empty action / key name yields an empty list, which never fires, matching the
	/// asset path's long-standing behaviour. An unknown kind degrades to <see cref="TipTriggerKind.Always"/>.
	/// </summary>
	public static TipTrigger From( TipTriggerKind kind, string action, string key, string name,
		float threshold, float seconds, TipTriggerAnalogSource analogSource, float magnitude,
		IReadOnlyList<TipTrigger> children )
	{
		switch ( kind )
		{
			case TipTriggerKind.InputAction:
				return new TipTrigger
				{
					Kind = TipTriggerKind.InputAction,
					Actions = string.IsNullOrEmpty( action ) ? Array.Empty<string>() : new[] { action },
				};

			case TipTriggerKind.Key:
				return new TipTrigger
				{
					Kind = TipTriggerKind.Key,
					Keys = string.IsNullOrEmpty( key ) ? Array.Empty<string>() : new[] { key },
				};

			case TipTriggerKind.Signal:
			case TipTriggerKind.Ever:
			case TipTriggerKind.Flag:
				return new TipTrigger { Kind = kind, Key = name ?? "" };

			case TipTriggerKind.AtLeast:
				return new TipTrigger { Kind = TipTriggerKind.AtLeast, Key = name ?? "", Threshold = threshold };

			case TipTriggerKind.Timer:
				return new TipTrigger { Kind = TipTriggerKind.Timer, Seconds = seconds };

			case TipTriggerKind.AnalogAxis:
				return new TipTrigger { Kind = TipTriggerKind.AnalogAxis, AnalogSource = analogSource, Threshold = magnitude };

			case TipTriggerKind.AnyOf:
			case TipTriggerKind.AllOf:
				return new TipTrigger { Kind = kind, Children = ToArray( children ) };

			default:
				return new TipTrigger { Kind = TipTriggerKind.Always };
		}
	}

	private static TipTrigger[] ToArray( IReadOnlyList<TipTrigger> children )
	{
		if ( children is null || children.Count == 0 )
			return Array.Empty<TipTrigger>();

		var arr = new TipTrigger[children.Count];
		for ( var i = 0; i < children.Count; i++ )
			arr[i] = children[i] ?? new TipTrigger { Kind = TipTriggerKind.Always };
		return arr;
	}
}
