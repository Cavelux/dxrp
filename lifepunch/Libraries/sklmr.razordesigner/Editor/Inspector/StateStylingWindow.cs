using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Grains.RazorDesigner.Common;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Selection;
using Sandbox;
using Margin = Sandbox.UI.Margin;

namespace Grains.RazorDesigner.Inspector;

public sealed class StateStylingWindow : BaseWindow
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const string GeometryCookie = "razordesigner.statestyling.geometry";

	private readonly DesignerDocument _document;
	private readonly SelectionController _selection;
	private readonly Action _onValueChanged;
	private readonly Action<ControlRecord, PseudoKind?> _onPinChanged;

	public event Action Closed;

	private ControlRecord _target;
	private Widget _toolbar;
	private ScrollArea _scroll;
	private Widget _footer;
	private Label _emptyStateLabel;
	private ComboBox _previewPicker;
	private Button _addStateButton;

	public StateStylingWindow(
		DesignerDocument document,
		SelectionController selection,
		Action onValueChanged,
		Action<ControlRecord, PseudoKind?> onPinChanged )
	{
		_document = document ?? throw new ArgumentNullException( nameof(document) );
		_selection = selection ?? throw new ArgumentNullException( nameof(selection) );
		_onValueChanged = onValueChanged ?? throw new ArgumentNullException( nameof(onValueChanged) );
		_onPinChanged = onPinChanged ?? throw new ArgumentNullException( nameof(onPinChanged) );

		// Always-on-top so the window can't fall behind an undocked DesignerWindow.
		// Tool flag was tried first but suppressed the taskbar entry, so when the window did go
		// behind there was no way to bring it back. Window | StaysOnTop keeps it in the taskbar
		// AND pinned above other windows. Trade-off: it pins above every app, not just sbox.
		WindowFlags = WindowFlags.Window | WindowFlags.WindowTitle | WindowFlags.CloseButton | WindowFlags.MinMaxButtons | WindowFlags.WindowStaysOnTopHint;
		WindowTitle = "State Styling";
		MinimumSize = new Vector2( 360, 400 );

		var raw = EditorCookie.Get<string>( GeometryCookie, null );
		if ( !string.IsNullOrEmpty( raw ) )
		{
			var parts = raw.Split( ',' );
			if ( parts.Length == 4
				&& float.TryParse( parts[0], out var x )
				&& float.TryParse( parts[1], out var y )
				&& float.TryParse( parts[2], out var w )
				&& float.TryParse( parts[3], out var h ) )
			{
				Position = new Vector2( x, y );
				Size = new Vector2( w, h );
				Log.Info( $"{LogPrefix} StateStylingWindow geometry restored: {x},{y} {w}x{h}" );
			}
		}
		else
		{
			Size = new Vector2( 480, 640 );
		}

		Log.Info( $"{LogPrefix} StateStylingWindow ctor" );

		// Body layout: toolbar (E1-E3) | scroll | footer hint (F6). For now just the scroll + empty-state.
		Layout = Layout.Column();
		Layout.Margin = 0;
		Layout.Spacing = 0;

		// Toolbar — Preview state picker + Add state button (E1).
		_toolbar = new Widget( this );
		_toolbar.Layout = Layout.Row();
		_toolbar.Layout.Margin = new Margin( 8, 4 );
		_toolbar.Layout.Spacing = 6;
		Layout.Add( _toolbar );

		var previewLabel = new Label( "Preview state:", _toolbar );
		previewLabel.SetStyles( "color: " + Theme.TextLight.Hex + ";" );
		_toolbar.Layout.Add( previewLabel );

		_previewPicker = new ComboBox( _toolbar );
		_previewPicker.AddItem( "None",    null, onSelected: () => SetPreviewPin( null ) );
		_previewPicker.AddItem( ":hover",  null, onSelected: () => SetPreviewPin( PseudoKind.Hover ) );
		_previewPicker.AddItem( ":active", null, onSelected: () => SetPreviewPin( PseudoKind.Active ) );
		_previewPicker.AddItem( ":focus",  null, onSelected: () => SetPreviewPin( PseudoKind.Focus ) );
		_toolbar.Layout.Add( _previewPicker );

		_toolbar.Layout.AddStretchCell();

		_addStateButton = new Button( "+ Add state ▾", _toolbar );
		_addStateButton.Clicked = OnAddStateClicked;
		_toolbar.Layout.Add( _addStateButton );

		Layout.AddSeparator();

		_scroll = new ScrollArea( this );
		_scroll.Canvas = new Widget();
		_scroll.Canvas.Layout = Layout.Column();
		_scroll.Canvas.Layout.Margin = new Margin( 0, 4 );
		_scroll.Canvas.VerticalSizeMode = SizeMode.CanGrow;
		Layout.Add( _scroll, 1 );

		// Footer hint — populated in F6.
		_footer = new Widget( this );
		_footer.Layout = Layout.Row();
		_footer.Layout.Margin = new Margin( 8, 6 );
		Layout.Add( _footer );

		// Footer hint — UI-spec §Window footer. Permanent (so it's findable), subtle.
		_footer.Layout.AddStretchCell();
		var footerLabel = new Label( "ⓘ  State rules without a transition apply instantly. Transitions land with grd-pd3.", _footer );
		footerLabel.SetStyles( "color: " + Theme.TextLight.Hex + "; font-size: 11px;" );
		_footer.Layout.Add( footerLabel );
		_footer.Layout.AddStretchCell();
		Log.Info( $"{LogPrefix} Footer transition-hint populated" );

		// Selection binding: rebuild body on every change.
		_selection.Changed += OnSelectionChanged;
		Bind( _selection.Selected );
	}

	protected override void OnClosed()
	{
		var x = Position.x; var y = Position.y; var w = Size.x; var h = Size.y;
		EditorCookie.Set( GeometryCookie, $"{x},{y},{w},{h}" );
		Log.Info( $"{LogPrefix} StateStylingWindow geometry saved: {x},{y} {w}x{h}" );

		_selection.Changed -= OnSelectionChanged;
		if ( _target is not null ) _onPinChanged?.Invoke( _target, null );
		Closed?.Invoke();
		base.OnClosed();
	}

	private void OnSelectionChanged()
	{
		// Pin clears on the OLD target before re-binding (UI-spec §Pin clearing rules).
		if ( _target is not null ) _onPinChanged?.Invoke( _target, null );
		Bind( _selection.Selected );
	}

	private void Bind( ControlRecord target )
	{
		_target = target;
		Log.Info( $"{LogPrefix} StateStylingWindow.Bind target={target?.ClassName ?? "<none>"}" );
		WindowTitle = target is null ? "State Styling" : $"State Styling — {target.ClassName}";
		if ( _previewPicker is not null ) _previewPicker.CurrentIndex = 0;  // Reset to None on every target change.
		_addStateButton.Enabled = target is not null;
		RebuildBody();
	}

	private void SetPreviewPin( PseudoKind? state )
	{
		if ( _target is null ) return;
		Log.Info( $"{LogPrefix} SetPreviewPin: target={_target?.ClassName} state={state}" );
		_onPinChanged?.Invoke( _target, state );
	}

	private void OnAddStateClicked()
	{
		if ( _target is null ) return;

		var menu = new Menu( _addStateButton );

		// Interactive states — already-present ones are skipped (except :nth-child which is repeatable).
		var present = new HashSet<PseudoKind>( _target.StateRules.Select( r => r.State ) );

		if ( !present.Contains( PseudoKind.Hover ) )
			menu.AddOption( ":hover", null, () => AppendStateRule( PseudoKind.Hover ) );
		if ( !present.Contains( PseudoKind.Active ) )
			menu.AddOption( ":active", null, () => AppendStateRule( PseudoKind.Active ) );
		if ( !present.Contains( PseudoKind.Focus ) )
			menu.AddOption( ":focus", null, () => AppendStateRule( PseudoKind.Focus ) );

		menu.AddSeparator();

		if ( !present.Contains( PseudoKind.Empty ) )
			menu.AddOption( ":empty", null, () => AppendStateRule( PseudoKind.Empty ) );
		if ( !present.Contains( PseudoKind.FirstChild ) )
			menu.AddOption( ":first-child", null, () => AppendStateRule( PseudoKind.FirstChild ) );
		if ( !present.Contains( PseudoKind.LastChild ) )
			menu.AddOption( ":last-child", null, () => AppendStateRule( PseudoKind.LastChild ) );
		if ( !present.Contains( PseudoKind.OnlyChild ) )
			menu.AddOption( ":only-child", null, () => AppendStateRule( PseudoKind.OnlyChild ) );

		// :nth-child is always offered — it's repeatable (multiple sections allowed for different args).
		menu.AddOption( ":nth-child…", null, () => AppendStateRule( PseudoKind.NthChild ) );

		menu.OpenAtCursor();
	}

	private void AppendStateRule( PseudoKind state )
	{
		if ( _target is null ) return;

		var rule = state == PseudoKind.NthChild
			? new StateRule { State = state, NthChildMode = NthChildMode.Literal, NthChildArg = 1, Delta = Appearance.Default }
			: new StateRule { State = state, Delta = Appearance.Default };

		_target.StateRules.Add( rule );
		Log.Info( $"{LogPrefix} AppendStateRule: target={_target.ClassName} state={state}" );

		_onValueChanged?.Invoke();   // dirties doc + refreshes preview.
		RebuildBody();
	}

	private void RebuildBody()
	{
		_scroll.Canvas.Layout.Clear( true );

		if ( _target is null )
		{
			Log.Info( $"{LogPrefix} StateStylingWindow.RebuildBody: no target — showing empty-state" );
			_emptyStateLabel = new Label( "No control selected.", _scroll.Canvas );
			_emptyStateLabel.SetStyles( "color: " + Theme.TextLight.Hex + "; padding: 16px; text-align: center;" );
			_scroll.Canvas.Layout.Add( _emptyStateLabel );
			_scroll.Canvas.Layout.AddStretchCell();
			return;
		}

		if ( _target.StateRules.Count == 0 )
		{
			Log.Info( $"{LogPrefix} StateStylingWindow.RebuildBody: target={_target.ClassName} has no state rules — showing hint" );
			_emptyStateLabel = new Label( "No state rules yet — click + Add state to add one.", _scroll.Canvas );
			_emptyStateLabel.SetStyles( "color: " + Theme.TextLight.Hex + "; padding: 16px; text-align: center;" );
			_scroll.Canvas.Layout.Add( _emptyStateLabel );
			_scroll.Canvas.Layout.AddStretchCell();
			return;
		}

		Log.Info( $"{LogPrefix} StateStylingWindow.RebuildBody: target={_target.ClassName}, {_target.StateRules.Count} state rule(s)" );
		// Render one StateSectionWidget per StateRule, in CompareCanonical order.
		var ordered = _target.StateRules.OrderBy( r => r, StateRuleCanonicalComparer.Instance ).ToList();
		for ( int i = 0; i < ordered.Count; i++ )
		{
			var rule = ordered[i];
			var section = new StateSectionWidget( _scroll.Canvas, _target, rule );
			section.BuildBody( _document, _onValueChanged );
			section.RuleChanged = _ => { _onValueChanged?.Invoke(); RebuildBody(); };
			section.RemoveRequested = () =>
			{
				_target.StateRules.Remove( rule );
				_onValueChanged?.Invoke();
				RebuildBody();
			};
			_scroll.Canvas.Layout.Add( section );
		}

		_scroll.Canvas.Layout.AddStretchCell();
	}
}
