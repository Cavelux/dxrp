using System;

namespace FieldGuide.DayNight;

/// <summary>
/// All the tuning for the day/night cycle in one passed-in struct (library law: config over constants).
/// Every pure math call (<see cref="SkyGrade"/>, <see cref="SkyWeights"/>) and the clock accumulate take a
/// config by reference, so a library consumer never reads a project-global static. Grab
/// <see cref="Default"/> and tweak the fields you care about.
///
/// The values in <see cref="Default"/> are the exact reference grade from the source game: an afternoon
/// anchor at 15:00, a symmetric 12 h day (sunrise 6, sunset 18), a warm HDR sun and sky, and a deep-blue
/// night. The arc is DERIVED from <see cref="SunDirection"/> (its noon pitch/yaw come from LookAt→Angles),
/// so nudging <see cref="SunDirection"/> moves the whole arc without touching the pitch/yaw fields.
///
/// EXPOSURE IS NOT IN HERE ON PURPOSE. The whole diurnal look comes from sun rotation + light/sky/envmap
/// colours, never from tone-mapping. If your game locks camera exposure, night renders genuinely dark under
/// it; this kit never writes exposure, so it will not fight your camera. See the README.
/// </summary>
public struct DayNightConfig
{
	// ── daylight window + pace ──
	/// <summary>Game-hour daylight begins. Default 6, giving a symmetric 12 h day with
	/// <see cref="SunsetHour"/>.</summary>
	public float SunriseHour;
	/// <summary>Game-hour daylight ends. Default 18.</summary>
	public float SunsetHour;
	/// <summary>Real-time MINUTES per in-game day at the NIGHT pace. Because <see cref="DayRateScale"/> slows
	/// the daylight arc, this is the night-arc pace, not the whole-cycle length.</summary>
	public float DayLengthMinutes;
	/// <summary>Clock-rate multiplier applied through full daylight so the day lasts longer than the night;
	/// night stays at rate 1 so its real-time length is preserved exactly. The default is solved so the
	/// effective day:night real-time ratio is 3.0 (see <see cref="SkyGrade.ClockRateScale"/> and the ratio
	/// self-test). Change the daylight window or twilight width and re-solve this against your target.</summary>
	public float DayRateScale;
	/// <summary>Smoothstep ramp width (game-hours) at each daylight edge, shared by the pace ramp and the
	/// twilight colour blend so the pace shift is masked by the sky already transitioning.</summary>
	public float TwilightHours;

	// ── session start ──
	/// <summary>Game-hour a fresh session's clock starts at.</summary>
	public float StartHours;
	/// <summary>Whether a fresh session starts paused (held at <see cref="StartHours"/> until something sets
	/// the time). Default false = the clock runs.</summary>
	public bool StartPaused;

	// ── sun-arc shape ──
	/// <summary>Near-horizon sun pitch at sunrise/sunset.</summary>
	public float HorizonPitch;
	/// <summary>Total east→west yaw the sun sweeps across the day.</summary>
	public float YawSpan;
	/// <summary>Fixed low-moon pitch for deep night.</summary>
	public float NightPitch;
	/// <summary>Reference sun direction. The arc's noon pitch/yaw are derived from this (LookAt→Angles), so
	/// the arc always threads the current reference sun.</summary>
	public Vector3 SunDirection;

	// ── daytime reference colours (the anchor grade) ──
	/// <summary>Reference sun key colour at the anchor daylight. The daytime lerp is anchored so the sun key
	/// EQUALS this exactly at <see cref="AnchorHours"/>.</summary>
	public Color SunColor;
	/// <summary>Reference ambient (sky-fill) colour at the anchor daylight.</summary>
	public Color SkyAmbient;
	/// <summary>Reference SkyBox2D tint at the anchor daylight.</summary>
	public Color SkyTint;
	/// <summary>Reference EnvmapProbe tint at the anchor daylight.</summary>
	public Color EnvmapTint;
	/// <summary>The daylight hour the reference grade is authored at. At this hour (Clear weather) the computed
	/// grade equals the reference values above exactly.</summary>
	public float AnchorHours;

	// ── diurnal key targets ──
	/// <summary>Sun key the daytime lerp reaches toward noon.</summary>
	public Color NoonKey;
	/// <summary>Deep warm sun key at the horizon edge (sunrise/sunset).</summary>
	public Color HorizonKey;
	/// <summary>Deep-night sun key.</summary>
	public Color NightKey;
	/// <summary>Deep-night ambient (SkyColor) fill.</summary>
	public Color NightAmbient;
	/// <summary>Deep-night SkyBox2D tint.</summary>
	public Color NightSkyTint;
	/// <summary>Deep-night EnvmapProbe tint.</summary>
	public Color NightEnvTint;

	// ── weather dimming ──
	/// <summary>Sun-key dim multiplier under Cloudy weather (1 = no dim).</summary>
	public float WeatherDimCloudy;
	/// <summary>Sun-key dim multiplier under Rain weather.</summary>
	public float WeatherDimRain;

	// ── four-slot sky anchors (for SkyWeights, the sky seam) ──
	/// <summary>Hour-of-day anchors for the four sky slots the consumer crossfades (night / morning / noon /
	/// evening). Evenly 6 h apart by default and aligned to sunrise/sunset so every segment is a clean
	/// adjacent-pair crossfade and the midnight wrap is continuous.</summary>
	public float SkyNightHour;
	/// <summary>Hour-of-day the MORNING sky slot owns outright (weight 1). Default 6, at sunrise.</summary>
	public float SkyMorningHour;
	/// <summary>Hour-of-day the NOON sky slot owns outright (weight 1). Default 12.</summary>
	public float SkyNoonHour;
	/// <summary>Hour-of-day the EVENING sky slot owns outright (weight 1). Default 18, at sunset.</summary>
	public float SkyEveningHour;

	/// <summary>The reference grade lifted verbatim from the source game. Afternoon anchor, symmetric 12 h day,
	/// 20 real-minute night pace, day 3x longer than night, warm HDR daylight, deep-blue night.</summary>
	public static DayNightConfig Default => new()
	{
		SunriseHour = 6f,
		SunsetHour = 18f,
		DayLengthMinutes = 20f,
		DayRateScale = 0.31494221f,   // solved so day:night == 3.0; re-solve if the window/twilight change
		TwilightHours = 0.75f,

		StartHours = 7f,
		StartPaused = false,

		HorizonPitch = 3f,
		YawSpan = 150f,
		NightPitch = 34f,
		SunDirection = new Vector3( 0.35f, 0.62f, -0.70f ),

		SunColor = new Color( 1.72f, 1.50f, 1.14f ),
		SkyAmbient = new Color( 0.92f, 0.84f, 0.68f ),
		SkyTint = new Color( 1.30f, 1.24f, 1.12f ),
		EnvmapTint = new Color( 1.02f, 0.90f, 0.70f ),
		AnchorHours = 15f,

		NoonKey = new Color( 1.95f, 1.85f, 1.60f ),
		HorizonKey = new Color( 2.05f, 1.05f, 0.52f ),
		NightKey = new Color( 0.10f, 0.14f, 0.24f ),
		NightAmbient = new Color( 0.05f, 0.07f, 0.12f ),
		NightSkyTint = new Color( 0.05f, 0.06f, 0.10f ),
		NightEnvTint = new Color( 0.06f, 0.07f, 0.11f ),

		WeatherDimCloudy = 0.62f,
		WeatherDimRain = 0.40f,

		SkyNightHour = 0f,
		SkyMorningHour = 6f,
		SkyNoonHour = 12f,
		SkyEveningHour = 18f,
	};
}
