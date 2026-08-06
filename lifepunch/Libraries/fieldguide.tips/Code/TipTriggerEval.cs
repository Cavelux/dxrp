using System;

namespace FieldGuide.Tips;

/// <summary>
/// The environment a <see cref="TipTrigger"/> is evaluated against: the four world reads plus the active
/// tip's elapsed visible time. <see cref="TipsCoach"/> fills these from <see cref="Sandbox.Input"/>, the
/// pushed <see cref="TipContext"/> and its own timer; a headless harness fills them with fakes. Keeping
/// the reads behind delegates is what lets the trigger truth table be unit-tested without the engine.
/// </summary>
public sealed class TipTriggerEnv
{
	/// <summary>Was the named Input.config action pressed this frame? (Input.Pressed)</summary>
	public Func<string, bool> ActionPressed { get; init; } = static _ => false;

	/// <summary>Was the named raw key / mouse button pressed this frame? (Input.Keyboard.Pressed)</summary>
	public Func<string, bool> KeyPressed { get; init; } = static _ => false;

	/// <summary>Has the named string signal been latched this session? (Signal / Ever kinds)</summary>
	public Func<string, bool> SignalLatched { get; init; } = static _ => false;

	/// <summary>Is the named custom context flag true this frame? (TipContext.Flag)</summary>
	public Func<string, bool> Flag { get; init; } = static _ => false;

	/// <summary>The named custom context number this frame. (TipContext.Number)</summary>
	public Func<string, float> Number { get; init; } = static _ => 0f;

	/// <summary>Seconds the active tip has been visible, for the Timer kind.</summary>
	public float Elapsed { get; init; }

	/// <summary>The current magnitude (0..1-ish) of the given analog stick, for the AnalogAxis kind. (Input.AnalogMove / Input.AnalogLook length.)</summary>
	public Func<TipTriggerAnalogSource, float> AnalogMagnitude { get; init; } = static _ => 0f;
}

/// <summary>
/// The pure, engine-free evaluation core for a declarative <see cref="TipTrigger"/>: the per-kind rule and
/// the AnyOf / AllOf recursion, with every world read behind a <see cref="TipTriggerEnv"/> delegate. This
/// holds the whole completion/relevance truth table, so it can be exercised headlessly (the coach passes
/// real Input / context reads; a harness passes fakes) and stays identical between the two.
/// </summary>
public static class TipTriggerEval
{
	/// <summary>Evaluate a trigger against the given environment. Null trigger evaluates false.</summary>
	public static bool Evaluate( TipTrigger t, TipTriggerEnv env )
	{
		if ( t is null || env is null )
			return false;

		switch ( t.Kind )
		{
			case TipTriggerKind.Always:
				return true;

			case TipTriggerKind.InputAction:
				foreach ( var a in t.Actions )
					if ( !string.IsNullOrEmpty( a ) && env.ActionPressed( a ) )
						return true;
				return false;

			case TipTriggerKind.Key:
				foreach ( var k in t.Keys )
					if ( !string.IsNullOrEmpty( k ) && env.KeyPressed( k ) )
						return true;
				return false;

			case TipTriggerKind.Signal:
			case TipTriggerKind.Ever:
				return !string.IsNullOrEmpty( t.Key ) && env.SignalLatched( t.Key );

			case TipTriggerKind.Flag:
				return !string.IsNullOrEmpty( t.Key ) && env.Flag( t.Key );

			case TipTriggerKind.AtLeast:
				return !string.IsNullOrEmpty( t.Key ) && env.Number( t.Key ) >= t.Threshold;

			case TipTriggerKind.Timer:
				return env.Elapsed >= t.Seconds;

			case TipTriggerKind.AnalogAxis:
				return env.AnalogMagnitude( t.AnalogSource ) >= t.Threshold;

			case TipTriggerKind.AnyOf:
				foreach ( var c in t.Children )
					if ( Evaluate( c, env ) )
						return true;
				return false;

			case TipTriggerKind.AllOf:
				if ( t.Children.Length == 0 )
					return false;
				foreach ( var c in t.Children )
					if ( !Evaluate( c, env ) )
						return false;
				return true;

			default:
				return false;
		}
	}
}
