using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo Polygon authored in the editor.</summary>
[Title( "Goo Polygon" )]
[Category( "Goo" )]
[Icon( "pentagon" )]
public sealed partial class PolygonNode : BlobNode
{
	/// <summary>Vertices in unit coordinates from 0 to 1.</summary>
	[Property, Group( "Shape" ), Order( 5 )]
	public Vector2[] Points { get; set; } = new[]
	{
		new Vector2( 0.5f, 0f ),
		new Vector2( 1f, 1f ),
		new Vector2( 0f, 1f ),
	};

	/// <summary>When true, input hits the full bounding box.</summary>
	[Property, Group( "Shape" ), Order( 5 )] public bool RectHitTest { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Polygon
		{
			Key = BlobKey,
			Points = Points,
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
