using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo ScenePanel authored in the editor from a scene asset path.</summary>
[Title( "Goo Scene Panel" )]
[Category( "Goo" )]
[Icon( "view_in_ar" )]
public sealed partial class ScenePanelNode : BlobNode
{
	/// <summary>Path to the scene asset to render.</summary>
	[Property, Group( "Content" ), Order( 5 ), FilePath( Extension = "scene" )] public string ScenePath { get; set; }

	/// <summary>When true, renders the scene only once.</summary>
	[Property, Group( "Content" ), Order( 5 )] public bool RenderOnce { get; set; }

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Goo.ScenePanel
		{
			Key = BlobKey,
			ScenePath = string.IsNullOrWhiteSpace( ScenePath ) ? null : ScenePath,
			RenderOnce = RenderOnce,
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
