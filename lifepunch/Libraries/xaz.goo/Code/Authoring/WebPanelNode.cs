using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo WebPanel authored in the editor.</summary>
[Title( "Goo Web Panel" )]
[Category( "Goo" )]
[Icon( "language" )]
public sealed partial class WebPanelNode : BlobNode
{
	/// <summary>URL to load in the web view.</summary>
	[Property, Group( "Content" ), Order( 5 )] public string Url { get; set; }

	/// <summary>When true, throttles scripts, repaints, and media.</summary>
	[Property, Group( "Content" ), Order( 5 )] public bool Paused { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Goo.WebPanel
		{
			Key = BlobKey,
			Url = string.IsNullOrWhiteSpace( Url ) ? null : Url,
			Paused = Paused,
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
