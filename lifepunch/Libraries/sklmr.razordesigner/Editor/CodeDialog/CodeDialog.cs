using Editor;
using Grains.RazorDesigner.Wiring;
using Sandbox;

namespace Grains.RazorDesigner.CodeDialog;

public sealed class CodeDialog
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    internal const int DeleteButtonWidth = 28;
    internal const int AddButtonMinWidth = 120;
    private const int DialogButtonMinWidth = 80;

    private readonly Widget _parent;
    private readonly CodeDialogEditBuffer _buffer;
    private Editor.Dialog _dialog;

    public CodeDialog( Widget parent, WiringEnvelope current )
    {
        _parent = parent;
        _buffer = new CodeDialogEditBuffer( current );
    }

    public void Show( System.Action<WiringEnvelope> onConfirm, System.Action onCancel = null )
    {
        _dialog = new Editor.Dialog( _parent );
        _dialog.Window.WindowTitle = "Code — State & Parameters";
        _dialog.Window.SetWindowIcon( "code" );
        _dialog.Window.SetModal( true, true );
        _dialog.Window.MinimumWidth = 720;
        _dialog.Window.MinimumHeight = 520;

        _dialog.Layout = Layout.Column();
        _dialog.Layout.Margin = 0;
        _dialog.Layout.Spacing = 0;

        // Header strip — single-line hint above the splitter.
        var hint = new Editor.Label( _dialog )
        {
            Text = "Declare State (private fields) and Parameters (public [Parameter] properties). "
                 + "Methods, Lifecycle, and the Action editor land in W3.",
        };
        hint.SetStyles( "color: #888; font-size: 11px; padding: 8px 12px;" );
        _dialog.Layout.Add( hint );

        var splitter = new Splitter( _dialog ) { IsVertical = true };
        splitter.SetStyles(
            "QSplitter::handle { background-color: #1d1d1d; }" +
            "QSplitter::handle:hover { background-color: #4aa0ff; }" );
        splitter.HandleWidth = 4;

        var stateTabWrap = new Widget( _dialog );
        stateTabWrap.Layout = Layout.Column();
        var stateHeader = new Editor.Label( _dialog ) { Text = "State" };
        stateHeader.SetStyles( "font-weight: bold; padding: 6px 12px 0 12px;" );
        stateTabWrap.Layout.Add( stateHeader );
        var stateScroll = new ScrollArea( _dialog );
        stateScroll.Canvas = new StateTab( _dialog, _buffer );
        stateTabWrap.Layout.Add( stateScroll, 1 );
        splitter.AddWidget( stateTabWrap );

        var paramTabWrap = new Widget( _dialog );
        paramTabWrap.Layout = Layout.Column();
        var paramHeader = new Editor.Label( _dialog ) { Text = "Parameters" };
        paramHeader.SetStyles( "font-weight: bold; padding: 6px 12px 0 12px;" );
        paramTabWrap.Layout.Add( paramHeader );
        var paramScroll = new ScrollArea( _dialog );
        paramScroll.Canvas = new ParameterTab( _dialog, _buffer );
        paramTabWrap.Layout.Add( paramScroll, 1 );
        splitter.AddWidget( paramTabWrap );

        splitter.SetStretch( 0, 1 );
        splitter.SetStretch( 1, 1 );
        _dialog.Layout.Add( splitter, 1 );

        var buttonRow = _dialog.Layout.Add( Layout.Row() );
        buttonRow.Margin = 12;
        buttonRow.Spacing = 6;
        buttonRow.AddStretchCell();

        var cancel = new Editor.Button( _dialog ) { Text = "Cancel", MinimumWidth = DialogButtonMinWidth };
        cancel.MouseLeftPress += () =>
        {
            Log.Info( $"{LogPrefix} CodeDialog cancelled" );
            _dialog.Close();
            onCancel?.Invoke();
        };
        buttonRow.Add( cancel );

        var ok = new Editor.Button( _dialog ) { Text = "Apply", MinimumWidth = DialogButtonMinWidth };
        ok.MouseLeftPress += () =>
        {
            var env = _buffer.ToEnvelope();
            Log.Info( $"{LogPrefix} CodeDialog applied: states={_buffer.States.Count}, parameters={_buffer.Parameters.Count}" );
            _dialog.Close();
            onConfirm?.Invoke( env );
        };
        buttonRow.Add( ok );

        _dialog.Window.AdjustSize();
        _dialog.Show();
    }
}
