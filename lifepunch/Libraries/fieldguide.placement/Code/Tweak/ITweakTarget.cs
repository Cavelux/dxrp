using Sandbox;

namespace FieldGuide.Placement;

/// <summary>
/// The seam the tweak panel edits. A target is a display name plus the GameObject whose LOCAL transform
/// (position, rotation as pitch/yaw/roll, uniform scale) the panel nudges.
///
/// Only <see cref="DisplayName"/> and <see cref="Target"/> are required. Everything below is
/// default-implemented, so an existing implementation keeps compiling and gets sensible behaviour:
/// accessory-scale sliders, a "local" frame label, a bake id derived from the display name, and the
/// kit's standard bake line. Override the ones you care about.
///
/// Implement this to expose your own objects to the panel, or use the built-in <see cref="TweakTarget"/>
/// wrapper around a plain GameObject.
/// </summary>
public interface ITweakTarget
{
	/// <summary>The label shown on this target's tab in the panel.</summary>
	string DisplayName { get; }

	/// <summary>The object whose LocalPosition / LocalRotation / LocalScale the panel edits.</summary>
	GameObject Target { get; }

	/// <summary>
	/// What the edited offset is measured against: the parent this object hangs from, named in words your
	/// game code recognises. "citizen/hold_R", "citizen/spine_2", "turret/muzzle". It shows on the panel's
	/// sub-line and goes into the export and the bake comment, so a pasted offset says what it is relative
	/// to. Defaults to <see cref="BakeFormat.LocalFrame"/>, which just means "this object's parent".
	/// </summary>
	string FrameName => BakeFormat.LocalFrame;

	/// <summary>
	/// The identifier the bake line names, e.g. "hat" in <c>new PlacedOffset( "hat", ... )</c>. Defaults to
	/// a code-safe squashing of <see cref="DisplayName"/>, so "Back Pack" bakes as "back_pack".
	/// </summary>
	string BakeSymbol => BakeFormat.SymbolFrom( DisplayName );

	/// <summary>Slider bounds and steps for this target's rows. Defaults to
	/// <see cref="TweakRanges.Accessory"/>; use <see cref="TweakRanges.Scene"/> for a placed prop.</summary>
	TweakRanges Ranges => TweakRanges.Accessory;

	/// <summary>
	/// Optional shaping hook for the paste-ready bake line. Return null (the default) for the kit's
	/// standard two-line form, or return your own text to bake straight into your project's real shape,
	/// e.g. <c>ItemMounts.HatOffset = new Vector3( ... );</c>. The <paramref name="item"/> handed in
	/// already carries this target's live local transform, its <see cref="BakeSymbol"/> as the id, and its
	/// <see cref="FrameName"/>; format it with <see cref="BakeFormat.F(float)"/> to match the kit's
	/// invariant-culture float style.
	/// </summary>
	string FormatBakeLine( PlacedItem item ) => null;
}

/// <summary>
/// A minimal <see cref="ITweakTarget"/> that wraps a GameObject, defaulting its label to the object's
/// name. Convenience for the common case where a target is just "this GameObject", with the optional
/// interface members lifted to settable properties so a caller can fill them at registration.
/// </summary>
public sealed class TweakTarget : ITweakTarget
{
	public string DisplayName { get; set; }
	public GameObject Target { get; set; }

	/// <summary>What the offset is relative to, shown on the panel sub-line and written into the export.
	/// Leave null for the "local" default.</summary>
	public string Frame { get; set; }

	/// <summary>The id the bake line names. Leave null to derive it from <see cref="DisplayName"/>.</summary>
	public string Symbol { get; set; }

	/// <summary>Slider bounds and steps. Defaults to accessory scale.</summary>
	public TweakRanges Ranges { get; set; } = TweakRanges.Accessory;

	string ITweakTarget.FrameName => string.IsNullOrEmpty( Frame ) ? BakeFormat.LocalFrame : Frame;
	string ITweakTarget.BakeSymbol => string.IsNullOrEmpty( Symbol ) ? BakeFormat.SymbolFrom( DisplayName ) : Symbol;
	TweakRanges ITweakTarget.Ranges => Ranges ?? TweakRanges.Accessory;

	public TweakTarget( GameObject go, string name = null )
	{
		Target = go;
		DisplayName = string.IsNullOrEmpty( name ) ? (go?.Name ?? "Object") : name;
	}
}
