using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// Wires the demo scene in code so the whole kit is exercised from one component.
///
/// The scene it builds is the kit's hero case: a lit ground plane with a few shapes on it, a
/// <see cref="DayNightClock"/> running the time, and a <see cref="DayNightDriver"/> on the scene's
/// DirectionalLight. Nothing else. That is enough, because the kit's product IS the light: the sun sweeps,
/// the shadows swing across the ground, the colour grade warms into dusk and drops into a genuinely dark
/// night, and none of it touches camera exposure. The shapes exist to catch that light and throw the
/// shadows that make the sweep readable; a bare plane shows almost nothing.
///
/// Two surfaces sit on top. The hint card (left) is up from the first frame and prints the live sky
/// weights, which is the only way to SEE the sky seam in a kit that deliberately ships no sky art. The
/// time panel (right) is the kit's dev tuning surface, opened here because a demo whose point is
/// "drive the cycle" should not hide the control behind a keypress.
///
/// Everything it builds ships with the engine: the dev primitives and the default material. The kit adds
/// no art of its own.
///
/// DEMO CONTENT IS INERT BY CONSTRUCTION (library law 11). Two things make that true here rather than by
/// instruction. First, the kit ships no scanned GameResource, so there is no demo content that can load
/// itself into a consumer's game the way a stray demo asset would. Second, the demo's UI is gated on
/// <see cref="DemoActive"/>, a flag ONLY this bootstrap sets: a consumer who forgets to delete Code/Demo,
/// and who somehow ends up with the hint card component in a scene, still renders nothing.
///
/// Not part of the kit's runtime surface: delete Code/Demo when you drop the kit into your own project.
/// </summary>
[Title( "Day Night Demo Bootstrap" )]
[Category( "Field Guide · Day Night" )]
[Icon( "auto_awesome" )]
public sealed class DayNightDemoBootstrap : Component
{
	/// <summary>
	/// True once this bootstrap has run in this session. The demo's own UI checks it before rendering, so
	/// demo content cannot appear in a consumer's game just because Code/Demo was left in the project.
	/// Nothing outside Code/Demo reads or writes it.
	/// </summary>
	public static bool DemoActive { get; private set; }

	/// <summary>The world seed the deterministic weather roll hashes against. Any int works; this one is
	/// just a fixed number so the demo rolls the same weather every run and two people comparing notes see
	/// the same days.</summary>
	[Property] public int DemoWorldSeed { get; set; } = 20260731;

	/// <summary>Real minutes per in-game day at the night pace. Short by default: the whole point of the
	/// demo is watching a full cycle, and the shipped default of 20 minutes is a long wait for that.</summary>
	[Property] public float DemoDayLengthMinutes { get; set; } = 4f;

	/// <summary>Game-hour the demo starts at. Mid-morning, so the first thing on screen is a lit scene with
	/// a sun that is visibly climbing rather than a black frame.</summary>
	[Property] public float DemoStartHour { get; set; } = 8.5f;

	const string FallbackMaterial = "materials/default.vmat";

	/// <summary>One shape in the demo cluster, sized in ENGINE UNITS per axis. The builder divides the size
	/// by the model's own bounds to get the scale, so the table below reads as real dimensions and survives
	/// the engine changing what a dev primitive measures (the shipped box is 50 units, the sphere 64).</summary>
	readonly record struct Shape( string ModelPath, Vector3 SizeUnits, Vector3 Position, Color Tint );

	/// <summary>
	/// The cluster. A tall slab, a low wall, two blocks and a ball, spread out and at different heights.
	///
	/// The shapes are chosen for their SHADOWS, not their looks. A tall thin slab throws a long finger that
	/// swings a quarter turn across the plane over one day, which is the single clearest read on "the sun is
	/// actually moving"; the low wall gives a hard edge for the terminator to crawl along at dawn and dusk;
	/// the ball is the only curved surface, so it is where the warm key and the cool sky fill are visibly
	/// two different colours rather than one flat tone.
	/// </summary>
	static readonly Shape[] Cluster =
	{
		new( "models/dev/box.vmdl",    new Vector3(  24f,  24f, 260f ), new Vector3(   0f,    0f, 130f ), new Color( 0.78f, 0.76f, 0.72f ) ),
		new( "models/dev/box.vmdl",    new Vector3( 420f,  28f,  90f ), new Vector3( -60f, -320f,  45f ), new Color( 0.62f, 0.58f, 0.54f ) ),
		new( "models/dev/box.vmdl",    new Vector3( 110f, 110f, 110f ), new Vector3( 300f,  140f,  55f ), new Color( 0.70f, 0.55f, 0.42f ) ),
		new( "models/dev/box.vmdl",    new Vector3(  70f,  70f, 170f ), new Vector3( 190f, -220f,  85f ), new Color( 0.55f, 0.60f, 0.68f ) ),
		new( "models/dev/sphere.vmdl", new Vector3( 150f, 150f, 150f ), new Vector3( -280f,  180f,  75f ), new Color( 0.80f, 0.80f, 0.82f ) ),
	};

	DayNightClock _clock;
	RainStreaks _rain;
	CameraComponent _camera;

	protected override void OnStart()
	{
		DemoActive = true;

		_camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		_clock = EnsureClock();
		BuildCluster();
		BuildRain();
		BuildUi();

		Log.Info( $"[daynight] demo ready. Day length {DemoDayLengthMinutes:0.#} real minutes, seed {DemoWorldSeed}. "
			+ "N opens the time panel, H hides the hint card." );
	}

	/// <summary>
	/// The clock, configured for a demo rather than for a game.
	///
	/// The one non-default value is the day length. Everything else is <see cref="DayNightConfig.Default"/>
	/// verbatim, on purpose: a demo that tunes the grade is showing you ITS look, not the kit's, and the
	/// shipped default is the reference grade a consumer gets on install.
	/// </summary>
	DayNightClock EnsureClock()
	{
		var clock = DayNightClock.For( Scene ) ?? Components.GetOrCreate<DayNightClock>();

		var cfg = DayNightConfig.Default;
		cfg.DayLengthMinutes = MathF.Max( 0.25f, DemoDayLengthMinutes );
		cfg.StartHours = Math.Clamp( DemoStartHour, 0f, 24f );
		clock.Config = cfg;
		clock.WorldSeed = DemoWorldSeed;

		// The driver has to agree with the clock: same config on both, which is exactly what the README
		// tells a consumer to do. Set it here rather than in the scene file so there is one source of truth.
		foreach ( var driver in Scene.GetAllComponents<DayNightDriver>() )
			driver.Config = cfg;

		return clock;
	}

	// ---- the shapes that catch the light ----

	void BuildCluster()
	{
		var root = Scene.CreateObject();
		root.Name = "Demo Shapes";

		for ( int i = 0; i < Cluster.Length; i++ )
			BuildShape( root, $"Shape {i + 1}", Cluster[i] );
	}

	void BuildShape( GameObject parent, string name, Shape shape )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.SetParent( parent, false );
		go.LocalPosition = shape.Position;
		go.LocalRotation = Rotation.Identity;

		var renderer = go.Components.Create<ModelRenderer>();
		var model = Model.Load( shape.ModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[daynight] demo model '{shape.ModelPath}' did not load; '{name}' will be invisible." );
			return;
		}
		renderer.Model = model;

		var bounds = model.Bounds.Size;
		go.LocalScale = new Vector3(
			bounds.x > 0.001f ? shape.SizeUnits.x / bounds.x : 1f,
			bounds.y > 0.001f ? shape.SizeUnits.y / bounds.y : 1f,
			bounds.z > 0.001f ? shape.SizeUnits.z / bounds.z : 1f );

		// The engine's models/dev primitives render as missing-material magenta unless a real material is
		// forced on, which would swallow the grade this whole demo exists to show.
		var mat = Material.Load( FallbackMaterial );
		if ( mat is not null ) renderer.MaterialOverride = mat;
		renderer.Tint = shape.Tint;
	}

	// ---- the optional rain module, so a Rain day is visible ----

	void BuildRain()
	{
		var go = Scene.CreateObject();
		go.Name = "Demo Rain";

		_rain = go.Components.Create<RainStreaks>();

		// The shower centres on the camera, which is the seam's whole point: the kit never reaches for your
		// player or camera type, you hand it a position.
		_rain.Center = () => _camera.IsValid() ? _camera.WorldPosition : Vector3.Up * 200f;
	}

	protected override void OnUpdate()
	{
		// Drive the optional rain module from the clock's weather, which is the two-line wiring the README
		// describes. Cheap to call every frame.
		if ( _rain.IsValid() && _clock.IsValid() )
			_rain.SetRaining( _clock.CurrentWeather == WeatherKind.Rain );
	}

	// ---- screen UI ----

	void BuildUi()
	{
		// One ScreenPanel per PanelComponent (the World Builder UI idiom). Built in code so the demo scene
		// needs no razor wiring.
		var hintHost = Scene.CreateObject();
		hintHost.Name = "Day Night Hint";
		hintHost.Components.Create<ScreenPanel>();
		hintHost.Components.Create<DayNightHintCard>();

		var panelHost = Scene.CreateObject();
		panelHost.Name = "Day Night UI";
		panelHost.Components.Create<ScreenPanel>();

		// Open on arrival. Driving the cycle is what this scene is FOR, so making the visitor find the key
		// first is a toll booth on the way to the point. N and the header x still close it. The panel reads
		// this on its first update, after every OnStart in the frame, so setting it here always lands.
		var panel = panelHost.Components.Create<DayNightPanel>();
		panel.OpenOnStart = true;
	}
}
