using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo Sector authored in the editor.</summary>
[Title( "Goo Sector" )]
[Category( "Goo" )]
[Icon( "pie_chart" )]
public sealed partial class SectorNode : BlobNode
{
	/// <summary>Angle where the wedge begins, in degrees.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( -360f, 360f ), Step( 1f )] public float StartAngle { get; set; }

	/// <summary>Angle where the wedge ends, in degrees.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( -360f, 360f ), Step( 1f )] public float EndAngle { get; set; } = 270f;

	/// <summary>Inner radius as a fraction from 0 to 1.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( 0f, 1f ), Step( 0.01f )] public float InnerRadius { get; set; } = 0.6f;

	/// <summary>Outer radius as a fraction from 0 to 1.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( 0f, 1f ), Step( 0.01f )] public float OuterRadius { get; set; } = 1f;

	/// <summary>Corner rounding as a fraction from 0 to 1.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( 0f, 1f ), Step( 0.01f )] public float CornerRadius { get; set; }

	/// <summary>When true, input hits the full bounding box.</summary>
	[Property, Group( "Shape" ), Order( 5 )] public bool RectHitTest { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Sector
		{
			Key = BlobKey,
			StartAngle = StartAngle,
			EndAngle = EndAngle,
			InnerRadius = InnerRadius,
			OuterRadius = OuterRadius,
			CornerRadius = CornerRadius,
			RectHitTest = RectHitTest,
			OnClick = BlobOnClick,
			OnRightClick = BlobOnRightClick,
			OnMiddleClick = BlobOnMiddleClick,
			OnMouseEnter = BlobOnMouseEnter,
			OnMouseLeave = BlobOnMouseLeave,
			OnMouseDown = BlobOnMouseDown,
			OnMouseUp = BlobOnMouseUp,
			OnMouseMove = BlobOnMouseMove,
		} ) );
}
