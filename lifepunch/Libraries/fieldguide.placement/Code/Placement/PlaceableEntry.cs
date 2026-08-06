using Sandbox;

namespace FieldGuide.Placement;

/// <summary>
/// One entry in a <see cref="PlacementCatalog"/>: an id, a display name, and what to spawn. Supply
/// either a <see cref="ModelPath"/> (the object is built as a GameObject + ModelRenderer, the light
/// path the ghost preview also uses) or a <see cref="Prefab"/> template GameObject (cloned on place,
/// carrying all its components). If both are set, <see cref="Prefab"/> wins for the placed object.
///
/// <see cref="Id"/> is the stable key written into the export (JSON/C# snippet), so keep it terse and
/// unique within a catalog (e.g. "box", "sphere", "crate_a").
/// </summary>
public sealed class PlaceableEntry
{
	/// <summary>Stable key written to the export. Unique within the catalog.</summary>
	public string Id { get; set; }

	/// <summary>Label shown in the placement HUD / logs.</summary>
	public string DisplayName { get; set; }

	/// <summary>Engine model path, e.g. "models/dev/box.vmdl". Used for the ghost preview and, when no
	/// <see cref="Prefab"/> is set, for the placed object (a GameObject with a single ModelRenderer).</summary>
	public string ModelPath { get; set; }

	/// <summary>Optional template GameObject cloned on place (a disabled prefab instance in the scene works
	/// well). Overrides <see cref="ModelPath"/> for the placed object; the ghost still uses ModelPath if set.</summary>
	public GameObject Prefab { get; set; }

	public PlaceableEntry() { }

	public PlaceableEntry( string id, string displayName, string modelPath )
	{
		Id = id;
		DisplayName = displayName;
		ModelPath = modelPath;
	}
}
