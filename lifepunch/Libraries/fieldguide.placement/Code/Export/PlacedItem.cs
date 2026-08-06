using Sandbox;

namespace FieldGuide.Placement;

/// <summary>
/// One exported transform. <see cref="Frame"/> says what <see cref="Position"/> / <see cref="Rotation"/>
/// / <see cref="Scale"/> are measured against, and it is the field that makes an export useful in two
/// very different jobs:
///
///  - <see cref="WorldFrame"/> ("world"): a scene-authoring placement. The values are WORLD space, read
///    off a <see cref="PlacedInstance"/> the ghost placer stamped.
///  - anything else (e.g. "citizen/hold_R", "citizen/spine_2", "local"): a LOCAL offset relative to the
///    named parent frame, read off a live <see cref="ITweakTarget"/> in the tweak session. This is the
///    hero case: an accessory hanging off a character, tuned live and baked into game code.
///
/// <see cref="Id"/> is the stable key: the catalog id for a placed object, the target's bake id for a
/// tweaked one. Both export paths emit this same record so JSON and the C# snippet stay one shape.
/// </summary>
public record PlacedItem( string Id, Vector3 Position, Angles Rotation, float Scale, string Frame = PlacedItem.WorldFrame )
{
	/// <summary>The <see cref="Frame"/> value meaning "these are world-space coordinates".</summary>
	public const string WorldFrame = "world";

	/// <summary>True when this record is a local offset relative to a named parent frame, false when it
	/// is a world-space placement. Drives which block the C# export writes it into.</summary>
	public bool IsLocalOffset => Frame != WorldFrame;
}
