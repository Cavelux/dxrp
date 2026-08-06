namespace FieldGuide.Placement;

/// <summary>
/// Slider bounds and step sizes for one tweak target's rows, in ENGINE UNITS and degrees.
///
/// Why this is per-target and not a constant: fitting a hat to a head and laying out a building are the
/// same three sliders over wildly different ranges. A position row spanning +/-1024 units at step 1 (the
/// kit's original fixed range) cannot seat an accessory at all: one pixel of drag is several units, and
/// the value you want lives in the first half-percent of the track. The accessory default below spans
/// +/-48 units at step 0.25, so the xfine step (÷10) reaches 0.025 units and a full drag still covers a
/// character-sized volume.
/// </summary>
public sealed record TweakRanges(
	float PositionRange = 48f,
	float PositionStep = 0.25f,
	float RotationStep = 1f,
	float ScaleMin = 0.05f,
	float ScaleMax = 4f,
	float ScaleStep = 0.05f )
{
	/// <summary>The default: character-accessory scale. +/-48 units at 0.25 (xfine reaches 0.025), rotation
	/// at 1 degree, scale 0.05 to 4. This is what an unconfigured target gets.</summary>
	public static readonly TweakRanges Accessory = new();

	/// <summary>Scene-authoring scale, for tweaking a placed prop rather than a worn accessory:
	/// +/-1024 units at step 1, scale up to 8.</summary>
	public static readonly TweakRanges Scene = new( PositionRange: 1024f, PositionStep: 1f, ScaleMax: 8f );

	/// <summary>Room-scale, between the two: +/-256 units at 0.5.</summary>
	public static readonly TweakRanges Prop = new( PositionRange: 256f, PositionStep: 0.5f );
}
