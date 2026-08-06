using System.Collections.Generic;
using Editor;
using Grains.RazorDesigner.Wiring;
using Sandbox;

namespace Grains.RazorDesigner.CodeDialog;

public sealed class ParameterTab : Widget
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    private readonly CodeDialogEditBuffer _buffer;
    private readonly Layout _rowList;

    public ParameterTab( Widget parent, CodeDialogEditBuffer buffer ) : base( parent )
    {
        _buffer = buffer;
        Layout = Layout.Column();
        Layout.Margin = 8;
        Layout.Spacing = 4;

        // Column header — fixed labels aligned with the row's stretch cells (2/1/2 + delete column).
        var header = Layout.Row();
        header.Spacing = 4;
        header.Add( new Editor.Label( this ) { Text = "Name" }, 2 );
        header.Add( new Editor.Label( this ) { Text = "Type" }, 1 );
        header.Add( new Editor.Label( this ) { Text = "Initial" }, 2 );
        header.Add( new Widget( this ) { FixedWidth = CodeDialog.DeleteButtonWidth } );   // spacer for the × button column
        Layout.Add( header );

        _rowList = Layout.Column();
        _rowList.Spacing = 2;
        Layout.Add( _rowList );

        Layout.AddStretchCell();

        var addButton = new Editor.Button( this )
        {
            Text = "+ Add Parameter",
            Icon = "add",
            MinimumWidth = CodeDialog.AddButtonMinWidth,
        };
        addButton.MouseLeftPress += OnAddClicked;
        Layout.Add( addButton );

        // Seed rows from the buffer.
        foreach ( var p in _buffer.Parameters ) AddRow( p );

        Log.Info( $"{LogPrefix} ParameterTab ctor: seeded {_buffer.Parameters.Count} row(s)" );
    }

    private void OnAddClicked()
    {
        var fresh = new ParameterSymbol
        {
            Name = MintFreshName(),
            Type = "string",
            Initial = InitialValueParser.Parse( "string", "" ),
        };
        _buffer.Parameters.Add( fresh );
        AddRow( fresh );
        Log.Info( $"{LogPrefix} ParameterTab.OnAddClicked: added '{fresh.Name}'; total={_buffer.Parameters.Count}" );
    }

    private void AddRow( ParameterSymbol symbol )
    {
        var row = new ParameterSymbolRow( this, symbol );
        row.Changed += () =>
        {
            // Locate by Id (stable across `with`-updates) and replace in place.
            var idx = _buffer.Parameters.FindIndex( p => p.Id == row.Symbol.Id );
            if ( idx >= 0 ) _buffer.Parameters[idx] = row.Symbol;
        };
        row.DeleteRequested += () =>
        {
            _buffer.Parameters.RemoveAll( p => p.Id == row.Symbol.Id );
            row.Destroy();
            Log.Info( $"{LogPrefix} ParameterTab row deleted; total={_buffer.Parameters.Count}" );
        };
        _rowList.Add( row );
    }

    private string MintFreshName()
    {
        var existing = new HashSet<string>();
        foreach ( var p in _buffer.Parameters ) existing.Add( p.Name ?? "" );
        if ( !existing.Contains( "Title" ) ) return "Title";
        for ( int i = 2; i < 1000; i++ )
        {
            var candidate = $"Title{i}";
            if ( !existing.Contains( candidate ) ) return candidate;
        }
        return "Title";
    }
}
