using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo Image authored in the editor. Pick a texture asset; unset properties inherit Goo defaults.</summary>
[Title( "Goo Image" )]
[Category( "Goo" )]
[Icon( "image" )]
public sealed partial class ImageNode : BlobNode
{
	string _path;

	/// <summary>Texture asset to display. Clear it to use Path.</summary>
	[Property, Group( "Content" ), Order( 5 ), ShowIf( nameof( Path ), null )]
	public Texture Texture { get; set; }

	/// <summary>Mounted image-file path to display. Clear it to use Texture.</summary>
	[Property, Group( "Content" ), Order( 5 ), FilePath]
	public string Path
	{
		get => _path;
		set => _path = string.IsNullOrWhiteSpace( value ) ? null : value;
	}

	internal override void AddTo( Children children ) =>
		children.Add( WithStyles( new Goo.Image
		{
			Key = BlobKey,
			Texture = string.IsNullOrWhiteSpace( Path ) ? Texture : null,
			Path = string.IsNullOrWhiteSpace( Path ) ? null : Path,
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
