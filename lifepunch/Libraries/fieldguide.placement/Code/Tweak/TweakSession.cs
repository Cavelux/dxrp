using Sandbox;
using System.Collections.Generic;

namespace FieldGuide.Placement;

/// <summary>
/// Holds the list of objects the <see cref="TweakPanel"/> can edit, and publishes itself through a
/// static <see cref="Instance"/> so the panel needs no component handle. Two ways to fill it:
///
///  1. Wire GameObjects into <see cref="Objects"/> in the scene inspector (the simple path).
///  2. Call <see cref="Add(ITweakTarget)"/> / <see cref="Add(GameObject, string)"/> at runtime for
///     code-supplied or custom targets. This is the path the accessory-fitting flow uses: parent your
///     model to a character mount, then register it with a frame name so the bake says what the offset
///     is relative to.
///
/// <see cref="Targets"/> merges both into the effective list the panel tabs across.
///
/// The session also remembers each target's AUTHORED transform, captured the moment it is registered, so
/// the panel's Reset restores what you started with rather than slamming the object to zero (which for a
/// hand-mounted accessory means "collapsed into the wrist", never what you wanted).
/// </summary>
[Title( "Tweak Session" )]
[Category( "Field Guide · Placement" )]
[Icon( "tune" )]
public sealed class TweakSession : Component
{
	public static TweakSession Instance { get; private set; }

	/// <summary>Scene-wired targets: drop GameObjects here in the inspector to make them tweakable.</summary>
	[Property] public List<GameObject> Objects { get; set; } = new();

	readonly List<ITweakTarget> _custom = new();

	/// <summary>The authored local transform of each registered target, captured at registration. Keyed by
	/// GameObject so a custom ITweakTarget and a wrapped GameObject share one entry.</summary>
	readonly Dictionary<GameObject, Seed> _seeds = new();

	readonly record struct Seed( Vector3 Position, Rotation Rotation, Vector3 Scale );

	/// <summary>The effective, de-duplicated target list the panel tabs across: code-supplied targets
	/// first, then the scene-wired <see cref="Objects"/> (skipping any invalid or already-listed ones).</summary>
	public IReadOnlyList<ITweakTarget> Targets
	{
		get
		{
			var list = new List<ITweakTarget>();
			var seen = new HashSet<GameObject>();
			foreach ( var t in _custom )
			{
				if ( t?.Target is null || !t.Target.IsValid() || !seen.Add( t.Target ) ) continue;
				list.Add( t );
			}
			foreach ( var go in Objects )
			{
				if ( go is null || !go.IsValid() || !seen.Add( go ) ) continue;
				CaptureSeedIfNew( go );   // an object dropped into the list at runtime still gets an authored baseline
				list.Add( new TweakTarget( go ) );
			}
			return list;
		}
	}

	/// <summary>Register a custom target (your own <see cref="ITweakTarget"/> implementation).</summary>
	public void Add( ITweakTarget target )
	{
		if ( target?.Target is null ) return;
		_custom.Add( target );
		CaptureSeedIfNew( target.Target );
	}

	/// <summary>Register a GameObject as a target, wrapped in a <see cref="TweakTarget"/>.</summary>
	public void Add( GameObject go, string name = null )
	{
		if ( go is null ) return;
		_custom.Add( new TweakTarget( go, name ) );
		CaptureSeedIfNew( go );
	}

	/// <summary>Register a GameObject with the frame its offset is measured against and, optionally, the
	/// slider ranges its rows should use. The frame name shows on the panel sub-line and lands in the
	/// export and the bake comment, which is the whole reason a baked offset is readable later.</summary>
	public TweakTarget Add( GameObject go, string name, string frame, TweakRanges ranges = null )
	{
		if ( go is null ) return null;
		var t = new TweakTarget( go, name ) { Frame = frame, Ranges = ranges ?? TweakRanges.Accessory };
		_custom.Add( t );
		CaptureSeedIfNew( go );
		return t;
	}

	/// <summary>Drop a previously-registered code-supplied target (does not affect scene-wired Objects).</summary>
	public void Remove( GameObject go )
	{
		_custom.RemoveAll( t => t.Target == go );
		_seeds.Remove( go );
	}

	// ---- authored-default baseline ----

	/// <summary>Record this object's CURRENT local transform as its authored default, overwriting any
	/// earlier capture. Call it after you finish positioning an object in code if you want Reset to come
	/// back to that pose rather than the one it had at registration.</summary>
	public void CaptureSeed( GameObject go )
	{
		if ( go is null || !go.IsValid() ) return;
		_seeds[go] = new Seed( go.LocalPosition, go.LocalRotation, go.LocalScale );
	}

	void CaptureSeedIfNew( GameObject go )
	{
		if ( go is null || !go.IsValid() || _seeds.ContainsKey( go ) ) return;
		_seeds[go] = new Seed( go.LocalPosition, go.LocalRotation, go.LocalScale );
	}

	/// <summary>Restore a target to the transform captured when it was registered. Returns false if the
	/// object is invalid or was never registered here, so a caller can fall back if it wants to.</summary>
	public bool ResetToSeed( GameObject go )
	{
		if ( go is null || !go.IsValid() || !_seeds.TryGetValue( go, out var seed ) ) return false;
		go.LocalPosition = seed.Position;
		go.LocalRotation = seed.Rotation;
		go.LocalScale = seed.Scale;
		return true;
	}

	protected override void OnEnabled() => Instance = this;

	protected override void OnStart()
	{
		// Baseline the scene-wired objects at start, so their authored inspector transform is what Reset
		// comes back to even if something moves them before the panel is first opened.
		foreach ( var go in Objects )
			CaptureSeedIfNew( go );
	}

	protected override void OnDisabled()
	{
		if ( Instance == this ) Instance = null;
	}
}
