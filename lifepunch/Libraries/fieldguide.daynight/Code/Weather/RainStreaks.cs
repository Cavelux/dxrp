using System;
using System.Collections.Generic;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// OPTIONAL cosmetic rain module (delete the Weather/ folder if you don't want it). A cheap box-streak shower
/// centred on a point you provide, so the deterministic Rain weather is visible without pulling in your
/// player or camera types. It uses only the engine dev box primitive and an xorshift jitter (no System.Random,
/// no gameplay), and it is entirely client-local.
///
/// Wire it in two lines: set <see cref="Center"/> to a delegate returning where the shower should sit (usually
/// the local player or camera position, in engine units), and each frame call <see cref="SetRaining"/> with
/// whether the current weather is <see cref="WeatherKind.Rain"/> (ask your <see cref="DayNightClock"/>). Or
/// just add it to a GameObject and set <see cref="Center"/>; a sibling script can call SetRaining.
/// </summary>
[Title( "Rain Streaks" )]
[Category( "Field Guide" )]
[Icon( "grain" )]
public sealed class RainStreaks : Component
{
	/// <summary>Where the shower centres (engine units). Defaults to this component's own world position.</summary>
	public Func<Vector3> Center { get; set; }

	/// <summary>Number of streaks in the pool. Set before first enable.</summary>
	public int StreakCount { get; set; } = 60;

	GameObject _fxRoot;
	readonly List<GameObject> _streaks = new();
	uint _scatter = 0x2545F491;   // xorshift state (no System.Random, determinism hygiene)
	bool _raining;

	/// <summary>Turn the shower on or off. Cheap to call every frame with your weather check.</summary>
	public void SetRaining( bool raining ) => _raining = raining;

	Vector3 ResolveCenter() => Center?.Invoke() ?? WorldPosition;

	void EnsureRoot()
	{
		if ( _fxRoot.IsValid() ) return;
		_fxRoot = Scene.CreateObject();
		_fxRoot.Name = "fg_rain_fx";
		_fxRoot.SetParent( GameObject, false );
		var model = Model.Load( "models/dev/box.vmdl" );
		for ( int i = 0; i < StreakCount; i++ )
		{
			var go = Scene.CreateObject();
			go.Name = "rain_streak";
			go.SetParent( _fxRoot, false );
			go.Enabled = false;
			var r = go.Components.Create<ModelRenderer>();
			if ( model is not null ) r.Model = model;
			r.Tint = new Color( 0.62f, 0.72f, 0.85f, 0.45f );
			_streaks.Add( go );
		}
	}

	protected override void OnUpdate()
	{
		if ( !_raining )
		{
			if ( _fxRoot.IsValid() )
				foreach ( var s in _streaks )
					if ( s.IsValid() ) s.Enabled = false;
			return;
		}

		EnsureRoot();
		var center = ResolveCenter();
		const float fall = 900f, spread = 900f, top = 650f, bottom = 350f;

		foreach ( var s in _streaks )
		{
			if ( !s.IsValid() ) continue;
			if ( !s.Enabled ) { s.Enabled = true; Respawn( s, center, spread, top, bottom ); }

			s.WorldPosition += Vector3.Down * fall * Time.Delta;
			if ( s.WorldPosition.z <= center.z - 100f || s.WorldPosition.Distance( center ) > 1600f )
				Respawn( s, center, spread, top, bottom );
		}
	}

	void Respawn( GameObject s, Vector3 center, float spread, float top, float bottom )
	{
		s.WorldPosition = center + new Vector3(
			(NextJitter() * 2f - 1f) * spread,
			(NextJitter() * 2f - 1f) * spread,
			bottom + NextJitter() * (top - bottom) );
		s.WorldScale = new Vector3( 0.025f, 0.025f, 0.5f );   // thin vertical streak
	}

	/// <summary>Cheap per-streak scatter in [0,1), an xorshift on local state, NOT System.Random. Cosmetic
	/// only; never feeds anything deterministic.</summary>
	float NextJitter()
	{
		_scatter ^= _scatter << 13;
		_scatter ^= _scatter >> 17;
		_scatter ^= _scatter << 5;
		return (_scatter & 0xFFFFFF) / (float)0x1000000;
	}

	protected override void OnDestroy()
	{
		if ( _fxRoot.IsValid() ) _fxRoot.Destroy();
		_streaks.Clear();
	}
}
