using System;
using System.Linq;
using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

[CustomEditor( typeof( string ), WithAllAttributes = new[] { typeof( ImagePickerAttribute ) } )]
public sealed class ImageSourceControlWidget : ControlWidget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public override bool IsControlButton => true;
	public override bool SupportsMultiEdit => true;

	public ImageSourceControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		MouseTracking = true;
		AcceptDrops = true;
		IsDraggable = true;
		OnValueChanged();
	}

	protected override void PaintControl()
	{
		var resource = SerializedProperty.GetValue<string>( null );
		var asset = !string.IsNullOrEmpty( resource ) ? AssetSystem.FindByPath( resource ) : null;

		var rect = new Rect( 0, Size );
		var iconRect = rect.Shrink( 2 );
		iconRect.Width = iconRect.Height;
		rect.Left = iconRect.Right + 10;

		Paint.ClearPen();
		Paint.SetBrush( Theme.SurfaceBackground.WithAlpha( 0.2f ) );
		Paint.DrawRect( iconRect, 2 );

		Pixmap fallbackIcon = AssetType.ImageFile?.Icon64;

		if ( SerializedProperty.IsMultipleDifferentValues )
		{
			if ( fallbackIcon != null ) Paint.Draw( iconRect, fallbackIcon );
			Paint.SetDefaultFont();
			Paint.SetPen( Theme.MultipleValues );
			Paint.DrawText( rect.Shrink( 0, 3 ), "Multiple Values", TextFlag.LeftCenter );
			return;
		}

		if ( asset is not null && !asset.IsDeleted )
		{
			Paint.Draw( iconRect, asset.GetAssetThumb( true ) );

			var textRect = rect.Shrink( 0, 3 );
			Paint.SetPen( Theme.Text.WithAlpha( 0.9f ) );
			Paint.SetHeadingFont( 8, 450 );
			var t = Paint.DrawText( textRect, asset.Name, TextFlag.LeftTop );

			textRect.Left = t.Right + 6;
			Paint.SetDefaultFont( 7 );
			Theme.DrawFilename( textRect, asset.RelativePath, TextFlag.LeftCenter, Theme.Text.WithAlpha( 0.5f ) );
			return;
		}

		if ( !string.IsNullOrWhiteSpace( resource ) )
		{
			Paint.SetBrush( Theme.Red.Darken( 0.8f ) );
			Paint.DrawRect( iconRect, 2 );
			Paint.SetPen( Theme.Red );
			Paint.DrawIcon( iconRect, "error", Math.Max( 16, iconRect.Height / 2 ) );

			var textRect = rect.Shrink( 0, 3 );
			Paint.SetPen( Theme.Text.WithAlpha( 0.9f ) );
			Paint.SetHeadingFont( 8, 450 );
			var t = Paint.DrawText( textRect, "Missing image", TextFlag.LeftTop );

			textRect.Left = t.Right + 6;
			Paint.SetDefaultFont( 7 );
			Theme.DrawFilename( textRect, resource, TextFlag.LeftCenter, Theme.Text.WithAlpha( 0.5f ) );
			return;
		}

		if ( fallbackIcon != null ) Paint.Draw( iconRect, fallbackIcon );
		Paint.SetDefaultFont( italic: true );
		Paint.SetPen( Theme.Text.WithAlpha( 0.2f ) );
		Paint.DrawText( rect.Shrink( 0, 3 ), "Drop or click to pick image", TextFlag.LeftCenter );
	}

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		var m = new ContextMenu();

		var resource = SerializedProperty.GetValue<string>( null );
		var asset = !string.IsNullOrEmpty( resource ) ? AssetSystem.FindByPath( resource ) : null;

		m.AddOption( "Open in Editor", "edit", () => asset?.OpenInEditor() ).Enabled = asset is not null && !asset.IsProcedural;
		m.AddOption( "Find in Asset Browser", "search", () => LocalAssetBrowser.OpenTo( asset, true ) ).Enabled = asset is not null;
		m.AddSeparator();
		m.AddOption( "Copy", "file_copy", Copy ).Enabled = asset is not null;
		m.AddOption( "Paste", "content_paste", Paste );
		m.AddSeparator();
		m.AddOption( "Clear", "backspace", Clear ).Enabled = !string.IsNullOrEmpty( resource );

		m.OpenAtCursor( false );
		e.Accepted = true;
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		base.OnMouseClick( e );
		if ( ReadOnly ) return;
		OpenPicker();
	}

	private void OpenPicker()
	{
		var resource = SerializedProperty.GetValue<string>( null );
		var asset = !string.IsNullOrEmpty( resource ) ? AssetSystem.FindByPath( resource ) : null;

		var picker = AssetPicker.Create( this, AssetType.ImageFile );
		picker.SetSelection( resource );
		picker.Title = "Select Image";
		picker.OnAssetHighlighted = ( o ) => UpdateFromAsset( o.FirstOrDefault() );
		picker.OnAssetPicked = ( o ) => UpdateFromAsset( o.FirstOrDefault() );
		picker.Show();

		picker.SetSelection( asset );
	}

	private void UpdateFromAsset( Asset asset )
	{
		if ( asset is null ) return;
		SerializedProperty.Parent?.NoteStartEdit( SerializedProperty );
		SerializedProperty.SetValue( asset.RelativePath );
		SerializedProperty.Parent?.NoteFinishEdit( SerializedProperty );
		Log.Info( $"{LogPrefix} ImageSourceControlWidget set: {asset.RelativePath}" );
	}

	public override void OnDragHover( DragEvent ev )
	{
		var asset = ResolveDraggedAsset( ev );
		if ( IsAcceptable( asset ) ) { ev.Action = DropAction.Link; return; }
		if ( TryResolveRawImagePath( ev, out _ ) ) { ev.Action = DropAction.Link; return; }
	}

	public override void OnDragDrop( DragEvent ev )
	{
		var asset = ResolveDraggedAsset( ev );
		if ( IsAcceptable( asset ) )
		{
			PropertyStartEdit();
			UpdateFromAsset( asset );
			PropertyFinishEdit();
			ev.Action = DropAction.Link;
			return;
		}

		if ( TryResolveRawImagePath( ev, out var relPath ) )
		{
			PropertyStartEdit();
			SerializedProperty.Parent?.NoteStartEdit( SerializedProperty );
			SerializedProperty.SetValue( relPath );
			SerializedProperty.Parent?.NoteFinishEdit( SerializedProperty );
			PropertyFinishEdit();
			Log.Info( $"{LogPrefix} ImageSourceControlWidget set: {relPath}" );
			ev.Action = DropAction.Link;
			return;
		}

	}

	private static Asset ResolveDraggedAsset( DragEvent ev )
	{
		var text = ev.Data.Text;
		if ( !string.IsNullOrEmpty( text ) && !text.StartsWith( "file:", StringComparison.OrdinalIgnoreCase ) )
		{
			var byText = AssetSystem.FindByPath( text );
			if ( byText is not null ) return byText;
		}

		if ( ev.Data.HasFileOrFolder )
		{
			var path = ev.Data.FileOrFolder;
			if ( !string.IsNullOrEmpty( path ) )
			{
				var byPath = AssetSystem.FindByPath( path );
				if ( byPath is not null ) return byPath;

				var flipped = path.Replace( '\\', '/' );
				if ( flipped != path )
				{
					var byFlipped = AssetSystem.FindByPath( flipped );
					if ( byFlipped is not null ) return byFlipped;
				}
			}
		}

		return null;
	}

	private static bool IsAcceptable( Asset asset )
	{
		if ( asset is null ) return false;
		return asset.AssetType == AssetType.ImageFile || asset.AssetType == AssetType.Texture;
	}

	private static readonly string[] _rawImageExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".exr", ".webp", ".gif" };

	private static bool TryResolveRawImagePath( DragEvent ev, out string relativePath )
	{
		relativePath = null;
		if ( !ev.Data.HasFileOrFolder ) return false;

		var path = ev.Data.FileOrFolder;
		if ( string.IsNullOrEmpty( path ) ) return false;
		if ( !System.IO.File.Exists( path ) ) return false;

		var ext = System.IO.Path.GetExtension( path ).ToLowerInvariant();
		if ( Array.IndexOf( _rawImageExtensions, ext ) < 0 ) return false;

		var assetsRoot = Project.Current?.GetAssetsPath();
		if ( string.IsNullOrEmpty( assetsRoot ) ) return false;

		relativePath = System.IO.Path.GetRelativePath( assetsRoot, path ).Replace( '\\', '/' );
		return true;
	}

	protected override void OnDragStart()
	{
		var resource = SerializedProperty.GetValue<string>( null );
		var asset = !string.IsNullOrEmpty( resource ) ? AssetSystem.FindByPath( resource ) : null;
		if ( asset is null ) return;

		var drag = new Drag( this );
		drag.Data.Url = new Uri( $"file://{asset.AbsolutePath}" );
		drag.Execute();
	}

	private void Copy()
	{
		var resource = SerializedProperty.GetValue<string>( null );
		if ( string.IsNullOrEmpty( resource ) ) return;
		var asset = AssetSystem.FindByPath( resource );
		if ( asset is not null ) resource = asset.RelativePath;
		EditorUtility.Clipboard.Copy( resource );
	}

	private void Paste()
	{
		var path = EditorUtility.Clipboard.Paste();
		var asset = !string.IsNullOrEmpty( path ) ? AssetSystem.FindByPath( path ) : null;
		UpdateFromAsset( asset );
	}

	private void Clear()
	{
		SerializedProperty.Parent?.NoteStartEdit( SerializedProperty );
		SerializedProperty.SetValue( "" );
		SerializedProperty.Parent?.NoteFinishEdit( SerializedProperty );
		Log.Info( $"{LogPrefix} ImageSourceControlWidget cleared" );
	}
}
