using Sandbox;
using System;
using System.Linq;

namespace FieldGuide.Placement;

/// <summary>
/// Aim-follow ghost placement, pattern-rebuilt from World Builder's prop placer, minus all networking:
/// this is a single-player authoring driver. Toggle placement mode with B (or the `placement_place`
/// convar); a tinted, colliderless GHOST of the current catalog entry follows the aim ray (green =
/// valid spot, red = invalid, per <see cref="PlacementCatalog.ValidityCheck"/>). Bracket keys cycle
/// the catalog, R rotates the ghost, left mouse places, G deletes the placed object under the cursor.
///
/// Placed objects get a <see cref="PlacedInstance"/> tag so <see cref="PlacementExport"/> can find them.
/// Works in engine units throughout (no meter scaling), so it composes with any camera setup.
/// </summary>
[Title( "Ghost Placer" )]
[Category( "Field Guide · Placement" )]
[Icon( "add_location" )]
public sealed class GhostPlacer : Component
{
	public static GhostPlacer Instance { get; private set; }

	// ── HUD readback (static so a HUD razor needs no component handle) ──
	public static bool PlaceModeActive { get; private set; }
	public static string CurrentEntryName { get; private set; }
	public static int PlacedCount { get; private set; }

	// ── toggle state (B raw key + `placement_place` convar fallback) ──
	static bool _placeConvar;
	[ConVar( "placement_place", Help = "Arm or disarm ghost placement mode (same as the B key)" )]
	public static bool PlaceModeConVar { get => _placeConvar; set => _placeConvar = value; }

	/// <summary>Rotation step (degrees) applied by the R key.</summary>
	[Property] public float RotateStepDeg { get; set; } = 45f;

	/// <summary>How far the placed/ghost object sinks along -Z from the aim hit (engine units).</summary>
	[Property] public float SinkDepth { get; set; } = 0f;

	/// <summary>Max aim-ray length in engine units.</summary>
	[Property] public float AimReach { get; set; } = 500_000f;

	bool _placeMode;
	int _catalogIndex;
	float _ghostYaw;
	GameObject _ghost;
	string _ghostModel;

	PlacementCatalog Catalog => PlacementCatalog.Instance;

	PlaceableEntry CurrentEntry
	{
		get
		{
			var cat = Catalog;
			if ( cat is null || cat.Entries.Count == 0 ) return null;
			int n = cat.Entries.Count;
			return cat.Entries[((_catalogIndex % n) + n) % n];
		}
	}

	protected override void OnEnabled() => Instance = this;

	protected override void OnDisabled()
	{
		if ( Instance == this ) Instance = null;
		PlaceModeActive = false;
		DestroyGhost();
	}

	protected override void OnUpdate()
	{
		HandleInput();
		UpdateGhost();

		PlaceModeActive = _placeMode;
		CurrentEntryName = CurrentEntry?.DisplayName;
		PlacedCount = CountPlaced();
	}

	// ── input ──

	void HandleInput()
	{
		// B (raw key) or the `placement_place` convar toggles placement mode.
		if ( Input.Keyboard.Pressed( "B" ) )
			SetPlaceMode( !_placeMode );
		else if ( _placeConvar != _placeMode )
			SetPlaceMode( _placeConvar );

		if ( !_placeMode ) return;

		// cycle the catalog (bracket keys; the mouse wheel is the camera zoom, so it is not used here)
		if ( Input.Keyboard.Pressed( "]" ) ) { _catalogIndex++; RebuildGhostModel(); }
		if ( Input.Keyboard.Pressed( "[" ) ) { _catalogIndex--; RebuildGhostModel(); }

		// rotate (R)
		if ( Input.Keyboard.Pressed( "R" ) ) _ghostYaw = (_ghostYaw + RotateStepDeg) % 360f;

		// place (left mouse)
		if ( Input.Pressed( "attack1" ) )
			TryPlaceAtAim();

		// delete the placed object under the cursor (G)
		if ( Input.Keyboard.Pressed( "G" ) )
			TryDeleteAtAim();
	}

	void SetPlaceMode( bool on )
	{
		_placeMode = on;
		_placeConvar = on;
		if ( on )
		{
			RebuildGhostModel();
			Mouse.Visibility = MouseVisibility.Visible;
			Log.Info( $"[placement] mode ON, armed with {CurrentEntry?.DisplayName ?? "(empty catalog)"}" );
		}
		else
		{
			DestroyGhost();
			Log.Info( "[placement] mode OFF" );
		}
	}

	// ── aim ──

	bool AimHit( out Vector3 worldPos )
	{
		worldPos = default;
		var cam = Scene.Camera;
		if ( !cam.IsValid() ) return false;
		var ray = cam.ScreenPixelToRay( Mouse.Position );
		var trace = Scene.Trace.Ray( ray, AimReach );
		if ( _ghost.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( _ghost );   // never hit the ghost itself
		var tr = trace.Run();
		if ( !tr.Hit || tr.StartedSolid ) return false;
		worldPos = tr.HitPosition;
		return true;
	}

	void TryPlaceAtAim()
	{
		var entry = CurrentEntry;
		if ( entry is null || !AimHit( out var hit ) ) return;

		var pos = hit.WithZ( hit.z - SinkDepth );
		var rot = Rotation.FromYaw( _ghostYaw );

		if ( !(Catalog?.IsValidSpot( pos, rot ) ?? true) )
		{
			Log.Info( $"[placement] refused: invalid spot for {entry.DisplayName}" );
			return;
		}

		var go = Spawn( entry, pos, rot );
		if ( go is null )
		{
			Log.Info( $"[placement] could not spawn {entry.DisplayName} (no prefab or model)" );
			return;
		}
		Log.Info( $"[placement] placed {entry.Id} at {pos} yaw={_ghostYaw:0} count={CountPlaced()}" );
	}

	GameObject Spawn( PlaceableEntry entry, Vector3 pos, Rotation rot )
	{
		GameObject go = null;
		if ( entry.Prefab.IsValid() )
		{
			go = entry.Prefab.Clone( pos, rot );
		}
		else if ( !string.IsNullOrEmpty( entry.ModelPath ) )
		{
			go = Scene.CreateObject();
			go.WorldPosition = pos;
			go.WorldRotation = rot;
			var r = go.Components.Create<ModelRenderer>();
			var m = Model.Load( entry.ModelPath );
			if ( m is not null && !m.IsError ) r.Model = m;
		}
		if ( go is null ) return null;

		go.Name = $"placed_{entry.Id}";
		go.Components.GetOrCreate<PlacedInstance>().CatalogId = entry.Id;
		return go;
	}

	void TryDeleteAtAim()
	{
		var cam = Scene.Camera;
		if ( !cam.IsValid() ) return;
		var ray = cam.ScreenPixelToRay( Mouse.Position );
		var trace = Scene.Trace.Ray( ray, AimReach );
		if ( _ghost.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( _ghost );
		var tr = trace.Run();
		if ( !tr.Hit ) return;

		var placed = tr.GameObject?.Components.GetInAncestorsOrSelf<PlacedInstance>();
		if ( placed is null || !placed.IsValid() ) return;
		Log.Info( $"[placement] deleted {placed.CatalogId}" );
		placed.GameObject.Destroy();
	}

	// ── ghost preview (colliderless, tinted) ──

	void RebuildGhostModel() => _ghostModel = null;

	void UpdateGhost()
	{
		if ( !_placeMode ) { DestroyGhost(); return; }

		var entry = CurrentEntry;
		if ( entry is null || !AimHit( out var hit ) )
		{
			if ( _ghost.IsValid() ) _ghost.Enabled = false;
			return;
		}

		EnsureGhost( entry );
		if ( !_ghost.IsValid() ) return;
		_ghost.Enabled = true;

		var pos = hit.WithZ( hit.z - SinkDepth );
		var rot = Rotation.FromYaw( _ghostYaw );
		_ghost.WorldPosition = pos;
		_ghost.WorldRotation = rot;

		bool valid = Catalog?.IsValidSpot( pos, rot ) ?? true;
		var r = _ghost.Components.Get<ModelRenderer>();
		if ( r is not null )
			r.Tint = valid ? new Color( 0.5f, 1f, 0.55f, 0.55f ) : new Color( 1f, 0.45f, 0.4f, 0.55f );
	}

	void EnsureGhost( PlaceableEntry entry )
	{
		string modelPath = entry.ModelPath;
		if ( string.IsNullOrEmpty( modelPath ) ) { DestroyGhost(); return; }   // prefab-only entries: no light ghost

		if ( !_ghost.IsValid() )
		{
			_ghost = Scene.CreateObject();
			_ghost.Name = "placement_ghost";
			_ghost.Components.Create<ModelRenderer>();
			_ghostModel = null;
		}
		if ( _ghostModel != modelPath )
		{
			var r = _ghost.Components.Get<ModelRenderer>();
			var m = Model.Load( modelPath );
			if ( m is not null && !m.IsError ) { r.Model = m; _ghostModel = modelPath; }
		}
	}

	void DestroyGhost()
	{
		if ( _ghost.IsValid() ) { _ghost.Destroy(); _ghost = null; }
		_ghostModel = null;
	}

	int CountPlaced() => Scene?.GetAllComponents<PlacedInstance>().Count() ?? 0;
}
