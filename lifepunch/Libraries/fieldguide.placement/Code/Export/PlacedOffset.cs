using Sandbox;

namespace FieldGuide.Placement;

/// <summary>
/// The paste target for a baked accessory offset: what the tweak panel's Copy button and the C# export
/// construct, and what your own code holds once the numbers are final. It is deliberately small so a
/// baked line drops into a game project with nothing else attached.
///
/// The values are LOCAL to whatever the accessory is parented to (a hand attachment, a bone-follow
/// mount, the character root). The kit does not store which frame that was, because your code already
/// knows where it parents the object; the export writes the frame name into the comment above the line
/// so the paste has that context in front of you.
///
/// <code>
/// // hat: offset from citizen/head
/// static readonly PlacedOffset HatOffset =
///     new PlacedOffset( "hat", new Vector3( 0.5f, 0f, 3.25f ), new Angles( 0f, 90f, 0f ), 1f );
///
/// hat.SetParent( headMount, false );
/// HatOffset.ApplyTo( hat );
/// </code>
/// </summary>
public record PlacedOffset( string Id, Vector3 Position, Angles Rotation, float Scale )
{
	/// <summary>Write this offset onto a GameObject's LOCAL transform. Call it after parenting the object
	/// to its mount, and the object lands exactly where the tweak panel had it. No-op on an invalid object.</summary>
	public void ApplyTo( GameObject go )
	{
		if ( go is null || !go.IsValid() ) return;
		go.LocalPosition = Position;
		go.LocalRotation = this.Rotation.ToRotation();   // this. so the Angles PROPERTY wins over the Sandbox.Rotation TYPE
		go.LocalScale = new Vector3( Scale, Scale, Scale );
	}
}
