using System.Collections.Generic;
using Editor;
using Grains.RazorDesigner.Wiring;
using Sandbox;

namespace Grains.RazorDesigner.CodeDialog;

public sealed class StateTab : Widget
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    private readonly CodeDialogEditBuffer _buffer;
    private readonly Layout _rowList;

    public StateTab( Widget parent, CodeDialogEditBuffer buffer ) : base( parent )
    {
        _buffer = buffer;
        Layout = Layout.Column();
        Layout.Margin = 8;
        Layout.Spacing = 4;

        // Column header — fixed labels aligned with the row's stretch cells (2/1/1/2 + delete column).
        var header = Layout.Row();
        header.Spacing = 4;
        header.Add( new Editor.Label( this ) { Text = "Name" }, 2 );
        header.Add( new Editor.Label( this ) { Text = "Type" }, 1 );
        header.Add( new Editor.Label( this ) { Text = "Visibility" }, 1 );
        header.Add( new Editor.Label( this ) { Text = "Initial" }, 2 );
        header.Add( new Widget( this ) { FixedWidth = CodeDialog.DeleteButtonWidth } );   // spacer for the × button column
        Layout.Add( header );

        _rowList = Layout.Column();
        _rowList.Spacing = 2;
        Layout.Add( _rowList );

        Layout.AddStretchCell();

        var addButton = new Editor.Button( this )
        {
            Text = "+ Add State",
            Icon = "add",
            MinimumWidth = CodeDialog.AddButtonMinWidth,
        };
        addButton.MouseLeftPress += OnAddClicked;
        Layout.Add( addButton );

        // Seed rows from the buffer.
        foreach ( var s in _buffer.States ) AddRow( s );

        Log.Info( $"{LogPrefix} StateTab ctor: seeded {_buffer.States.Count} row(s)" );
    }

    private void OnAddClicked()
    {
        var fresh = new StateSymbol
        {
            Name = MintFreshName(),
            Type = "int",
            Initial = InitialValueParser.Parse( "int", "0" ),
            Visibility = SymbolVisibility.Private,
        };
        _buffer.States.Add( fresh );
        AddRow( fresh );
        Log.Info( $"{LogPrefix} StateTab.OnAddClicked: added '{fresh.Name}'; total={_buffer.States.Count}" );
    }

    private void AddRow( StateSymbol symbol )
    {
        var row = new StateSymbolRow( this, symbol );
        row.Changed += () =>
        {
            // Locate by Id (stable across `with`-updates) and replace in place.
            var idx = _buffer.States.FindIndex( s => s.Id == row.Symbol.Id );
            if ( idx >= 0 ) _buffer.States[idx] = row.Symbol;
        };
        row.DeleteRequested += () =>
        {
            _buffer.States.RemoveAll( s => s.Id == row.Symbol.Id );
            row.Destroy();
            Log.Info( $"{LogPrefix} StateTab row deleted; total={_buffer.States.Count}" );
        };
        _rowList.Add( row );
    }

    private string MintFreshName()
    {
        var existing = new HashSet<string>();
        foreach ( var s in _buffer.States ) existing.Add( s.Name ?? "" );
        if ( !existing.Contains( "Count" ) ) return "Count";
        for ( int i = 2; i < 1000; i++ )
        {
            var candidate = $"Count{i}";
            if ( !existing.Contains( candidate ) ) return candidate;
        }
        return "Count";
    }
}
