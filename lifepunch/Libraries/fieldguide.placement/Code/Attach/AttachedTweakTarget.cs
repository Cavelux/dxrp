using Sandbox;

namespace FieldGuide.Placement;

/// <summary>
/// A tweak target whose frame label comes from a live <see cref="CharacterAttachPoint"/> rather than a
/// string fixed at registration. Use this for anything hanging off a character.
///
/// Why it exists: an attach point resolves LATE (the model has to pose before its attachment objects
/// and bones can be read), so a frame string captured at registration would say "citizen/root" forever
/// even after the hand attachment came good. Reading it through the mount means the panel sub-line, the
/// export, and the bake comment all name the frame the accessory actually ended up on.
/// </summary>
public sealed class AttachedTweakTarget : ITweakTarget
{
	public string DisplayName { get; set; }

	/// <summary>The accessory whose LOCAL transform the panel edits. Parent it under
	/// <see cref="Mount"/> before registering, or its offsets mean nothing.</summary>
	public GameObject Target { get; set; }

	/// <summary>The mount the offset is measured from. Its resolved frame is read live.</summary>
	public CharacterAttachPoint Mount { get; set; }

	/// <summary>The id the bake line names. Leave null to derive it from <see cref="DisplayName"/>.</summary>
	public string Symbol { get; set; }

	/// <summary>Slider bounds and steps. Accessory scale by default, which is the point of this type.</summary>
	public TweakRanges Ranges { get; set; } = TweakRanges.Accessory;

	string ITweakTarget.FrameName => Mount.IsValid() ? Mount.FrameName : BakeFormat.LocalFrame;
	string ITweakTarget.BakeSymbol => string.IsNullOrEmpty( Symbol ) ? BakeFormat.SymbolFrom( DisplayName ) : Symbol;
	TweakRanges ITweakTarget.Ranges => Ranges ?? TweakRanges.Accessory;

	public AttachedTweakTarget( GameObject accessory, CharacterAttachPoint mount, string name = null )
	{
		Target = accessory;
		Mount = mount;
		DisplayName = string.IsNullOrEmpty( name ) ? (accessory?.Name ?? "Accessory") : name;
	}
}
