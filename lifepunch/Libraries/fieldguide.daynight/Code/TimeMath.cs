using System;

namespace FieldGuide.DayNight;

/// <summary>Pure helpers for setting the clock from a UI. Kept separate from the networked component so they
/// are trivially unit-testable and reusable (a preview tool, an editor slider, a debug console).</summary>
public static class TimeMath
{
	/// <summary>Set the clock to an hour-of-day while PRESERVING the current day index, so the deterministic
	/// per-day weather roll does not re-roll when a player scrubs the time within a day. Result =
	/// floor(current/24)*24 + clamp(hourOfDay, 0, 24). Example: day 1 at 15:30 (total 39.5), set 06:00 → 30.0
	/// (still day 1).</summary>
	public static float ComputeSetHour( float currentTotalHours, float hourOfDay )
		=> MathF.Floor( currentTotalHours / 24f ) * 24f + Math.Clamp( hourOfDay, 0f, 24f );

	/// <summary>Map a slider/drag fraction across a track (0 left, 1 right) to a quantized hour-of-day in
	/// [0,24], rounded to the nearest in-game MINUTE (1/60 h). A pixel-cheap throttle with no timer state: a
	/// drag emits at most one distinct value per minute of readout. Feed the result into
	/// <see cref="ComputeSetHour"/> to keep the day index.
	///
	/// The result is clamped AFTER quantizing, and that clamp is load-bearing: one minute is not exactly
	/// representable in binary, so 1440 steps of 1/60 accumulate to 24.000002 and a full-right drag would
	/// hand <see cref="ComputeSetHour"/> a value past the end of the day. Round first, clamp second.</summary>
	public static float ComputeSliderHour( float frac )
	{
		float hour = Math.Clamp( frac, 0f, 1f ) * 24f;
		const float step = 1f / 60f;   // one in-game minute
		return Math.Clamp( MathF.Round( hour / step ) * step, 0f, 24f );
	}
}
