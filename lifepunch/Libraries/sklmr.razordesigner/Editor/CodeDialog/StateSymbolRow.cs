using Editor;
using Grains.RazorDesigner.Wiring;
using Sandbox;

namespace Grains.RazorDesigner.CodeDialog;

public sealed class StateSymbolRow : Widget
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    private readonly LineEdit _nameEdit;
    private readonly LineEdit _typeEdit;
    private readonly EnumControlWidget _visibilityWidget;
    private readonly LineEdit _initialEdit;

    private readonly VisibilityProxy _visibilityProxy;
    private readonly SerializedObject _visibilitySerialized;

    public StateSymbol Symbol { get; private set; }
    public event System.Action Changed;
    public event System.Action DeleteRequested;

    public StateSymbolRow( Widget parent, StateSymbol initial ) : base( parent )
    {
        Symbol = initial;
        Layout = Layout.Row();
        Layout.Spacing = 4;

        _nameEdit = new LineEdit( this )
        {
            Text = initial.Name ?? "",
            PlaceholderText = "Count",
        };
        _nameEdit.TextEdited += _ => OnNameEdited();
        Layout.Add( _nameEdit, 2 );

        _typeEdit = new LineEdit( this )
        {
            Text = string.IsNullOrEmpty( initial.Type ) ? "int" : initial.Type,
            PlaceholderText = "string | int | float | bool",
        };
        _typeEdit.TextEdited += _ => OnTypeEdited();
        Layout.Add( _typeEdit, 1 );

        _visibilityProxy = new VisibilityProxy { Visibility = initial.Visibility };
        _visibilitySerialized = EditorTypeLibrary.GetSerializedObject( _visibilityProxy );
        var visProp = _visibilitySerialized.GetProperty( nameof( VisibilityProxy.Visibility ) );
        _visibilityWidget = new EnumControlWidget( visProp );
        _visibilitySerialized.OnPropertyChanged += _ => OnVisibilityChanged();
        Layout.Add( _visibilityWidget, 1 );

        _initialEdit = new LineEdit( this )
        {
            Text = ExpressionPreview.For( initial.Initial ),
            PlaceholderText = "0",
        };
        _initialEdit.TextEdited += _ => OnInitialEdited();
        Layout.Add( _initialEdit, 2 );

        var deleteButton = new Editor.Button( this )
        {
            Icon = "delete",
            ToolTip = "Delete this state symbol",
            FixedWidth = CodeDialog.DeleteButtonWidth,
        };
        deleteButton.MouseLeftPress += () => DeleteRequested?.Invoke();
        Layout.Add( deleteButton );

        Log.Info( $"{LogPrefix} StateSymbolRow ctor: name='{initial.Name}', type='{initial.Type}', visibility={initial.Visibility}" );
    }

    private void OnNameEdited()
    {
        Symbol = Symbol with { Name = ( _nameEdit.Text ?? "" ).Trim() };
        Changed?.Invoke();
    }

    private void OnTypeEdited()
    {
        var newType = ( _typeEdit.Text ?? "" ).Trim();
        if ( string.IsNullOrEmpty( newType ) ) newType = "int";
        Symbol = Symbol with
        {
            Type = newType,
            Initial = InitialValueParser.Parse( newType, _initialEdit.Text ),
        };
        Changed?.Invoke();
    }

    private void OnVisibilityChanged()
    {
        Symbol = Symbol with { Visibility = _visibilityProxy.Visibility };
        Changed?.Invoke();
    }

    private void OnInitialEdited()
    {
        Symbol = Symbol with { Initial = InitialValueParser.Parse( Symbol.Type, _initialEdit.Text ) };
        Changed?.Invoke();
    }

    private sealed class VisibilityProxy
    {
        public SymbolVisibility Visibility { get; set; }
    }
}

internal static class ExpressionPreview
{
    public static string For( Expression expr )
    {
        return expr switch
        {
            null => "",
            LiteralExpression lit when lit.Value.ValueKind == System.Text.Json.JsonValueKind.String
                => lit.Value.GetString() ?? "",
            LiteralExpression lit => lit.Value.GetRawText(),
            InlineExpression inl  => inl.Code,
            _ => "<expression>",
        };
    }
}
