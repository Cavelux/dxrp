using System;
using System.Linq;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// OPTIONAL convenience driver. Put it on the same GameObject as your scene's DirectionalLight and it applies
/// the day/night grade every frame from the <see cref="DayNightClock"/> in the scene: sun rotation + colour,
/// SkyBox2D tint, and EnvmapProbe tint. It resolves the sun from its own GameObject and the sky/envmap from
/// the scene (first of each). Delete this file if you would rather call <see cref="SkyGrade.ApplyGradeTo"/>
/// yourself.
///
/// It never touches exposure/shadows/fog, so it will not fight a locked-exposure camera.
///
/// The <see cref="ShowCycle"/> seam generalizes the source game's "am I possessing a character?" gate. While
/// it returns false the driver holds the stable ANCHOR grade (a fully-lit, non-moving model-viewer look) and
/// leaves the sky at your authoring backdrop; the moment it returns true the live cycle resumes at the true
/// game time (the clock keeps ticking underneath regardless). Default: always show the cycle.
/// </summary>
[Title( "Day Night Driver" )]
[Category( "Field Guide" )]
[Icon( "wb_sunny" )]
public sealed class DayNightDriver : Component
{
	/// <summary>Tuning. Defaults to the reference grade; set it to match your clock's config.</summary>
	public DayNightConfig Config { get; set; } = DayNightConfig.Default;

	/// <summary>Return false to hold the stable anchor grade instead of the live cycle (e.g. while the local
	/// player is in a menu / god-camera authoring mode). Null-safe: null means always show the cycle.</summary>
	public Func<bool> ShowCycle { get; set; }

	/// <summary>Optional sky tint to force while <see cref="ShowCycle"/> is false (your authoring backdrop). If
	/// null the anchor sky tint is used.</summary>
	public Color? AuthoringSkyTint { get; set; }

	DirectionalLight _sun;
	SkyBox2D _sky;
	EnvmapProbe _env;
	DayNightClock _clock;
	DayNightClock Clock => _clock ??= DayNightClock.For( Scene );

	protected override void OnEnabled()
	{
		_sun = GetComponent<DirectionalLight>();
		_sky = Scene.GetAllComponents<SkyBox2D>().FirstOrDefault();
		_env = Scene.GetAllComponents<EnvmapProbe>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;                 // editor renders whatever you authored; the cycle is play-mode
		var clock = Clock;
		if ( clock is null || !_sun.IsValid() ) return;

		if ( ShowCycle is not null && !ShowCycle() )
		{
			// Stable anchor look (fully lit, not moving). Pass null for the sky so the anchor warm sky tint is not
			// applied, then force the authoring backdrop tint.
			SkyGrade.ApplyGradeTo( _sun, null, _env, Config.AnchorHours, WeatherKind.Clear, Config );
			if ( _sky.IsValid() ) _sky.Tint = AuthoringSkyTint ?? Config.SkyTint;
			return;
		}

		float total = clock.GetTimeHours();
		var weather = clock.EffectiveWeather( (int)MathF.Floor( total / 24f ) );
		SkyGrade.ApplyGradeTo( _sun, _sky, _env, total, weather, Config );
	}
}
