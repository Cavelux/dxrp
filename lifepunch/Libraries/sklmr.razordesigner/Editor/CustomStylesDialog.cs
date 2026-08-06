using System;
using System.Collections.Generic;
using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.CustomStylesDialog;

public sealed class CustomStylesDialog : Dialog
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly ControlRecord _record;
	private readonly Action _onSaved;
	
	private readonly Widget _rowsContainer;
	private readonly Layout _rowsLayout;
	private readonly List<StyleRow> _activeRows = new();

	private sealed class StyleRow
	{
		public Widget RowWidget { get; init; }
		public LineEdit KeyEdit { get; init; }
		public LineEdit ValueEdit { get; init; }
	}

	public CustomStylesDialog( Widget parent, ControlRecord record, Action onSaved ) : base( parent )
	{
		_record = record ?? throw new ArgumentNullException( nameof( record ) );
		_onSaved = onSaved;
		
		// OPRAVA 1: Title -> WindowTitle
		WindowTitle = $"Custom Style Properties: {record.ClassName ?? record.Type.ToString()}";
		Size = new Vector2( 500, 400 );
		MinimumSize = new Vector2( 400, 300 );

		Layout = Layout.Column();
		Layout.Margin = 10;
		Layout.Spacing = 10;
		
		var infoLabel = new Label( this ) { Text = "Add custom SCSS/CSS style properties directly to this element:" };
		infoLabel.SetStyles( "color: #aaa; font-size: 12px;" );
		Layout.Add( infoLabel );
		
		// OPRAVA 2: Trik s roztahovací oblastí (StretchCell) pro chybějící funkci Insert
		var scrollArea = new ScrollArea( this );
		var scrollCanvas = new Widget( scrollArea );
		scrollCanvas.Layout = Layout.Column();

		_rowsContainer = new Widget( scrollCanvas );
		_rowsLayout = Layout.Column();
		_rowsLayout.Margin = 5;
		_rowsLayout.Spacing = 6;
		_rowsContainer.Layout = _rowsLayout;

		// Přidáme náš kontejner pro řádky a POD NĚJ dáme stretch cell, který to natlačí nahoru
		scrollCanvas.Layout.Add( _rowsContainer );
		scrollCanvas.Layout.AddStretchCell();

		scrollArea.Canvas = scrollCanvas;
		Layout.Add( scrollArea, 1 ); 
		
		var footerRow = Layout.Row();
		footerRow.Spacing = 8;

		var btnNew = new Button( "New", "add", this );
		btnNew.Clicked += () => AddStyleRow( "", "" );
		btnNew.SetStyles( "background-color: #2e6930; color: white; font-weight: bold; padding: 6px 12px;" );
		footerRow.Add( btnNew );

		footerRow.AddStretchCell(); 
		
		var btnCancel = new Button( "Cancel", this );
		btnCancel.Clicked += Close;
		footerRow.Add( btnCancel );

		var btnSave = new Button( "Save", "save", this );
		btnSave.Clicked += OnSaveClicked;
		btnSave.SetStyles( "background-color: #1a5fb4; color: white; font-weight: bold; padding: 6px 16px;" );
		footerRow.Add( btnSave );

		Layout.Add( footerRow );
		
		LoadExistingStyles();
	}

	private void LoadExistingStyles()
	{
		if ( _record.CustomStyles == null ) return;

		foreach ( var kvp in _record.CustomStyles )
		{
			AddStyleRow( kvp.Key, kvp.Value );
		}
		
		if ( _activeRows.Count == 0 )
		{
			AddStyleRow( "", "" );
		}
	}

	private void AddStyleRow( string key, string value )
	{
		var rowWidget = new Widget( _rowsContainer );
		// OPRAVA 3: Správné přiřazení layoutu
		var rowLayout = Layout.Row();
		rowLayout.Spacing = 6;
		rowWidget.Layout = rowLayout;

		var keyEdit = new LineEdit( rowWidget ) { PlaceholderText = "property-name", Text = key };
		rowLayout.Add( keyEdit, 2 );
		
		var colonLabel = new Label( rowWidget ) { Text = ":" };
		colonLabel.SetStyles( "color: #888; font-weight: bold;" );
		rowLayout.Add( colonLabel );
		
		var valueEdit = new LineEdit( rowWidget ) { PlaceholderText = "value", Text = value };
		rowLayout.Add( valueEdit, 3 );

		var semiLabel = new Label( rowWidget ) { Text = ";" };
		semiLabel.SetStyles( "color: #888;" );
		rowLayout.Add( semiLabel );

		// OPRAVA 4: Náhrada IconButton za standardní Button s ikonkou
		var btnDelete = new Button( "", "delete", rowWidget );
		btnDelete.ToolTip = "Remove this property";
		btnDelete.SetStyles( "color: #e55353;" ); // Lze přidat i: background: transparent; border: none; pokud by měl ošklivý rámeček
		
		var rowInfo = new StyleRow { RowWidget = rowWidget, KeyEdit = keyEdit, ValueEdit = valueEdit };
		_activeRows.Add( rowInfo );

		btnDelete.Clicked += () =>
		{
			_activeRows.Remove( rowInfo );
			rowWidget.Destroy();
			_rowsContainer.Update();
		};

		rowLayout.Add( btnDelete );

		// OPRAVA 5: Klasické přidání na konec (StretchCell v parentu zařídí posunutí nahoru)
		_rowsLayout.Add( rowWidget );
		_rowsContainer.Update();
	}

	private void OnSaveClicked()
	{
		Log.Info( $"{LogPrefix} CustomStylesDialog: Saving changes..." );

		_record.CustomStyles.Clear();
		
		foreach ( var row in _activeRows )
		{
			var propKey = row.KeyEdit.Text?.Trim();
			var propValue = row.ValueEdit.Text?.Trim();

			if ( string.IsNullOrEmpty( propKey ) ) continue;
			
			_record.CustomStyles[propKey] = propValue ?? "";
		}

		_onSaved?.Invoke();

		Log.Info( $"{LogPrefix} CustomStylesDialog: Saved {_record.CustomStyles.Count} custom properties successfully." );
		Close();
	}
}