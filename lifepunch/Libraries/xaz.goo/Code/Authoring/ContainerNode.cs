using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo Container authored in the editor. Unset properties inherit Goo defaults.</summary>
[Title( "Goo Container" )]
[Category( "Goo" )]
[Icon( "check_box_outline_blank" )]
public sealed partial class ContainerNode : BlobNode
{
	/// <summary>Stops left-clicks on this container from bubbling to an ancestor click handler.</summary>
	[Property, Group( "Events", StartFolded = true ), Order( 130 )]
	public bool SwallowClick { get; set; }

	/// <summary>Runs when the mouse wheel scrolls over this container or a descendant that did not consume it. Delta y is positive when scrolling down. Setting this consumes the wheel.</summary>
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )]
	public GooWheelAction OnMouseWheel { get; set; }

	/// <summary>Duration of automatic layout-position glides. Zero disables layout transitions.</summary>
	[Property, Group( "Animation", StartFolded = true ), Order( 125 ), Editor( "goo-layout-transition" ),
		Title( "Layout Transition (ms)" ), Range( 0f, float.MaxValue, true, false ), Step( 1f )]
	public float LayoutTransitionMs { get; set; }

	/// <summary>Easing used by automatic layout-position glides.</summary>
	[Property, Group( "Animation", StartFolded = true ), Order( 125 ), Hide]
	public GooLayoutEasing LayoutTransitionEasing { get; set; } = GooLayoutEasing.EaseOut;

	internal override void AddTo( Children children )
	{
		var container = WithStyles( new Container
		{
			Key = BlobKey,
			SwallowClick = SwallowClick,
			LayoutTransition = LayoutTransitionMs > 0f
				? new LayoutTransition( LayoutTransitionMs, LayoutTransitionEasing.Resolve() )
				: null,
			OnClick = BlobOnClick,
			OnRightClick = BlobOnRightClick,
			OnMiddleClick = BlobOnMiddleClick,
			OnMouseEnter = BlobOnMouseEnter,
			OnMouseLeave = BlobOnMouseLeave,
			OnMouseDown = BlobOnMouseDown,
			OnMouseUp = BlobOnMouseUp,
			OnMouseMove = BlobOnMouseMove,
			OnMouseWheel = OnMouseWheel is null ? null : v => OnMouseWheel( this, v ),
		} );

		AddChildNodes( GameObject, container.Children );
		children.Add( in container );
	}
}
