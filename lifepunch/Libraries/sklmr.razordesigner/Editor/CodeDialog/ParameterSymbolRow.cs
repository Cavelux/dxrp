using Editor;
using Grains.RazorDesigner.Wiring;
using Sandbox;

namespace Grains.RazorDesigner.CodeDialog;

public sealed class ParameterSymbolRow : Widget
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    private readonly LineEdit _nameEdit;
    private readonly LineEdit _typeEdit;
    private readonly LineEdit _initialEdit;

    public ParameterSymbol Symbol { get; private set; }
    public event System.Action Changed;
    public event System.Action DeleteRequested;

    public ParameterSymbolRow( Widget parent, ParameterSymbol initial ) : base( parent )
    {
        Symbol = initial;
        Layout = Layout.Row();
        Layout.Spacing = 4;

        _nameEdit = new LineEdit( this )
        {
            Text = initial.Name ?? "",
            PlaceholderText = "Title",
        };
        _nameEdit.TextEdited += _ => OnNameEdited();
        Layout.Add( _nameEdit, 2 );

        _typeEdit = new LineEdit( this )
        {
            Text = string.IsNullOrEmpty( initial.Type ) ? "string" : initial.Type,
            PlaceholderText = "string | int | float | bool",
        };
        _typeEdit.TextEdited += _ => OnTypeEdited();
        Layout.Add( _typeEdit, 1 );

        _initialEdit = new LineEdit( this )
        {
            Text = ExpressionPreview.For( initial.Initial ),
            PlaceholderText = "Hello",
        };
        _initialEdit.TextEdited += _ => OnInitialEdited();
        Layout.Add( _initialEdit, 2 );

        var deleteButton = new Editor.Button( this )
        {
            Icon = "delete",
            ToolTip = "Delete this parameter",
            FixedWidth = CodeDialog.DeleteButtonWidth,
        };
        deleteButton.MouseLeftPress += () => DeleteRequested?.Invoke();
        Layout.Add( deleteButton );

        Log.Info( $"{LogPrefix} ParameterSymbolRow ctor: name='{initial.Name}', type='{initial.Type}'" );
    }

    private void OnNameEdited()
    {
        Symbol = Symbol with { Name = ( _nameEdit.Text ?? "" ).Trim() };
        Changed?.Invoke();
    }

    private void OnTypeEdited()
    {
        var newType = ( _typeEdit.Text ?? "" ).Trim();
        if ( string.IsNullOrEmpty( newType ) ) newType = "string";
        Symbol = Symbol with
        {
            Type = newType,
            Initial = InitialValueParser.Parse( newType, _initialEdit.Text ),
        };
        Changed?.Invoke();
    }

    private void OnInitialEdited()
    {
        Symbol = Symbol with { Initial = InitialValueParser.Parse( Symbol.Type, _initialEdit.Text ) };
        Changed?.Invoke();
    }
}
