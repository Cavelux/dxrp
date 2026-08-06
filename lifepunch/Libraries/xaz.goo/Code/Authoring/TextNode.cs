using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>A Goo Text authored in the editor. Unset properties inherit Goo defaults.</summary>
[Title( "Goo Text" )]
[Category( "Goo" )]
[Icon( "text_fields" )]
public sealed partial class TextNode : BlobNode
{
	private string _path;

	/// <summary>Inline plain text, Markdown, or supported inline HTML.</summary>
	[Property, Group( "Content" ), Order( 5 ), TextArea, ShowIf( nameof( Path ), null )] public string Content { get; set; } = "Text";

	/// <summary>Mounted text-file path used instead of Content. Extensions do not select rich mode.</summary>
	[Property, Group( "Content" ), Order( 6 ), FilePath]
	public string Path
	{
		get => _path;
		set => _path = string.IsNullOrWhiteSpace( value ) ? null : value;
	}

	/// <summary>Parse the selected text source as Markdown and supported inline HTML.</summary>
	[Property, Group( "Content" ), Order( 7 )] public bool IsRich { get; set; }

	internal override void AddTo( Children children )
	{
		var path = string.IsNullOrWhiteSpace( Path ) ? null : Path;
		children.Add( WithStyles( new Text
		{
			Content = path is null ? Content ?? "" : "",
			Path = path,
			IsRich = IsRich,
			Key = BlobKey,
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
}
