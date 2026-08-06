using System;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// THE SKY SEAM. This kit does not ship a sky shader or sky art (see the README, "Why no sky shader").
/// Instead it publishes, for any total game-hour, four normalized blend weights (morning, noon, evening,
/// night) summing to 1 with only the adjacent anchor pair non-zero. Your game decides what to DO with them:
/// crossfade four equirect skybox textures in your own shader, lerp four flat sky colours, swap SkyBox2D
/// materials, or ignore them entirely and just read <see cref="DayNightClock.GetTimeHours"/>.
///
/// The weights are a STATELESS pure function of the hour: no easing state, no temporal smoothing. So an
/// explicit time jump (a menu preset, a pin) lands the exact target weights the same frame (an instant snap),
/// while natural clock advance moves the hour smoothly and therefore crossfades smoothly. Both behaviours fall
/// out of purity, do not add smoothing on top.
///
/// Feed it the SAME hour the lighting grade uses (<see cref="DayNightClock.GetTimeHours"/>) and the sky can
/// never disagree with the sun.
/// </summary>
public static class SkyWeights
{
	/// <summary>Pure: map a TOTAL game-hour to the four slot weights, using the four sky anchors in the config.
	/// Component order is (x = morning, y = noon, z = evening, w = night) so it drops straight into a shader
	/// float4 or your own four-way lerp. Continuous across every anchor including the midnight wrap, so the
	/// crossfade never pops.</summary>
	public static Vector4 WeightsFor( float totalHours, in DayNightConfig cfg )
	{
		float t = totalHours - MathF.Floor( totalHours / 24f ) * 24f;   // hour-of-day 0..24

		float night = cfg.SkyNightHour, morning = cfg.SkyMorningHour;
		float noon = cfg.SkyNoonHour, evening = cfg.SkyEveningHour;

		float m = 0f, n = 0f, e = 0f, ni = 0f;
		if ( t < morning )            // night -> morning
		{
			float s = SkyGrade.Smoothstep( (t - night) / (morning - night) );
			ni = 1f - s; m = s;
		}
		else if ( t < noon )          // morning -> noon
		{
			float s = SkyGrade.Smoothstep( (t - morning) / (noon - morning) );
			m = 1f - s; n = s;
		}
		else if ( t < evening )       // noon -> evening
		{
			float s = SkyGrade.Smoothstep( (t - noon) / (evening - noon) );
			n = 1f - s; e = s;
		}
		else                          // evening -> night (wraps to next midnight)
		{
			float s = SkyGrade.Smoothstep( (t - evening) / (24f - evening) );
			e = 1f - s; ni = s;
		}
		return new Vector4( m, n, e, ni );
	}

	/// <summary>Convenience: <see cref="WeightsFor(float, in DayNightConfig)"/> with the default anchors
	/// (night 0, morning 6, noon 12, evening 18).</summary>
	public static Vector4 WeightsFor( float totalHours )
	{
		var cfg = DayNightConfig.Default;
		return WeightsFor( totalHours, cfg );
	}
}
