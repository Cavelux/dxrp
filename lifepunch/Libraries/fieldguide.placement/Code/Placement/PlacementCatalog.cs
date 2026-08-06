using Sandbox;
using System;
using System.Collections.Generic;

namespace FieldGuide.Placement;

/// <summary>
/// The consumer-supplied set of placeable things, plus the validity seam the ghost uses to tint valid
/// vs invalid spots. Populate <see cref="Entries"/> in code (e.g. from a bootstrap) and, optionally, set
/// <see cref="ValidityCheck"/> to gate where placement is allowed (default: anywhere the aim ray hits).
/// Publishes itself through a static <see cref="Instance"/> so <see cref="GhostPlacer"/> needs no handle.
/// </summary>
[Title( "Placement Catalog" )]
[Category( "Field Guide · Placement" )]
[Icon( "category" )]
public sealed class PlacementCatalog : Component
{
	public static PlacementCatalog Instance { get; private set; }

	/// <summary>The placeable entries, in cycle order. Filled by the consumer at runtime.</summary>
	public List<PlaceableEntry> Entries { get; } = new();

	/// <summary>
	/// Seam: return true if an object may be placed at the given world position and rotation. Null means
	/// "always valid" (the default). The ghost tints green when this returns true, red when false, and a
	/// click only places on a valid spot.
	/// </summary>
	public Func<Vector3, Rotation, bool> ValidityCheck { get; set; }

	public bool IsValidSpot( Vector3 worldPos, Rotation worldRot )
		=> ValidityCheck is null || ValidityCheck( worldPos, worldRot );

	protected override void OnEnabled() => Instance = this;

	protected override void OnDisabled()
	{
		if ( Instance == this ) Instance = null;
	}
}
