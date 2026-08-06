using System;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// The pure grading math: a total game-hour + weather + config in, a sun rotation and four colour grades out.
/// No engine state, no randomness, so a host and every client compute the SAME look from the same clock, and
/// the editor / a headless test can render or check the exact cycle a play session shows.
///
/// ANCHOR-EXACT BY CONSTRUCTION: at <see cref="DayNightConfig.AnchorHours"/> (Clear) every output equals the
/// reference grade in the config, so pinning the anchor reproduces the authored look byte-for-byte. The
/// daytime arc is derived from <see cref="DayNightConfig.SunDirection"/> and lerps FROM the reference colours,
/// and a twilight band smoothstep-blends dusk/dawn to the night grade so there is no hard flip at the horizon.
///
/// This math NEVER touches exposure, shadows, or fog. Apply it and your night is dark because the sun and sky
/// colours are dark, not because tone-mapping moved.
/// </summary>
public static class SkyGrade
{
	/// <summary>Smoothstep 0→1 with clamp, the twilight blend easing (deterministic; no engine state).</summary>
	public static float Smoothstep( float x )
	{
		x = Math.Clamp( x, 0f, 1f );
		return x * x * (3f - 2f * x);
	}

	/// <summary>Compute the sun rotation + all four colour grades for a TOTAL game-hour + weather. Anchor-exact:
	/// at <see cref="DayNightConfig.AnchorHours"/> (Clear) every value equals the config reference grade. Never
	/// computes exposure, that stays whatever your camera set it to.</summary>
	public static void ComputeGrade( float total, WeatherKind weather, in DayNightConfig cfg,
		out Rotation sunRot, out Color sunColor, out Color skyColor, out Color skyTint, out Color envTint )
	{
		var anchor = Rotation.LookAt( cfg.SunDirection.Normal ).Angles();   // reference sun pitch/yaw
		int day = (int)MathF.Floor( total / 24f );
		float t = total - day * 24f;             // hour-of-day 0..24

		float sunrise = cfg.SunriseHour, sunset = cfg.SunsetHour;
		bool isDay = t >= sunrise && t <= sunset;
		float p = isDay ? (t - sunrise) / (sunset - sunrise) : 0f;   // 0 at sunrise .. 1 at sunset
		float daylight = isDay ? MathF.Sin( p * MathF.PI ) : 0f;      // 0 night .. 1 noon

		float pAnchor = (cfg.AnchorHours - sunrise) / (sunset - sunrise);
		float dlAnchor = MathF.Sin( pAnchor * MathF.PI );

		float weatherDim = weather switch
		{
			WeatherKind.Rain => cfg.WeatherDimRain,
			WeatherKind.Cloudy => cfg.WeatherDimCloudy,
			_ => 1f,
		};

		if ( isDay )
		{
			// ── DAYTIME (anchor-exact by construction) ──
			// ROTATION: pitch grows from the near-horizon value to the derived noon max, threading the derived
			// anchor pitch; yaw sweeps east→west across the day, threading the anchor yaw.
			float pitchSpan = (anchor.pitch - cfg.HorizonPitch) / dlAnchor;
			float pitch = cfg.HorizonPitch + daylight * pitchSpan;
			float yaw = anchor.yaw + (p - pAnchor) * cfg.YawSpan;
			sunRot = Rotation.From( pitch, yaw, 0f );

			// KEY COLOUR: lerp from the reference SunColor toward whiter noon / warmer horizon, anchored so the
			// value EQUALS SunColor exactly at the anchor daylight.
			float rel = daylight - dlAnchor;   // 0 at anchor, + toward noon, - toward horizon
			sunColor = rel >= 0f
				? Color.Lerp( cfg.SunColor, cfg.NoonKey, dlAnchor < 1f ? rel / (1f - dlAnchor) : 0f )
				: Color.Lerp( cfg.SunColor, cfg.HorizonKey, -rel / dlAnchor );
			sunColor *= weatherDim;

			// AMBIENT / SKY / ENVMAP: scale the reference grade by daylight (anchor == 1 == the exact reference).
			float skyScale = dlAnchor > 0f ? MathF.Min( daylight / dlAnchor, 1.15f ) : 0f;
			skyColor = cfg.SkyAmbient * skyScale;
			skyTint = cfg.SkyTint * skyScale;
			envTint = cfg.EnvmapTint * skyScale;
			return;
		}

		// ── NIGHT + TWILIGHT. The fixed-low-moon night grade is the deep-night target; a TWILIGHT band
		// TwilightHours past sunset (and before sunrise) smoothstep-lerps the sun rotation AND all four colours
		// from the HORIZON-edge values (what the day arc reaches at sunrise/sunset, daylight→0) to the night
		// values, so dusk reads as the sun continuing its arc below the horizon, not a hard flip. At w=0 it
		// equals the day boundary (continuous with daytime); at w=1 it equals deep night. ──
		float nightPitch = cfg.NightPitch;
		float nightYaw = anchor.yaw + cfg.YawSpan * 0.6f;   // fixed low moon direction
		Color nightSun = cfg.NightKey;
		Color nightSky = cfg.NightAmbient;
		Color nightTint = cfg.NightSkyTint;
		Color nightEnv = cfg.NightEnvTint;

		// Twilight blend factor: 0 = horizon-edge look, 1 = deep night. Evening band (just after sunset) and the
		// mirror morning band (just before sunrise); outside the bands it is 1 (deep night).
		float tw = cfg.TwilightHours;
		float w = 1f;
		bool evening = t > sunset && t <= sunset + tw;
		bool morning = t >= sunrise - tw && t < sunrise;
		if ( evening ) w = Smoothstep( (t - sunset) / tw );
		else if ( morning ) w = Smoothstep( (sunrise - t) / tw );

		if ( w >= 1f )
		{
			// deep night (no blend), the fixed night grade.
			sunRot = Rotation.From( nightPitch, nightYaw, 0f );
			sunColor = nightSun;
			skyColor = nightSky;
			skyTint = nightTint;
			envTint = nightEnv;
			return;
		}

		// HORIZON-edge grade (the day arc evaluated at the sunrise/sunset boundary, daylight→0): pitch sits at the
		// near-horizon value, yaw at that boundary's swept position, sun the deep warm HorizonKey, sky/ambient →0.
		float boundaryP = evening ? 1f : 0f;   // sunset p=1, sunrise p=0
		float horizonPitch = cfg.HorizonPitch;
		float horizonYaw = anchor.yaw + (boundaryP - pAnchor) * cfg.YawSpan;
		Color horizonSun = cfg.HorizonKey * weatherDim;

		sunRot = Rotation.From(
			MathX.Lerp( horizonPitch, nightPitch, w ),
			MathX.LerpDegrees( horizonYaw, nightYaw, w ),
			0f );
		sunColor = Color.Lerp( horizonSun, nightSun, w );
		skyColor = Color.Lerp( Color.Black, nightSky, w );   // day-edge ambient is ~0; rise to the night floor
		skyTint = Color.Lerp( Color.Black, nightTint, w );
		envTint = Color.Lerp( Color.Black, nightEnv, w );
	}

	/// <summary>PURE: the clock-advance rate MULTIPLIER for a given hour-of-day (0..24), applied to the base
	/// pace in <see cref="DayNightClock"/>. The daylight arc runs slower (<see cref="DayNightConfig.DayRateScale"/>)
	/// so the day lasts longer, while night keeps rate 1 so night real-time is preserved exactly. The two ramps
	/// live INSIDE the daylight window (a <see cref="DayNightConfig.TwilightHours"/>-wide smoothstep at each edge),
	/// reaching rate 1 exactly at sunrise/sunset so there is no rate discontinuity at the night boundary. Bounded
	/// to [DayRateScale, 1], pure, so host and every client derive the same rate and stay in lockstep.</summary>
	public static float ClockRateScale( float hourOfDay, in DayNightConfig cfg )
	{
		float sunrise = cfg.SunriseHour, sunset = cfg.SunsetHour;
		float tw = cfg.TwilightHours;
		float dayScale = cfg.DayRateScale;
		const float nightScale = 1f;

		float t = hourOfDay - MathF.Floor( hourOfDay / 24f ) * 24f;   // wrap to 0..24 for callers passing total hours

		// Full daylight interior: the slow pace.
		if ( t >= sunrise + tw && t <= sunset - tw ) return dayScale;
		// Dawn ramp (inside the day window): rate 1 at sunrise → slow by sunrise+tw. Night stays exact because the
		// ramp is spent within daylight, not stolen from the night arc.
		if ( t >= sunrise && t < sunrise + tw ) return MathX.Lerp( nightScale, dayScale, Smoothstep( (t - sunrise) / tw ) );
		// Dusk ramp (inside the day window): slow until sunset-tw → rate 1 exactly at sunset, matching night.
		if ( t > sunset - tw && t <= sunset ) return MathX.Lerp( dayScale, nightScale, Smoothstep( (t - (sunset - tw)) / tw ) );
		// Night: unchanged rate, so the night arc's real-time length is preserved exactly.
		return nightScale;
	}

	/// <summary>Apply the computed grade to a specific sun/sky/envmap trio. Leaves exposure / shadows / fog
	/// alone. Pass null for any of sky/env you do not have. This is the ONLY method here that writes engine
	/// state; everything above is pure.</summary>
	public static void ApplyGradeTo( DirectionalLight sun, SkyBox2D sky, EnvmapProbe env,
		float total, WeatherKind weather, in DayNightConfig cfg )
	{
		if ( !sun.IsValid() ) return;
		ComputeGrade( total, weather, cfg, out var rot, out var sunColor, out var skyColor, out var skyTint, out var envTint );
		sun.WorldRotation = rot;
		sun.LightColor = sunColor;
		sun.SkyColor = skyColor;
		if ( sky.IsValid() ) sky.Tint = skyTint;
		if ( env.IsValid() ) env.TintColor = envTint;
	}
}
