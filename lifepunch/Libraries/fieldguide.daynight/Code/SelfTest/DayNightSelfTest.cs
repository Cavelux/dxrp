using System;
using System.Collections.Generic;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// A pure self-test battery for the kit's deterministic math, ported from the source game's pure-test suite.
/// None of it needs a running scene or networking, it exercises <see cref="SkyGrade"/>, <see cref="SkyWeights"/>,
/// <see cref="WeatherRoll"/>, and <see cref="TimeMath"/> against the default config and returns a pass/fail
/// report. It is the kit's compile-in-isolation smoke proof and doubles as executable documentation.
///
/// Run it from the s&amp;box console with <c>fg_daynight_selftest</c>, or call <see cref="RunAll"/> from your own
/// harness. Every case is a pure function of the config, so a green run here is meaningful without the editor.
/// </summary>
public static class DayNightSelfTest
{
	/// <summary>One test result.</summary>
	public readonly record struct Case( string Name, bool Passed, string Detail );

	/// <summary>Run every case against <see cref="DayNightConfig.Default"/>. Returns the per-case results; the
	/// caller decides how to surface them.</summary>
	public static List<Case> RunAll()
	{
		var cfg = DayNightConfig.Default;
		return new List<Case>
		{
			WeatherDeterminism( cfg ),
			AnchorExact( cfg ),
			TwilightContinuity( cfg ),
			RateNightAndMidday( cfg ),
			RateContinuousAtTwilight( cfg ),
			RatioIs3x( cfg ),
			SkyWeightsPartition( cfg ),
			TimeSetPreservesDay( cfg ),
		};
	}

	// ── weather: deterministic and seed-sensitive ──
	static Case WeatherDeterminism( DayNightConfig cfg )
	{
		const int seed = 71237;
		bool stable = true;
		for ( int day = 0; day < 16; day++ )
		{
			var w = WeatherRoll.For( seed, day );
			if ( w != WeatherRoll.For( seed, day ) ) { stable = false; break; }
			if ( !System.Enum.IsDefined( typeof( WeatherKind ), w ) ) { stable = false; break; }
		}
		bool seedSensitive = false;
		for ( int day = 0; day < 32 && !seedSensitive; day++ )
			if ( WeatherRoll.For( seed, day ) != WeatherRoll.For( seed + 1, day ) )
				seedSensitive = true;
		bool ok = stable && seedSensitive;
		return new( "weather_deterministic", ok, $"stable={stable} seedSensitive={seedSensitive}" );
	}

	// ── grade: anchor-exact ──
	static Case AnchorExact( DayNightConfig cfg )
	{
		SkyGrade.ComputeGrade( cfg.AnchorHours, WeatherKind.Clear, cfg,
			out var rot, out var sun, out _, out _, out _ );
		var refRot = Rotation.LookAt( cfg.SunDirection.Normal );
		float dot = Math.Clamp( Vector3.Dot( rot.Forward.Normal, refRot.Forward.Normal ), -1f, 1f );
		float ang = MathF.Acos( dot ) * (180f / MathF.PI);
		bool rotOk = ang < 0.05f;
		bool sunOk = MathF.Abs( sun.r - cfg.SunColor.r ) < 1e-3f
			&& MathF.Abs( sun.g - cfg.SunColor.g ) < 1e-3f
			&& MathF.Abs( sun.b - cfg.SunColor.b ) < 1e-3f;
		bool ok = rotOk && sunOk;
		return new( "grade_anchor_exact", ok, $"rotDeltaDeg={ang:0.000} sunKeyMatches={sunOk}" );
	}

	// ── grade: twilight continuity (no teleport across sunset) ──
	static Case TwilightContinuity( DayNightConfig cfg )
	{
		float maxAngleDeg = 0f, maxColorStep = 0f;
		Vector3 prevDir = Vector3.Zero;
		Color prevSun = default;
		bool first = true;
		for ( float t = 17.5f; t <= 19.5f + 1e-4f; t += 0.1f )
		{
			SkyGrade.ComputeGrade( t, WeatherKind.Clear, cfg, out var rot, out var sun, out _, out _, out _ );
			var dir = rot.Forward;
			if ( !first )
			{
				float d = Math.Clamp( Vector3.Dot( dir.Normal, prevDir.Normal ), -1f, 1f );
				float ang = MathF.Acos( d ) * (180f / MathF.PI);
				if ( ang > maxAngleDeg ) maxAngleDeg = ang;
				float cstep = MathF.Max( MathF.Abs( sun.r - prevSun.r ),
					MathF.Max( MathF.Abs( sun.g - prevSun.g ), MathF.Abs( sun.b - prevSun.b ) ) );
				if ( cstep > maxColorStep ) maxColorStep = cstep;
			}
			prevDir = dir; prevSun = sun; first = false;
		}
		bool ok = maxAngleDeg < 16f && maxColorStep < 0.45f;
		return new( "grade_twilight_continuity", ok, $"maxStepDeg={maxAngleDeg:0.0} maxColorStep={maxColorStep:0.00} (thresholds 16, 0.45)" );
	}

	// ── rate: night == 1, midday == DayRateScale ──
	static Case RateNightAndMidday( DayNightConfig cfg )
	{
		float n0 = SkyGrade.ClockRateScale( 0f, cfg );
		float n3 = SkyGrade.ClockRateScale( 3f, cfg );
		float n21 = SkyGrade.ClockRateScale( 21f, cfg );
		float mid = SkyGrade.ClockRateScale( 12f, cfg );
		bool night = MathF.Abs( n0 - 1f ) < 1e-4f && MathF.Abs( n3 - 1f ) < 1e-4f && MathF.Abs( n21 - 1f ) < 1e-4f;
		bool midday = MathF.Abs( mid - cfg.DayRateScale ) < 1e-4f;
		bool ok = night && midday;
		return new( "rate_night_and_midday", ok, $"night(0/3/21)={n0:0.000}/{n3:0.000}/{n21:0.000} midday={mid:0.000} (want {cfg.DayRateScale:0.000})" );
	}

	// ── rate: continuous at the twilight boundary ──
	static Case RateContinuousAtTwilight( DayNightConfig cfg )
	{
		const float eps = 0.01f;
		float atSunrise = SkyGrade.ClockRateScale( cfg.SunriseHour, cfg );
		float atSunset = SkyGrade.ClockRateScale( cfg.SunsetHour, cfg );
		bool endsAtOne = MathF.Abs( atSunrise - 1f ) < 1e-3f && MathF.Abs( atSunset - 1f ) < 1e-3f;
		float srStep = MathF.Abs( SkyGrade.ClockRateScale( cfg.SunriseHour + eps, cfg ) - SkyGrade.ClockRateScale( cfg.SunriseHour - eps, cfg ) );
		float ssStep = MathF.Abs( SkyGrade.ClockRateScale( cfg.SunsetHour - eps, cfg ) - SkyGrade.ClockRateScale( cfg.SunsetHour + eps, cfg ) );
		bool ok = endsAtOne && srStep < 5e-3f && ssStep < 5e-3f;
		return new( "rate_continuous_at_twilight", ok, $"atSunrise={atSunrise:0.000} atSunset={atSunset:0.000} srStep={srStep:0.0000} ssStep={ssStep:0.0000}" );
	}

	// ── rate: effective day:night real-time ratio == 3.0 ──
	static Case RatioIs3x( DayNightConfig cfg )
	{
		const int n = 120000;
		float a = cfg.SunriseHour, b = cfg.SunsetHour;
		float h = (b - a) / n;
		double dayRealtime = 0.0;
		for ( int i = 0; i < n; i++ )
		{
			float t = a + (i + 0.5f) * h;
			dayRealtime += 1.0 / SkyGrade.ClockRateScale( t, cfg );
		}
		dayRealtime *= h;
		float nightHours = 24f - (b - a);
		double ratio = dayRealtime / nightHours;
		bool ok = System.Math.Abs( ratio - 3.0 ) <= 0.03;
		return new( "rate_ratio_is_3x", ok, $"day:night ratio={ratio:0.0000} (want 3.0 +/-1%)" );
	}

	// ── sky weights: partition of unity, adjacent-pair only ──
	static Case SkyWeightsPartition( DayNightConfig cfg )
	{
		bool ok = true;
		string detail = "sum==1, <=2 nonzero across 24h";
		for ( float t = 0f; t < 24f; t += 0.05f )
		{
			var w = SkyWeights.WeightsFor( t, cfg );
			float sum = w.x + w.y + w.z + w.w;
			if ( MathF.Abs( sum - 1f ) > 1e-3f ) { ok = false; detail = $"sum={sum:0.000} at t={t:0.00}"; break; }
			int nonzero = (w.x > 1e-4f ? 1 : 0) + (w.y > 1e-4f ? 1 : 0) + (w.z > 1e-4f ? 1 : 0) + (w.w > 1e-4f ? 1 : 0);
			if ( nonzero > 2 ) { ok = false; detail = $"{nonzero} nonzero weights at t={t:0.00}"; break; }
		}
		return new( "sky_weights_partition", ok, detail );
	}

	// ── time-set preserves the day index ──
	static Case TimeSetPreservesDay( DayNightConfig cfg )
	{
		bool preservesDay = TimeMath.ComputeSetHour( 39.5f, 6f ) == 30f;   // day 1 15:30 → 06:00, still day 1
		bool clampsHigh = TimeMath.ComputeSetHour( 39.5f, 99f ) == 48f;
		bool clampsLow = TimeMath.ComputeSetHour( 39.5f, -5f ) == 24f;
		bool sliderMid = MathF.Abs( TimeMath.ComputeSliderHour( 0.5f ) - 12f ) < 1e-3f;
		// The slider must never hand back a value past the end of the day: one in-game minute is not exactly
		// representable, so 1440 quantized steps land at 24.000002 unless the result is clamped.
		bool sliderRange = TimeMath.ComputeSliderHour( 1f ) <= 24f && TimeMath.ComputeSliderHour( 0f ) >= 0f;
		bool ok = preservesDay && clampsHigh && clampsLow && sliderMid && sliderRange;
		return new( "time_set_preserves_day", ok, $"preservesDay={preservesDay} clampHi={clampsHigh} clampLo={clampsLow} sliderMid={sliderMid} sliderRange={sliderRange}" );
	}

	/// <summary>Console entry: run the battery and print a one-line-per-case report plus a summary.</summary>
	[ConCmd( "fg_daynight_selftest" )]
	public static void RunFromConsole()
	{
		int passed = 0, failed = 0;
		foreach ( var c in RunAll() )
		{
			if ( c.Passed ) { passed++; Log.Info( $"  ok    {c.Name}  {c.Detail}" ); }
			else { failed++; Log.Warning( $"  FAIL  {c.Name}  {c.Detail}" ); }
		}
		if ( failed == 0 ) Log.Info( $"fg_daynight_selftest: PASSED {passed}/{passed}" );
		else Log.Warning( $"fg_daynight_selftest: FAILED {failed} of {passed + failed}" );
	}
}
