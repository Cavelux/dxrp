using System;
using System.Linq;
using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

[CustomEditor( typeof( string ), WithAllAttributes = new[] { typeof( BackgroundImagePickerAttribute ) } )]
public sealed class BackgroundImageControlWidget : ControlWidget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public override bool SupportsMultiEdit => true;

	private readonly LineEdit _textEdit;
	// Synchronous change events both directions; without this SetValue would re-enter.
	private bool _syncing;

	public BackgroundImageControlWidget( SerializedProperty property ) : base( property )
	{
		Log.Info( $"{LogPrefix} BackgroundImageControlWidget ctor for {property.Name}" );

		AcceptDrops = true;

		Layout = Layout.Row();
		Layout.Spacing = 2;

		_textEdit = new LineEdit( this )
		{
			PlaceholderText = "url(\"ui/bg.png\") or linear-gradient(...)",
		};
		_textEdit.MinimumSize = new Vector2( 60, Theme.RowHeight );
		_textEdit.MaximumSize = new Vector2( 4096, Theme.RowHeight );
		_textEdit.SetStyles( "background-color: transparent;" );
		_textEdit.AcceptDrops = false;
		_textEdit.EditingStarted += OnEditStarted;
		_textEdit.TextEdited += OnTextEdited;
		_textEdit.EditingFinished += OnEditFinished;
		Layout.Add( _textEdit, 1 );

		var pick = new IconButton( "image", OpenPicker )
		{
			ToolTip = "Pick an image asset — inserts url(\"…\")",
			Background = Color.Transparent,
			FixedSize = new Vector2( Theme.RowHeight, Theme.RowHeight ),
		};
		Layout.Add( pick );

		SyncFromProperty();
	}

	private void SyncFromProperty()
	{
		if ( _syncing ) return;
		_syncing = true;
		try
		{
			if ( SerializedProperty.IsMultipleDifferentValues )
			{
				if ( !_textEdit.IsFocused ) _textEdit.Text = "";
				return;
			}

			var v = SerializedProperty.GetValue<string>( "" ) ?? "";
			// Don't clobber the user's in-progress typing.
			if ( !_textEdit.IsFocused && _textEdit.Text != v )
				_textEdit.Text = v;
		}
		finally
		{
			_syncing = false;
		}
	}

	private void OnEditStarted()
	{
		if ( ReadOnly || !SerializedProperty.IsEditable ) return;
		PropertyStartEdit();
	}

	private void OnTextEdited( string text )
	{
		if ( _syncing ) return;
		if ( ReadOnly || !SerializedProperty.IsEditable ) return;
		Commit( text );
	}

	private void OnEditFinished()
	{
		if ( _syncing ) return;
		if ( !ReadOnly && SerializedProperty.IsEditable )
			Commit( _textEdit.Text );
		PropertyFinishEdit();
	}

	private void Commit( string text )
	{
		var current = SerializedProperty.GetValue<string>( "" ) ?? "";
		if ( text == current ) return;

		_syncing = true;
		try
		{
			SerializedProperty.SetValue( text );
			SignalValuesChanged();
		}
		finally
		{
			_syncing = false;
		}
	}

	// External set (picker / drop) — wrap in its own undo bracket and refresh the text box.
	private void SetExternal( string text )
	{
		if ( ReadOnly || !SerializedProperty.IsEditable ) return;

		PropertyStartEdit();
		_syncing = true;
		try
		{
			SerializedProperty.SetValue( text );
			SignalValuesChanged();
			_textEdit.Text = text;
		}
		finally
		{
			_syncing = false;
		}
		PropertyFinishEdit();
		Log.Info( $"{LogPrefix} BackgroundImageControlWidget set: {text}" );
	}

	private static string ToUrl( string relativePath ) => $"url(\"{relativePath}\")";

	private void OpenPicker()
	{
		if ( ReadOnly ) return;

		var picker = AssetPicker.Create( this, AssetType.ImageFile );
		picker.Title = "Select Background Image";
		picker.OnAssetPicked = ( assets ) =>
		{
			var asset = assets.FirstOrDefault();
			if ( asset is not null ) SetExternal( ToUrl( asset.RelativePath ) );
		};
		picker.Show();
	}

	public override void OnDragHover( DragEvent ev )
	{
		// Must set ev.Action here or the drop is never offered — keep it cheap (fires per move).
		if ( !ReadOnly && TryResolveImageRelPath( ev, out _ ) )
			ev.Action = DropAction.Link;
	}

	public override void OnDragDrop( DragEvent ev )
	{
		if ( TryResolveImageRelPath( ev, out var rel ) )
		{
			Log.Info( $"{LogPrefix} BackgroundImageControlWidget drop -> {rel}" );
			SetExternal( ToUrl( rel ) );
			ev.Action = DropAction.Link;
			return;
		}

		Log.Info( $"{LogPrefix} BackgroundImageControlWidget drop ignored — not an image asset (Text='{ev.Data.Text}', HasFileOrFolder={ev.Data.HasFileOrFolder}, FileOrFolder='{(ev.Data.HasFileOrFolder ? ev.Data.FileOrFolder : "")}')" );
	}

	private static bool TryResolveImageRelPath( DragEvent ev, out string relativePath )
	{
		relativePath = null;

		var text = ev.Data.Text;
		if ( !string.IsNullOrEmpty( text ) && !text.StartsWith( "file:", StringComparison.OrdinalIgnoreCase ) )
		{
			var byText = AssetSystem.FindByPath( text );
			if ( IsImageAsset( byText ) ) { relativePath = byText.RelativePath; return true; }
		}

		if ( ev.Data.HasFileOrFolder )
		{
			var path = ev.Data.FileOrFolder;
			if ( !string.IsNullOrEmpty( path ) )
			{
				var byPath = AssetSystem.FindByPath( path ) ?? AssetSystem.FindByPath( path.Replace( '\\', '/' ) );
				if ( IsImageAsset( byPath ) ) { relativePath = byPath.RelativePath; return true; }
			}
		}

		return false;
	}

	private static bool IsImageAsset( Asset asset )
		=> asset is not null && !asset.IsDeleted
		   && ( asset.AssetType == AssetType.ImageFile || asset.AssetType == AssetType.Texture );

	protected override void OnValueChanged()
	{
		base.OnValueChanged();
		SyncFromProperty();
	}
}
