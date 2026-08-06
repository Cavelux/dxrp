using System.Collections.Generic;
using System.Linq;
using Editor;
using Grains.RazorDesigner.Common;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Diagnostics;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Validation;
using Sandbox;
using Margin = Sandbox.UI.Margin;

namespace Grains.RazorDesigner.Inspector;

public sealed class InspectorPanel : Widget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const string ExpandStateCookie = "razordesigner.inspector.expanded";

	public System.Action ValueChanged;
	// (record, oldClassName). Surgical: caller swaps LivePanel class + repaints tree.
	public System.Action<ControlRecord, string> ClassNameChanged;

	private readonly DesignerDocument _document;
	private ControlRecord _target;
	private SerializedObject _serialized;
	private string _priorClassName;
	// Re-entry guard: SetValue on rejected edit fires OnFinishEdit again.
	private bool _isRevertingClassName;

	private LineEdit _filterEdit;
	private string _filterText = "";
	private CollapsibleSection _identityBanner;
	private ScrollArea _scroll;
	private Widget _canvas;
	private AppearanceGroupBuilder _builder;
	private readonly InspectorExpandStateStore _expandStore = new( ExpandStateCookie );

	// Diagnostics from the last Validator pass. Empty = no issues.
	private IReadOnlyList<ValidationDiagnostic> _diagnostics = System.Array.Empty<ValidationDiagnostic>();
	// Status row shown at the bottom of the inspector when diagnostics are present.
	private Widget _diagnosticsRow;
	private Label _diagnosticsLabel;

	public System.Action<ControlRecord> OpenStateStylingRequested;
	private Button _stateStylingButton;
	private Label _stateStylingCount;

	private static readonly (string Group, bool Expanded, string Icon)[] GroupOrder =
	{
		( "Identity",     true,  "label" ),
		( "Layout",       true,  "dashboard" ),
		( "Flex",         true,  "view_column" ),
		( "Content",      true,  "text_fields" ),
		( "Image",        true,  "image" ),
		( "Icon",         true,  "star" ),
		( "Typography",   false, "format_size" ),
		( "Constraints",  false, "fit_screen" ),
		( "Background",   false, "palette" ),
		( "Border",       false, "border_style" ),
		( "Effects",      false, "auto_awesome" ),
		( "Interaction",  false, "touch_app" ),
	};

	public InspectorPanel( Widget parent, DesignerDocument document ) : base( parent )
	{
		_document = document;
		Log.Info( $"{LogPrefix} InspectorPanel ctor" );
		Layout = Layout.Column();
		Layout.Margin = 0;
		Layout.Spacing = 0;
		MinimumWidth = 280;

		var toolbar = new Widget( this );
		toolbar.Layout = Layout.Row();
		toolbar.Layout.Margin = 4;
		toolbar.Layout.Spacing = 4;

		_filterEdit = new LineEdit( toolbar ) { PlaceholderText = "Filter properties..." };
		_filterEdit.TextEdited += OnFilterEdited;
		toolbar.Layout.Add( _filterEdit, 1 );

		Layout.Add( toolbar );
		Layout.AddSeparator();

		_identityBanner = new CollapsibleSection( this, "", "category" );
		_identityBanner.IsCollapsible = false;
		Layout.Add( _identityBanner );

		_scroll = new ScrollArea( this );
		_scroll.Canvas = new Widget();
		_scroll.Canvas.Layout = Layout.Column();
		_scroll.Canvas.Layout.Margin = new Margin( 0, 4 );
		_scroll.Canvas.VerticalSizeMode = SizeMode.CanGrow;
		_scroll.Canvas.HorizontalSizeMode = SizeMode.Flexible;
		_canvas = _scroll.Canvas;
		Layout.Add( _scroll, 1 );
		Layout.AddStretchCell();

		// State styling footer — sits above diagnostics. (grd-ge50 D3.)
		var stateRow = new Widget( this );
		stateRow.Layout = Layout.Row();
		stateRow.Layout.Margin = new Margin( 8, 4 );
		stateRow.Layout.Spacing = 6;

		_stateStylingButton = new Button( "State styling…", stateRow );
		_stateStylingButton.Clicked = () =>
		{
			if ( _target is null ) return;
			OpenStateStylingRequested?.Invoke( _target );
		};
		stateRow.Layout.Add( _stateStylingButton );

		_stateStylingCount = new Label( "", stateRow );
		_stateStylingCount.SetStyles( "color: " + Theme.TextLight.Hex + ";" );
		stateRow.Layout.Add( _stateStylingCount, 1 );

		Layout.Add( stateRow );

		_diagnosticsRow = new Widget( this );
		_diagnosticsRow.Layout = Layout.Row();
		_diagnosticsRow.Layout.Margin = new Margin( 6, 4 );
		_diagnosticsRow.Layout.Spacing = 4;
		_diagnosticsLabel = new Label( _diagnosticsRow );
		_diagnosticsLabel.WordWrap = true;
		_diagnosticsRow.Layout.Add( _diagnosticsLabel, 1 );
		_diagnosticsRow.Visible = false;
		Layout.Add( _diagnosticsRow );
		Log.Info( $"{LogPrefix} Inspector diagnostics status row created" );

		Rebuild();
	}

	public void SetTarget( ControlRecord record )
	{
		_target = record;
		_priorClassName = _target?.ClassName;
		Log.Info( $"{LogPrefix} Inspector.SetTarget({_target?.ClassName ?? "<none>"})" );
		Rebuild();
	}

	public void SetDiagnostics( IReadOnlyList<ValidationDiagnostic> diagnostics )
	{
		_diagnostics = diagnostics ?? System.Array.Empty<ValidationDiagnostic>();
		RebuildDiagnosticsRow();
		Log.Info( $"{LogPrefix} Inspector.SetDiagnostics: {_diagnostics.Count} diagnostic(s)" );
	}

	public void NotifyValueChanged() => ValueChanged?.Invoke();

	private void RebuildDiagnosticsRow()
	{
		if ( _diagnosticsRow is null || _diagnosticsLabel is null ) return;

		if ( _diagnostics.Count == 0 )
		{
			_diagnosticsRow.Visible = false;
			return;
		}

		var errorCount = 0;
		var warnCount = 0;
		foreach ( var d in _diagnostics )
		{
			if ( d.Severity == DiagnosticSeverity.Error ) errorCount++;
			else warnCount++;
		}

		string text;
		if ( errorCount > 0 )
			text = $"⚠ {errorCount} error(s){(warnCount > 0 ? $", {warnCount} warning(s)" : "")} — Razor regeneration blocked";
		else
			text = $"{warnCount} warning(s)";

		_diagnosticsLabel.Text = text;
		_diagnosticsRow.Visible = true;
	}

	private void Rebuild()
	{
		// Unwire prior _serialized: orphaned instance still holds delegate refs.
		if ( _serialized is not null )
		{
			_serialized.OnPropertyChanged -= OnPropertyChanged;
			var oldClassName = _serialized.GetProperty( "ClassName" );
			if ( oldClassName is not null )
				oldClassName.OnFinishEdit -= OnClassNameFinishEdit;
			_serialized = null;
		}

		if ( _target is null )
		{
			_identityBanner.Visible = false;
			_scroll.Visible = false;
			_canvas.Layout.Clear( true );
			_builder = null;
			Update();
			RefreshStateStylingFooter();
			return;
		}

		_identityBanner.Visible = true;
		_identityBanner.Title = BuildHeaderText();
		_identityBanner.Icon = ContractScanner.Table.Get( _target.Type ).InspectorIcon;
		_scroll.Visible = true;

		_serialized = EditorTypeLibrary.GetSerializedObject( _target );
		_serialized.OnPropertyChanged += OnPropertyChanged;

		// ClassName: commit-time only. OnFinishEdit fires on focus loss/Enter, not per keystroke.
		var classNameProp = _serialized.GetProperty( "ClassName" );
		if ( classNameProp is not null )
			classNameProp.OnFinishEdit += OnClassNameFinishEdit;

		BuildSections();
		RefreshStateStylingFooter();
	}

	private void BuildSections()
	{
		var probeSw = PerfProbes.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;

		_canvas.Layout.Clear( true );
		if ( _serialized is null || _target is null )
		{
			if ( probeSw != null )
				Log.Info( $"{LogPrefix} InspectorPanel.BuildSections: empty ({probeSw.Elapsed.TotalMilliseconds:F2}ms)" );
			return;
		}

		var spec = GroupOrder.Select( g => new AppearanceGroupBuilder.GroupSpec( g.Group, g.Expanded, g.Icon ) ).ToList();
		_builder = new AppearanceGroupBuilder( spec, _expandStore );

		_builder.Build( _canvas.Layout, _serialized, _filterText, ShouldShow );

		_canvas.Layout.AddStretchCell();
		_builder.ApplyReadOnlyStates( _target, ContractScanner.Table, _target.Type );
		_scroll.Update();

		if ( probeSw != null )
			Log.Info( $"{LogPrefix} InspectorPanel.BuildSections[{_target.Type}]: {probeSw.Elapsed.TotalMilliseconds:F2}ms" );
	}

	// Painted when no target is bound. Mirrors canonical Editor Inspector.cs OnPaint behaviour.
	protected override void OnPaint()
	{
		if ( _target is not null ) return;

		Paint.ClearPen();
		Paint.ClearBrush();
		Paint.SetDefaultFont( italic: true );
		Paint.SetPen( Theme.SurfaceLightBackground );

		var r = LocalRect;
		r.Top += 128;
		Paint.DrawText( r, "No control selected.", TextFlag.CenterTop );
	}

	private bool ShouldShow( SerializedProperty p )
	{
		if ( p.HasAttribute<HideAttribute>() )
			return false;

		if ( _target.IsSlot )
			return p.Name is "ClassName" or "Padding";

		var contract    = ContractScanner.Table.Get( _target.Type );
		var isContainer = contract.IsContainer;
		var isRoot      = _target == _document.RootRecord;

		if ( p.Name is "Direction" or "Justify" or "Align" or "Gap" or "Wrap" )
		{
			if ( !isContainer ) return false;
		}

		if ( isContainer && p.Name == "Content" )
			return false;

		var contractFieldNames = new HashSet<string>(
			contract.PayloadFields.Select( f => f.Name ),
			System.StringComparer.Ordinal );

		if ( p.Name is "Source" or "IconName" or "Placeholder" or "CheckboxSize" or "Content" )
		{
			if ( !contractFieldNames.Contains( p.Name ) ) return false;
		}

		var isTextControl = _target.Type is ControlType.Label or ControlType.Button
			or ControlType.TextEntry or ControlType.Checkbox;
		var isIconControl = _target.Type is ControlType.IconPanel;

		if ( p.Name is "OverrideTypography" or "FontSize" or "Color" )
		{
			if ( !isTextControl && !isIconControl ) return false;
		}
		if ( p.Name is "FontFamily" or "FontWeight" or "FontStyleItalic" or "TextTransform" or "LetterSpacing" or "LineHeight" )
		{
			if ( !isTextControl ) return false;
		}
		if ( p.Name == "TextAlign" && _target.Type != ControlType.Label )
			return false;

		if ( isRoot && p.Name == "Margin" ) return false;

		if ( isRoot && p.Name is "FlexGrow" or "FlexShrink" or "FlexBasis" or "AlignSelf"
			or "Position" or "Top" or "Left" or "Right" or "Bottom" )
			return false;

		// RootClassName is the isRoot sentinel; renaming would break serializer.
		if ( isRoot && p.Name == "ClassName" )
			return false;

		if ( !string.IsNullOrEmpty( _filterText ) )
		{
			var ft = _filterText.ToLowerInvariant();
			if ( (p.Name?.ToLowerInvariant().Contains( ft )) == true ) return true;
			if ( (p.DisplayName?.ToLowerInvariant().Contains( ft )) == true ) return true;
			if ( (p.GroupName?.ToLowerInvariant().Contains( ft )) == true ) return true;
			return false;
		}

		return true;
	}

	private void OnFilterEdited( string text )
	{
		_filterText = text ?? "";
		if ( _serialized is null ) return;
		BuildSections();
	}

	private void OnPropertyChanged( SerializedProperty property )
	{
		// ClassName handled on commit via OnClassNameFinishEdit.
		if ( property.Name == "ClassName" )
			return;

		Log.Info( $"{LogPrefix} Inspector.{property.Name} -> {property.GetValue<object>()}" );
		ValueChanged?.Invoke();
		RefreshStateStylingFooter();

		if ( property.Name.StartsWith( "Override" ) )
			_builder?.ApplyReadOnlyStates( _target, ContractScanner.Table, _target.Type );

		if ( property.Name == "Position" )
			Rebuild();
	}

	private void OnClassNameFinishEdit( SerializedProperty property )
	{
		HandleClassNameChange();
	}

	private void HandleClassNameChange()
	{
		if ( _target is null ) return;
		if ( _isRevertingClassName ) return;

		var newName = _target.ClassName;
		if ( newName == _priorClassName )
			return;

		var error = _document.ValidateClassName( newName, _target );
		if ( error is not null )
		{
			Log.Warning( $"{LogPrefix} Inspector.ClassName '{newName}' rejected: {error} -> reverting to '{_priorClassName}'" );

			// bd memory: engine-stringcontrolwidget-onvalue. SetValue re-syncs LineEdit on focus loss.
			_isRevertingClassName = true;
			try
			{
				var prop = _serialized?.GetProperty( "ClassName" );
				if ( prop is not null )
					prop.SetValue<string>( _priorClassName );
				else
					_target.ClassName = _priorClassName;
			}
			finally
			{
				_isRevertingClassName = false;
			}
			return;
		}

		Log.Info( $"{LogPrefix} Inspector.ClassName '{_priorClassName}' -> '{newName}'" );
		var oldName = _priorClassName;
		_priorClassName = newName;
		_identityBanner.Title = BuildHeaderText();
		_identityBanner.Update();
		ClassNameChanged?.Invoke( _target, oldName );
	}

	private string BuildHeaderText()
	{
		if ( _target is null ) return "";
		if ( _target == _document.RootRecord ) return "Canvas";
		return $"{_target.Type}: {_target.ClassName}";
	}

	private void RefreshStateStylingFooter()
	{
		if ( _stateStylingButton is null ) return;
		_stateStylingButton.Enabled = _target is not null;

		var count = _target?.StateRules?.Count ?? 0;
		_stateStylingCount.Text = count switch
		{
			0 => "",
			1 => "1 rule",
			_ => $"{count} rules"
		};
	}
}
