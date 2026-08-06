using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo Arc authored in the editor.</summary>
[Title( "Goo Arc" )]
[Category( "Goo" )]
[Icon( "donut_large" )]
public sealed partial class ArcNode : BlobNode
{
	/// <summary>Angle where the arc begins, in degrees.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( -360f, 360f ), Step( 1f )] public float StartAngle { get; set; }

	/// <summary>Angle where the arc ends, in degrees.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( -360f, 360f ), Step( 1f )] public float EndAngle { get; set; } = 270f;

	/// <summary>Arc radius as a fraction from 0 to 1.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( 0f, 1f ), Step( 0.01f )] public float Radius { get; set; } = 0.8f;

	/// <summary>Thickness of the arc stroke.</summary>
	[Property, Group( "Shape" ), Order( 5 ), Editor( "goo-float" ), Range( 0f, 1f ), Step( 0.01f )] public float StrokeWidth { get; set; } = 0.08f;

	/// <summary>When true, input hits the full bounding box.</summary>
	[Property, Group( "Shape" ), Order( 5 )] public bool RectHitTest { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Arc
		{
			Key = BlobKey,
			StartAngle = StartAngle,
			EndAngle = EndAngle,
			Radius = Radius,
			StrokeWidth = StrokeWidth,
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
