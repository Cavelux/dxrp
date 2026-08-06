using System;
using System.Linq;
using Sandbox;

namespace FieldGuide.DayNight;

/// <summary>
/// The host-authoritative day/night clock: the ONE thin networked surface in this kit. Everything else
/// (grading, weights, weather, time-set math) is pure and testable; this component is the small, honest
/// piece that cannot run headless because it depends on s&amp;box networking.
///
/// Ownership model: in a peer-hosted s&amp;box session the HOST owns the clock. It accumulates game-time each
/// fixed tick and publishes three <c>[Sync(SyncFlags.FromHost)]</c> fields; clients never write them, they
/// observe and locally extrapolate so the sun advances smoothly between snapshots. Single-player (networking
/// inactive) is the host-of-one and just reads its own field directly.
///
/// THE PAUSE PIN (the hardening worth understanding). A paused host STOPS writing <see cref="NetTimeOfDay"/>.
/// A client only corrects its extrapolated clock toward the host snapshot when that value CHANGES, so with
/// only a time field on the wire, a paused host would leave every client free-running forever (host frozen at
/// noon, clients cycling a whole day). <see cref="NetTimePaused"/> fixes it: the host publishes the pause
/// state on the same FromHost surface, and while it is true a client PINS its local clock to the snapshot and
/// skips the advance. The instant the host unpauses, extrapolation resumes and re-locks onto the moving
/// snapshot. Keep both fields on the wire together, this is the fix, do not drop it.
///
/// Add this component to your session/game-manager GameObject once. Drive the look with
/// <see cref="DayNightDriver"/> (or read <see cref="GetTimeHours"/> / <see cref="EffectiveWeather"/> yourself).
/// </summary>
[Title( "Day Night Clock" )]
[Category( "Field Guide" )]
[Icon( "schedule" )]
public sealed class DayNightClock : Component
{
	/// <summary>Tuning (config over constants). Static, not replicated, set the SAME config on every peer
	/// (it is authoring data, identical everywhere, not session state). Defaults to the reference grade.</summary>
	public DayNightConfig Config { get; set; } = DayNightConfig.Default;

	/// <summary>The world seed the deterministic weather roll hashes against. Set it to whatever your game uses
	/// as its per-world seed so host and clients roll the same weather. Replicated so a mid-day joiner agrees.</summary>
	[Sync( SyncFlags.FromHost )] public int WorldSeed { get; set; }

	/// <summary>TOTAL game-hours since world start (dayIndex = floor(t/24), hour-of-day = t % 24). Host writes
	/// it each tick; clients observe and extrapolate. FromHost so only the host's write survives.</summary>
	[Sync( SyncFlags.FromHost )] public float NetTimeOfDay { get; set; }

	/// <summary>Pause replication (see the class remarks). The host's authority-only pause state, published on
	/// the SAME FromHost surface as <see cref="NetTimeOfDay"/> so a client can tell a paused host from a slow
	/// one and stop free-running. Default false = the clock runs.</summary>
	[Sync( SyncFlags.FromHost )] public bool NetTimePaused { get; set; }

	/// <summary>Weather override: -1 = derive from the (seed, dayIndex) hash; >=0 = a forced
	/// <see cref="WeatherKind"/> (a pin, or an authority carrying a specific day's roll to a late joiner).
	/// FromHost so the host owns it.</summary>
	[Sync( SyncFlags.FromHost )] public int NetWeatherOverride { get; set; } = -1;

	bool _timePaused;              // authority-side pause state, mirrored to NetTimePaused every tick
	float _clientTimeHours;        // client-side extrapolated clock (the host reads NetTimeOfDay directly)
	float _lastNetTime = float.NaN;// last observed NetTimeOfDay on a client (NaN ⇒ snap on first sync)

	/// <summary>Find the clock for a scene (first one). Returns null before it exists.</summary>
	public static DayNightClock For( Scene scene )
		=> scene?.GetAllComponents<DayNightClock>().FirstOrDefault();

	static bool IsAuthority => !Networking.IsActive || Networking.IsHost;

	protected override void OnStart()
	{
		if ( IsAuthority )
		{
			NetTimeOfDay = Config.StartHours;
			_timePaused = Config.StartPaused;
			NetTimePaused = _timePaused;
		}
		_clientTimeHours = NetTimeOfDay;
	}

	protected override void OnFixedUpdate()
	{
		// Authority only. Clients never accumulate here, they extrapolate NetTimeOfDay in OnUpdate.
		if ( !IsAuthority ) return;

		if ( !_timePaused )
		{
			// Non-uniform pace: scale the base hoursPerSecond by the pure per-hour-of-day rate so daylight runs
			// slower than night. Framerate-independent (dt-scaled) and deterministic (ClockRateScale is pure), so
			// host and every client derive the same rate from the same clock.
			float hod = NetTimeOfDay - MathF.Floor( NetTimeOfDay / 24f ) * 24f;
			NetTimeOfDay += SkyGrade.ClockRateScale( hod, Config ) * (24f / (Config.DayLengthMinutes * 60f)) * Time.Delta;
		}

		// Mirror the pause state to the wire EVERY tick, so it tracks every _timePaused mutation (the OnStart
		// seed, a pin, a UI toggle) even when the clock is not advancing.
		NetTimePaused = _timePaused;
	}

	protected override void OnUpdate()
	{
		// Only a CLIENT extrapolates. The authority reads NetTimeOfDay directly.
		if ( IsAuthority ) return;

		// Extrapolate the host clock locally between FromHost snapshots so the sun advances smoothly (snapshots
		// arrive at the network tick, not every frame). Same non-uniform pace as the authority (shared pure
		// ClockRateScale), derived from THIS client's own extrapolated clock so both peers advance identically
		// between snapshots; the ease-toward-net below corrects any residual drift each snapshot.
		//
		// THE PAUSE PIN: a paused host stops advancing NetTimeOfDay, and the change-gated ease below only corrects
		// when NetTimeOfDay MOVES, so a client that kept extrapolating would free-run forever. When the host
		// reports paused, PIN the local clock to the host snapshot and skip the advance; the instant the host
		// unpauses, extrapolation resumes and the ease re-locks onto the moving snapshot (the pinned _lastNetTime
		// avoids a false unpause jump).
		if ( NetTimePaused )
		{
			_clientTimeHours = NetTimeOfDay;
			_lastNetTime = NetTimeOfDay;
			return;
		}

		float clientHod = _clientTimeHours - MathF.Floor( _clientTimeHours / 24f ) * 24f;
		_clientTimeHours += SkyGrade.ClockRateScale( clientHod, Config ) * (24f / (Config.DayLengthMinutes * 60f)) * Time.Delta;
		float net = NetTimeOfDay;
		if ( net != _lastNetTime )
		{
			bool first = float.IsNaN( _lastNetTime );
			_lastNetTime = net;
			float d = net - _clientTimeHours;
			if ( first || MathF.Abs( d ) > 1f ) _clientTimeHours = net;   // first sync / pin jump → snap
			else _clientTimeHours += d * 0.25f;                            // small drift → ease
		}
	}

	/// <summary>The effective clock: the host reads its authoritative <see cref="NetTimeOfDay"/>, a client reads
	/// its locally-extrapolated clock (smoothed toward the host snapshots). Feed this to the grade and the sky
	/// weights so they can never disagree.</summary>
	public float GetTimeHours()
		=> IsAuthority ? NetTimeOfDay : _clientTimeHours;

	/// <summary>The current day index (floor(time / 24)).</summary>
	public int CurrentDay => (int)MathF.Floor( GetTimeHours() / 24f );

	/// <summary>The effective weather for a day: the host override if set, else the deterministic pure roll for
	/// (<see cref="WorldSeed"/>, dayIndex).</summary>
	public WeatherKind EffectiveWeather( int dayIndex )
	{
		if ( NetWeatherOverride >= 0 && System.Enum.IsDefined( typeof( WeatherKind ), NetWeatherOverride ) )
			return (WeatherKind)NetWeatherOverride;
		return WeatherRoll.For( WorldSeed, dayIndex );
	}

	/// <summary>The effective weather RIGHT NOW.</summary>
	public WeatherKind CurrentWeather => EffectiveWeather( CurrentDay );

	// ── authority-guarded writes (a client call is a quiet no-op; only the host's write survives the FromHost sync) ──

	/// <summary>Set the clock to an hour-of-day, preserving the current day (so weather does not re-roll). Snaps
	/// the client extrapolation trackers so a joiner does not ease across a deliberate jump. Authority only.</summary>
	public void SetTimeOfDay( float hourOfDay )
	{
		if ( Networking.IsActive && !Networking.IsHost ) return;
		NetTimeOfDay = TimeMath.ComputeSetHour( NetTimeOfDay, hourOfDay );
		_clientTimeHours = NetTimeOfDay;
		_lastNetTime = NetTimeOfDay;
	}

	/// <summary>Nudge the clock by a signed delta in game-hours (clamped at 0). Authority only.</summary>
	public void NudgeTime( float deltaHours )
	{
		if ( Networking.IsActive && !Networking.IsHost ) return;
		NetTimeOfDay = MathF.Max( 0f, NetTimeOfDay + deltaHours );
		_clientTimeHours = NetTimeOfDay;
		_lastNetTime = NetTimeOfDay;
	}

	/// <summary>Is the clock paused (authority-side)?</summary>
	public bool TimePaused => _timePaused;

	/// <summary>Pause or resume the clock. Authority only; the pause state replicates on the FromHost surface so
	/// clients stop free-running (see the class remarks).</summary>
	public void SetPaused( bool paused )
	{
		if ( Networking.IsActive && !Networking.IsHost ) return;
		_timePaused = paused;
		NetTimePaused = paused;
	}

	/// <summary>Force a weather kind (>=0) or clear back to the deterministic hash roll (-1). Authority only.</summary>
	public void SetWeatherOverride( int weather )
	{
		if ( Networking.IsActive && !Networking.IsHost ) return;
		NetWeatherOverride = ( weather >= 0 && System.Enum.IsDefined( typeof( WeatherKind ), weather ) ) ? weather : -1;
	}
}
