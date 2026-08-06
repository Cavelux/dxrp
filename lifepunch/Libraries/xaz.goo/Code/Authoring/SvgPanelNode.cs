using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo SvgPanel authored in the editor.</summary>
[Title( "Goo SVG Panel" )]
[Category( "Goo" )]
[Icon( "gesture" )]
public sealed partial class SvgPanelNode : BlobNode
{
	/// <summary>Path to the SVG asset to render.</summary>
	[Property, Group( "Content" ), Order( 5 ), FilePath( Extension = "svg" )] public string Path { get; set; }

	/// <summary>Optional CSS color override.</summary>
	[Property, Group( "Content" ), Order( 5 )] public string Color { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Goo.SvgPanel
		{
			Key = BlobKey,
			Path = string.IsNullOrWhiteSpace( Path ) ? null : Path,
			Color = string.IsNullOrWhiteSpace( Color ) ? null : Color,
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
