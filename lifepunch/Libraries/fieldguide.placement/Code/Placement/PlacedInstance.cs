using Sandbox;

namespace FieldGuide.Placement;

/// <summary>
/// Tags a placed object with the catalog id it came from, so <see cref="PlacementExport"/> can collect
/// every placed object in the scene and record which entry produced it. <see cref="GhostPlacer"/> adds
/// this on place; you can also add it by hand (or in a prefab) to make an object show up in exports.
/// </summary>
[Title( "Placed Instance" )]
[Category( "Field Guide · Placement" )]
[Icon( "place" )]
public sealed class PlacedInstance : Component
{
	/// <summary>The <see cref="PlaceableEntry.Id"/> this object was placed from.</summary>
	[Property] public string CatalogId { get; set; }
}
