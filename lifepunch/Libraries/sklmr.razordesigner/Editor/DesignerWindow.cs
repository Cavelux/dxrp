using System.Collections.Generic;
using System.Linq;
using Editor;
using Grains.RazorDesigner.Canvas;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Hierarchy;
using Grains.RazorDesigner.Inspector;
using Grains.RazorDesigner.Palette;
using Grains.RazorDesigner.Selection;
using Grains.RazorDesigner.Projection;
using Grains.RazorDesigner.Serialization;
using Grains.RazorDesigner.Templates;
using Grains.RazorDesigner.Validation;
using Sandbox;
using Sandbox.UI;

namespace Grains.RazorDesigner;

[Dock( "Editor", "Razor Designer", "brush" )]
public class DesignerWindow : Widget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const string SplitterOuterCookie = "razordesigner.splitter.outer";
	private const string SplitterLeftCookie  = "razordesigner.splitter.left";
	private const string ThemePathCookie     = "razordesigner.theme.path";
	private const string CanvasSizeCookie    = "razordesigner.canvas.size";
	private const string ChromeHiddenCookie  = "razordesigner.chrome.hidden";
	private const string PreviewTargetCookie = "razordesigner.canvas.target";
	private const string GridShowCookie      = "razordesigner.canvas.grid.show";
	private const string GridSnapCookie      = "razordesigner.canvas.grid.snap";
	private const string GridSizeCookie      = "razordesigner.canvas.grid.size";
	private const string ArtboardFillCookie   = "razordesigner.canvas.artboard.fill";
	private const string ArtboardCustomCookie = "razordesigner.canvas.artboard.custom";

	private static readonly Vector2 DefaultViewport = new( 1920, 1080 );
	private const string FitCookieValue = "fit";
	// First-open grid defaults — show + snap on, 16px step. See RestoreGridFromCookies.
	private const float DefaultGridStep = 16f;

	private Layout _canvasHost;
	private LiveTreeMirror _mirror;
	private DesignerWindowController _controller;
	private PalettePanel _palette;
	private HierarchyPanel _hierarchy;
	private InspectorPanel _inspector;
	private StateStylingWindow _stateStyling;
	private DesignerDocument _document;
	private SelectionController _selection;
	private OverlayController _overlay;
	private CanvasManipulator _manipulator;
	private Splitter _outerSplitter;
	private Splitter _leftStackSplitter;
	private Editor.Option _saveOption;
	private Editor.Option _themeOption;
	private Editor.Option _targetOption;
	private Editor.Option _chromeHiddenOption;
	// Inline canvas-size widgets — replace the prior "Preview ▾" viewport-presets dropdown.
	private Editor.LineEdit _widthEdit;
	private Editor.LineEdit _heightEdit;
	private Editor.Option _fitOption;
	// Inline grid widgets — replace the prior "Grid ▾" dropdown. State mirrored in _gridShow/Snap/Step.
	private Editor.Option _gridShowOption;
	private Editor.Option _gridSnapOption;
	private Editor.LineEdit _gridStepEdit;
	private bool _gridShow;
	private bool _gridSnap;
	private float _gridStep = DefaultGridStep;

	// Artboard fill picker — small "Artboard ▾" button with menu (Dark / Mid / Light / Checker / Custom).
	private Editor.Option _artboardOption;
	private ArtboardFillMode _artboardFill = ArtboardFillMode.Dark;
	private Color _artboardCustom = new( 0.137f, 0.165f, 0.200f );
	// Re-import toolbar button — enabled only when _trackedPath is set and a .razor sibling exists.
	private Editor.Option _reimportOption;
	// W2 — "Code ▾" toolbar button that opens the CodeDialog for State + Parameters.
	private Editor.Option _codeOption;
	// Non-null when a stamp-mismatch banner is pending; shown in inspector diagnostics until dismissed.
	private IReadOnlyList<ValidationDiagnostic> _stampMismatchDiags;
	private bool _chromeHidden;
	private CanvasViewportFrame _viewportFrame;
	private Vector2? _viewport;        // null = Fit
	private Vector2 _lastSizedViewport = new( 1920, 1080 );
	private PreviewTargetMode _previewTarget = PreviewTargetMode.None;
	private PreviewTheme _theme = PreviewTheme.Default;
	private string _trackedPath;
	private Editor.Label _fileLabel;
	private string _lastAppliedCss;
	private string _razorExportFailedSuffix;
	// bd memory: stylesheet-leak. Track sheet by ref; Remove(string) is a no-op on FromString sheets.
	private StyleSheet _previewSheet;

	private static bool _serializerWarmed;
	private static void WarmSerializerAsync()
	{
		if ( _serializerWarmed ) return;
		_serializerWarmed = true;
		System.Threading.Tasks.Task.Run( () =>
		{
			try
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				_ = Serialization.IR.IRWriter.WriteDocument( new DesignerDocument() );
				Log.Info( $"{LogPrefix} serializer warm-up done ({sw.Elapsed.TotalMilliseconds:F0}ms)" );
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"{LogPrefix} serializer warm-up failed: {ex.GetType().Name}: {ex.Message}" );
			}
		} );
	}

	public DesignerWindow( Widget parent ) : base( parent )
	{
		Log.Info( $"{LogPrefix} DesignerWindow ctor" );
		WarmSerializerAsync();

		Layout = Layout.Column();
		Layout.Margin = 0;
		Layout.Spacing = 0;

		MinimumWidth = 960;
		MinimumHeight = 640;

		// ── Toolbar strip: [ fileBar (text+icon) │ divider │ viewBar (icon-only) ……… filename chip ] ──
		var toolStrip = Layout.Row();

		// File operations — text beside icon so the buttons people hunt for are legible.
		var fileBar = new ToolBar( this, "RazorDesignerFileBar" );
		fileBar.SetIconSize( 16 );
		fileBar.ButtonStyle = ToolButtonStyle.TextBesideIcon;
		fileBar.SetStyles( "QToolButton:disabled { color: #4d4d4d; }" );

		var newOption = fileBar.AddOption( "New", "add", () => OnNew() );
		newOption.ToolTip = "New design (clears canvas)  [Ctrl+N]";
		newOption.StatusTip = "Discard the current design and start fresh.";

		var openOption = fileBar.AddOption( "Open", "folder_open", () => OnOpen() );
		openOption.ToolTip = "Open design (.razor or .razor.json)  [Ctrl+O]";
		openOption.StatusTip = "Replace current design with one loaded from a .razor or .razor.json file.";

		_saveOption = fileBar.AddOption( "Save", "save", () => OnSave() );
		_saveOption.ToolTip = "Save design (.razor + .razor.scss)  [Ctrl+S]";
		_saveOption.StatusTip = "Write .razor markup and paired .razor.scss stylesheet.";

		fileBar.AddSeparator();

		_reimportOption = fileBar.AddOption( "Re-import", "upload_file", () => OnReimportFromRazor() );
		_reimportOption.ToolTip = "File → Re-import from .razor  (lift hand-edits back into the IR)";
		_reimportOption.StatusTip = "Re-parse the .razor file via LegacyRazorImporter. Shows a loss-preview if differences are found.";
		_reimportOption.Enabled = false;   // enabled once a file with a .razor sibling is tracked

		toolStrip.Add( fileBar );

		var toolStripDivider = new Widget( this );
		toolStripDivider.FixedWidth = 1;
		toolStripDivider.SetSizeMode( SizeMode.Default, SizeMode.CanGrow );
		toolStripDivider.OnPaintOverride = () =>
		{
			Paint.SetBrushAndPen( Color.Parse( "#1d1d1d" ).Value );
			Paint.DrawRect( toolStripDivider.LocalRect );
			return true;   // suppress default OnPaint
		};
		toolStrip.AddSpacingCell( 4 );
		toolStrip.Add( toolStripDivider );
		toolStrip.AddSpacingCell( 4 );

		var viewBar = new ToolBar( this, "RazorDesignerViewBar" );
		viewBar.SetIconSize( 16 );
		viewBar.ButtonStyle = ToolButtonStyle.TextBesideIcon;

		_themeOption = viewBar.AddOption( "Theme ▾", "palette", OnThemeButton );
		_themeOption.StatusTip = "Choose preview theme — Default or load a custom .scss.";
		RestoreThemeFromCookie();
		UpdateThemeOptionTooltip();

		toolStrip.Add( viewBar );

		RestoreViewportFromCookie();

		toolStrip.AddSpacingCell( 8 );
		_widthEdit = MakeNumericFieldEdit( _lastSizedViewport.x, "Canvas width (logical pixels)" );
		_widthEdit.EditingFinished += () => OnSizeFieldCommitted();
		toolStrip.Add( _widthEdit );

		var sizeSeparator = new Editor.Label( this ) { Text = "×" };
		sizeSeparator.SetStyles( "color: #777; padding: 0 4px;" );
		toolStrip.Add( sizeSeparator );

		_heightEdit = MakeNumericFieldEdit( _lastSizedViewport.y, "Canvas height (logical pixels)" );
		_heightEdit.EditingFinished += () => OnSizeFieldCommitted();
		toolStrip.Add( _heightEdit );

		toolStrip.AddSpacingCell( 4 );
		var sizeBar = new ToolBar( this, "RazorDesignerSizeBar" );
		sizeBar.SetIconSize( 16 );
		sizeBar.ButtonStyle = ToolButtonStyle.TextBesideIcon;
		_fitOption = sizeBar.AddOption( "Fit", "fit_screen", OnFitToggle );
		_fitOption.Checkable = true;
		_fitOption.Checked = !_viewport.HasValue;
		_fitOption.ToolTip = "Fit canvas to the pane (no fixed viewport size).";
		_fitOption.StatusTip = "When on, the canvas fills the pane instead of rendering at a fixed pixel size.";
		toolStrip.Add( sizeBar );

		// ── viewBar2 (mid): Target ▾, Refresh, Chrome ──
		toolStrip.AddSpacingCell( 6 );
		var viewBar2 = new ToolBar( this, "RazorDesignerViewBar2" );
		viewBar2.SetIconSize( 16 );
		viewBar2.ButtonStyle = ToolButtonStyle.TextBesideIcon;

		// "Target ▾" — render-target Scale pin (None / ScreenPanel / WorldPanel) + Reset pan & zoom.
		_targetOption = viewBar2.AddOption( "Target ▾", "tv", OnTargetButton );
		_targetOption.StatusTip = "Pick the runtime render-target Scale pin or reset pan & zoom.";
		RestorePreviewTargetFromCookie();
		UpdateTargetOptionTooltip();

		var refreshOption = viewBar2.AddOption( "Refresh", "refresh", () => Refresh() );
		refreshOption.ToolTip = "Refresh preview (also fires automatically on code reload)";
		refreshOption.StatusTip = "Re-mirror the preview from the document and re-apply the stylesheet.";

		_chromeHiddenOption = viewBar2.AddOption( "Chrome", "visibility", OnChromeToggle );
		_chromeHiddenOption.Checkable = true;
		_chromeHidden = EditorCookie.Get<bool>( ChromeHiddenCookie, false );
		_chromeHiddenOption.Checked = _chromeHidden;
		_chromeHiddenOption.ToolTip = "Toggle scaffold chrome (borders, empty-container labels, empty-image placeholder, selection outline).";
		_chromeHiddenOption.StatusTip = "When off, the canvas previews the design as it would render outside the editor.";

		toolStrip.Add( viewBar2 );

		RestoreGridFromCookies();

		toolStrip.AddSpacingCell( 6 );
		var gridBar = new ToolBar( this, "RazorDesignerGridBar" );
		gridBar.SetIconSize( 16 );
		gridBar.ButtonStyle = ToolButtonStyle.IconOnly;
		_gridShowOption = gridBar.AddOption( "", "grid_on", OnGridShowToggle );
		_gridShowOption.Checkable = true;
		_gridShowOption.Checked = _gridShow;
		_gridShowOption.ToolTip = "Show canvas grid";
		_gridShowOption.StatusTip = "Toggle visibility of the canvas grid overlay.";
		toolStrip.Add( gridBar );

		toolStrip.AddSpacingCell( 2 );
		_gridStepEdit = MakeNumericFieldEdit( _gridStep, "Grid step (CSS pixels)" );
		_gridStepEdit.EditingFinished += () => OnGridStepCommitted();
		toolStrip.Add( _gridStepEdit );

		toolStrip.AddSpacingCell( 2 );
		var snapBar = new ToolBar( this, "RazorDesignerSnapBar" );
		snapBar.SetIconSize( 16 );
		snapBar.ButtonStyle = ToolButtonStyle.IconOnly;
		_gridSnapOption = snapBar.AddOption( "", "straighten", OnGridSnapToggle );
		_gridSnapOption.Checkable = true;
		_gridSnapOption.Checked = _gridSnap;
		_gridSnapOption.ToolTip = "Snap drag to grid";
		_gridSnapOption.StatusTip = "Toggle snap-to-grid for drag operations.";
		toolStrip.Add( snapBar );

		RestoreArtboardFillFromCookies();

		toolStrip.AddSpacingCell( 6 );
		var artboardBar = new ToolBar( this, "RazorDesignerArtboardBar" );
		artboardBar.SetIconSize( 16 );
		artboardBar.ButtonStyle = ToolButtonStyle.TextBesideIcon;
		_artboardOption = artboardBar.AddOption( "Artboard ▾", "format_paint", OnArtboardButton );
		_artboardOption.StatusTip = "Pick the artboard backdrop fill (Dark / Mid / Light / Checker / Custom).";
		UpdateArtboardOptionTooltip();
		toolStrip.Add( artboardBar );

		// W2 — Code ▾ bar. Divider after viewBar mirrors the fileBar/viewBar pattern.
		var codeBarDivider = new Widget( this );
		codeBarDivider.FixedWidth = 1;
		codeBarDivider.SetSizeMode( SizeMode.Default, SizeMode.CanGrow );
		codeBarDivider.OnPaintOverride = () =>
		{
			Paint.SetBrushAndPen( Color.Parse( "#1d1d1d" ).Value );
			Paint.DrawRect( codeBarDivider.LocalRect );
			return true;
		};
		toolStrip.AddSpacingCell( 4 );
		toolStrip.Add( codeBarDivider );
		toolStrip.AddSpacingCell( 4 );

		var codeBar = new ToolBar( this, "RazorDesignerCodeBar" );
		codeBar.SetIconSize( 16 );
		codeBar.ButtonStyle = ToolButtonStyle.TextBesideIcon;

		_codeOption = codeBar.AddOption( "Code ▾", "code", OnCodeButton );
		_codeOption.ToolTip = "Document-scoped State & Parameters (W2). Methods/Lifecycle/Bindings land in W3+.";
		_codeOption.StatusTip = "Open the Code dialog to declare State (private fields) and Parameters (public [Parameter] properties).";

		toolStrip.Add( codeBar );

		// Push the filename chip to the far right.
		toolStrip.AddStretchCell();

		_fileLabel = new Editor.Label( this );
		_fileLabel.SetStyles( "color: #888; padding-right: 8px;" );
		toolStrip.Add( _fileLabel );

		Layout.Add( toolStrip );

		_document = new DesignerDocument();
		_mirror = new LiveTreeMirror(
			_document,
			() => Canvas?.DesignerScene?.Root,
			() => _chromeHidden );

		_outerSplitter = new Splitter( this );
		_outerSplitter.IsHorizontal = true;
		StyleSplitter( _outerSplitter );
		Layout.Add( _outerSplitter, 1 );

		// IsHorizontal=false is a no-op in engine (setter always assigns Horizontal); IsVertical=true is the correct flip.
		_leftStackSplitter = new Splitter( this );
		_leftStackSplitter.IsVertical = true;
		_leftStackSplitter.MinimumWidth = 200;
		_leftStackSplitter.MaximumWidth = 280;
		StyleSplitter( _leftStackSplitter );

		_hierarchy = new HierarchyPanel( this, _document );
		_leftStackSplitter.AddWidget( _hierarchy );

		_palette = new PalettePanel( this );
		var paletteScroll = new ScrollArea( this );
		paletteScroll.Canvas = _palette;
		_leftStackSplitter.AddWidget( paletteScroll );

		_leftStackSplitter.SetStretch( 0, 1 );
		_leftStackSplitter.SetStretch( 1, 1 );
		_outerSplitter.AddWidget( _leftStackSplitter );

		var canvasContainer = new Widget( this );
		canvasContainer.Layout = Layout.Column();
		canvasContainer.SetSizeMode( SizeMode.CanGrow, SizeMode.CanGrow );
		canvasContainer.MinimumWidth = 320;
		_canvasHost = canvasContainer.Layout;
		_outerSplitter.AddWidget( canvasContainer );

		_inspector = new InspectorPanel( this, _document );
		_inspector.MinimumWidth = 280;
		_inspector.MaximumWidth = 380;
		_inspector.OpenStateStylingRequested = OpenStateStylingForTarget;
		_outerSplitter.AddWidget( _inspector );

		_outerSplitter.SetStretch( 0, 0 );
		_outerSplitter.SetStretch( 1, 10 );
		_outerSplitter.SetStretch( 2, 0 );
		_outerSplitter.SetCollapsible( 0, false );
		_outerSplitter.SetCollapsible( 1, false );
		_outerSplitter.SetCollapsible( 2, false );
		_leftStackSplitter.SetCollapsible( 0, false );
		_leftStackSplitter.SetCollapsible( 1, false );

		RestoreSplitterState();
		ApplyDefaultSplitterSizes();

		_selection = new SelectionController( _document );
		_overlay = new OverlayController( _selection );

		_controller = new DesignerWindowController(
			_document,
			_mirror,
			_selection,
			_overlay,
			_hierarchy,
			_inspector,
			_palette,
			() => Canvas.DpiScale,
			() => ApplyPreviewStylesheet(),
			this,
			onValidated: () =>
			{
				var diags = new Validator().Validate( new RecordNode( _document.RootRecord ) );
				_inspector.SetDiagnostics( diags );
			} );

		_manipulator = new CanvasManipulator(
			_selection,
			_overlay,
			() => Canvas?.DesignerScene,
			() => Canvas?.DpiScale ?? 1f,
			r => _controller.CommitCanvasManipulation( r ),
			() => (_gridSnap, _gridStep) );
		_controller.Manipulator = _manipulator;

		_palette.TypeAddRequested += _controller.OnPaletteTypeAddRequested;
		_palette.TemplateAddRequested += _controller.OnPaletteTemplateAddRequested;
		_inspector.ValueChanged += _controller.OnInspectorValueChanged;
		_inspector.ClassNameChanged += _controller.OnInspectorClassNameChanged;
		_hierarchy.RecordSelected += _controller.OnHierarchyRowClicked;
		_hierarchy.SelectionChanged += _controller.OnHierarchySelectionChanged;
		_hierarchy.RecordMoved += _controller.OnRecordMoved;
		_hierarchy.RecordsDeleteRequested += _controller.OnHierarchyDeleteRequested;
		_hierarchy.RecordCreated += _controller.OnHierarchyRecordCreated;
		_hierarchy.RecordsCutRequested += _controller.OnHierarchyCutRequested;
		_hierarchy.RecordsCopyRequested += _controller.OnHierarchyCopyRequested;
		_hierarchy.RecordsDuplicateRequested += _controller.DuplicateRecords;
		_hierarchy.RecordPasteRequested += _controller.OnHierarchyPasteRequested;
		_hierarchy.RecordRenameRequested += _controller.OnHierarchyRenameRequested;
		_hierarchy.RecordAddRequested += _controller.OnHierarchyAddRequested;
		_hierarchy.RecordsSaveAsTemplateRequested += _controller.OnHierarchySaveAsTemplateRequested;
		_hierarchy.TemplateDropRequested += _controller.OnHierarchyTemplateDropRequested;
		_hierarchy.CustomStylesRequested += _controller.OpenCustomStylesDialog;
		

		// TabOrClickOrWheel is required for the dock to receive Delete keypresses.
		FocusMode = FocusMode.TabOrClickOrWheel;

		UpdateFileLabel();   // initial "Untitled" (chip + fields now exist)
		Build();
	}

	public DesignerCanvas Canvas { get; private set; }

	public string TrackedFilename => string.IsNullOrEmpty( _trackedPath )
		? "Untitled"
		: System.IO.Path.GetFileName( _trackedPath );

	// Splitter handles default to invisible; this paints them with a tinted divider so users can grab them.
	private static void StyleSplitter( Splitter splitter )
	{
		splitter.HandleWidth = 4;
		splitter.SetStyles(
			"QSplitter::handle { background-color: #1d1d1d; }" +
			"QSplitter::handle:hover { background-color: #4aa0ff; }" +
			"QSplitter::handle:pressed { background-color: #4aa0ff; }" );
	}

	[EditorEvent.Hotload]
	public void Refresh()
	{
		Log.Info( $"{LogPrefix} === Refresh ===" );

		if ( Canvas is not null && _controller is not null )
		{
			_lastAppliedCss = null;
			_previewSheet = null;
			_controller.RepopulateMirrorAndRefresh();
			return;
		}

		Build();
	}

	// Frame-gated so we don't redundantly bash Enabled every paint; only flips when state changes.
	[EditorEvent.Frame]
	private void OnFrame()
	{
		if ( _saveOption is null || _document is null ) return;
		var canSave = _document.RootRecord.Children.Count > 0
				   || ( _document.Wiring is { Symbols.Count: > 0 } );
		if ( _saveOption.Enabled != canSave )
			_saveOption.Enabled = canSave;

		// Re-import button: enabled when there's a tracked path AND a .razor sibling on disk.
		if ( _reimportOption is not null )
		{
			var stem = _trackedPath is not null && _trackedPath.EndsWith( ".razor.json", System.StringComparison.OrdinalIgnoreCase )
				? _trackedPath[..^5]
				: _trackedPath;
			var canReimport = stem is not null && System.IO.File.Exists( stem );
			if ( _reimportOption.Enabled != canReimport )
				_reimportOption.Enabled = canReimport;
		}
	}

	private bool _outerSplitterRestored;
	private bool _leftSplitterRestored;

	private void RestoreSplitterState()
	{
		var outer = EditorCookie.Get<string>( SplitterOuterCookie, null );
		if ( !string.IsNullOrEmpty( outer ) )
		{
			_outerSplitter?.RestoreState( outer );
			_outerSplitterRestored = true;
			Log.Info( $"{LogPrefix} Splitter state restored (outer)" );
		}
		var left = EditorCookie.Get<string>( SplitterLeftCookie, null );
		if ( !string.IsNullOrEmpty( left ) )
		{
			_leftStackSplitter?.RestoreState( left );
			_leftSplitterRestored = true;
			Log.Info( $"{LogPrefix} Splitter state restored (left)" );
		}
	}

	private void ApplyDefaultSplitterSizes()
	{
		if ( !_outerSplitterRestored )
		{
			if ( _leftStackSplitter is not null ) _leftStackSplitter.Width = 240f;
			if ( _inspector is not null ) _inspector.Width = 320f;
			Log.Info( $"{LogPrefix} Outer splitter: no saved state, applied default widths (left=240, inspector=320)" );
		}
		// Left stack is hierarchy / palette stacked vertically — split evenly when fresh.
		if ( !_leftSplitterRestored && _hierarchy is not null )
		{
			Log.Info( $"{LogPrefix} Left splitter: no saved state, even split (Qt default with stretch 1/1 already correct)" );
		}
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();
		try
		{
			if ( _outerSplitter is not null )
				EditorCookie.Set( SplitterOuterCookie, _outerSplitter.SaveState() );
			if ( _leftStackSplitter is not null )
				EditorCookie.Set( SplitterLeftCookie, _leftStackSplitter.SaveState() );
			Log.Info( $"{LogPrefix} Splitter state saved on destroy" );
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"{LogPrefix} Splitter state save failed: {ex.Message}" );
		}
	}

	private void OnThemeButton()
	{
		var menu = new Editor.Menu( this );

		// Defaults submenu: engine baseline + every .scss bundled in the addon's Themes/.
		var defaults = menu.AddMenu( "Default", "auto_awesome" );
		defaults.AddOption( new Editor.Option
		{
			Text = "Default theme",
			Icon = "auto_awesome",
			Checkable = true,
			Checked = _theme.IsDefault,
			Triggered = () => SetTheme( PreviewTheme.Default ),
		} );
		foreach ( var bundled in EnumerateBundledThemes() )
		{
			// Capture loop locals — Triggered is a closure.
			var path = bundled.Path;
			var name = bundled.Name;
			defaults.AddOption( new Editor.Option
			{
				Text = name,
				Icon = "palette",
				Checkable = true,
				Checked = string.Equals( _theme.Source, path, System.StringComparison.OrdinalIgnoreCase ),
				Triggered = () => SetTheme( PreviewTheme.FromFile( path ) ),
			} );
		}

		// Custom submenu: user-loaded .scss off disk.
		var custom = menu.AddMenu( "Custom", "folder_open" );
		custom.AddOption( "Load custom .scss…", "folder_open", LoadCustomThemeViaDialog );

		// Reload at the root, applies to whichever non-default theme is active (bundled or custom).
		if ( !_theme.IsDefault )
		{
			menu.AddSeparator();
			menu.AddOption( $"Reload {_theme.Name}", "refresh", () => SetTheme( PreviewTheme.FromFile( _theme.Source ) ) );
		}

		menu.OpenAtCursor();
	}

	private static readonly (string Ident, string Subpath)[] BundledThemeRoots = new[]
	{
		( "razordesigner",        "Assets/Themes" ),
		( "grains_razordesigner", "Libraries/xaz.razordesigner/Assets/Themes" ),
	};

	private static string BundledThemesDirectoryPath()
	{
		foreach ( var (ident, subpath) in BundledThemeRoots )
		{
			var root = System.Linq.Enumerable
				.FirstOrDefault( EditorUtility.Projects.GetAll(), p => string.Equals( p.Config?.Ident, ident, System.StringComparison.OrdinalIgnoreCase ) )
				?.GetRootPath();
			if ( string.IsNullOrEmpty( root ) ) continue;
			var dir = System.IO.Path.Combine( root, subpath );
			if ( System.IO.Directory.Exists( dir ) ) return dir;
		}
		return null;
	}

	private static IEnumerable<(string Name, string Path)> EnumerateBundledThemes()
	{
		var dir = BundledThemesDirectoryPath();
		if ( string.IsNullOrEmpty( dir ) || !System.IO.Directory.Exists( dir ) )
			yield break;
		foreach ( var f in System.IO.Directory.EnumerateFiles( dir, "*.scss" ) )
			yield return ( DisplayNameFromFile( f ), f );
	}

	// "github-dark.scss" -> "Github Dark". Cheap title-case on - and _ separators; .scss stripped.
	private static string DisplayNameFromFile( string path )
	{
		var name = System.IO.Path.GetFileNameWithoutExtension( path );
		var parts = name.Split( '-', '_' );
		for ( int i = 0; i < parts.Length; i++ )
		{
			if ( parts[i].Length == 0 ) continue;
			parts[i] = char.ToUpperInvariant( parts[i][0] ) + parts[i].Substring( 1 );
		}
		return string.Join( ' ', parts );
	}

	private void LoadCustomThemeViaDialog()
	{
		var dialog = new FileDialog( null )
		{
			Title = "Load preview theme (.scss)",
			DefaultSuffix = ".scss",
		};
		dialog.SetFindFile();
		dialog.SetModeOpen();
		dialog.SetNameFilter( "Stylesheet (*.scss)" );

		if ( !dialog.Execute() )
		{
			Log.Info( $"{LogPrefix} Theme load cancelled" );
			return;
		}

		SetTheme( PreviewTheme.FromFile( dialog.SelectedFile ) );
	}

	private void SetTheme( PreviewTheme theme )
	{
		_theme = theme ?? PreviewTheme.Default;
		EditorCookie.Set( ThemePathCookie, _theme.IsDefault ? "" : _theme.Source );
		UpdateThemeOptionTooltip();
		// Force a re-apply on the next frame even if document CSS hasn't changed.
		_lastAppliedCss = null;
		WarnOnScssOnlySyntax( _theme );
		ApplyPreviewStylesheet();
		Log.Info( $"{LogPrefix} Theme set: {_theme.Name} ({_theme.Source})" );
	}

	private static void WarnOnScssOnlySyntax( PreviewTheme theme )
	{
		if ( theme is null || theme.IsDefault || string.IsNullOrEmpty( theme.Css ) ) return;
		var css = theme.Css;
		var hits = new List<string>();
		if ( css.Contains( "&:" ) || css.Contains( "& " ) || css.Contains( "&." ) || css.Contains( "&#" ) )
			hits.Add( "nested selectors via `&`" );
		if ( css.Contains( "@mixin" ) )    hits.Add( "@mixin" );
		if ( css.Contains( "@include" ) )  hits.Add( "@include" );
		if ( css.Contains( "@import" ) )   hits.Add( "@import" );
		if ( css.Contains( "@function" ) ) hits.Add( "@function" );
		if ( css.Contains( "@if" ) )       hits.Add( "@if" );
		if ( css.Contains( "@each" ) )     hits.Add( "@each" );
		// `$ident:` SCSS variable assignment heuristic — common pattern at file top.
		if ( System.Text.RegularExpressions.Regex.IsMatch( css, @"\$[A-Za-z_][\w-]*\s*:" ) )
			hits.Add( "$variables" );
		if ( hits.Count == 0 ) return;
		Log.Warning( $"{LogPrefix} Theme \"{theme.Name}\" contains SCSS-only syntax that will not parse: {string.Join( ", ", hits )}. Flatten to plain CSS." );
	}

	private void RestoreThemeFromCookie()
	{
		var path = EditorCookie.Get<string>( ThemePathCookie, null );
		if ( string.IsNullOrEmpty( path ) ) return;
		if ( !System.IO.File.Exists( path ) )
		{
			Log.Warning( $"{LogPrefix} Saved theme path no longer exists: {path}; staying on Default" );
			return;
		}
		_theme = PreviewTheme.FromFile( path );
	}

	private void UpdateThemeOptionTooltip()
	{
		if ( _themeOption is null ) return;
		_themeOption.ToolTip = _theme.IsDefault
			? "Theme: Default (click to change)"
			: $"Theme: {_theme.Name} (click to change)";
	}

	private void OnTargetButton()
	{
		var menu = new Editor.Menu( this );
		AddRenderTargetSection( menu );
		menu.AddSeparator();
		menu.AddOption( "Reset pan & zoom", "filter_center_focus", () => _viewportFrame?.ResetView() );
		menu.OpenAtCursor();
	}

	private Editor.LineEdit MakeNumericFieldEdit( float initial, string tooltip )
	{
		var edit = new Editor.LineEdit( this )
		{
			Text = ((int)System.MathF.Round( initial )).ToString(),
			ToolTip = tooltip,
		};
		edit.RegexValidator = "[0-9]+";
		edit.FixedWidth = 52;
		return edit;
	}

	private void OnSizeFieldCommitted()
	{
		if ( !TryParseSizeFields( out var size ) )
		{
			if ( _widthEdit is not null )  _widthEdit.Text  = ((int)_lastSizedViewport.x).ToString();
			if ( _heightEdit is not null ) _heightEdit.Text = ((int)_lastSizedViewport.y).ToString();
			return;
		}
		_lastSizedViewport = size;
		SetViewport( size );
		if ( _fitOption is not null ) _fitOption.Checked = false;
	}

	// Fit toolbar toggle. Editor.Option with Checkable=true updates Checked before firing — read it.
	private void OnFitToggle()
	{
		var fit = _fitOption.Checked;
		if ( fit )
		{
			// Preserve the typed values so flipping Fit back off returns to them.
			if ( TryParseSizeFields( out var current ) ) _lastSizedViewport = current;
			SetViewport( null );
		}
		else
		{
			// Flipping Fit off applies whatever's in the fields right now (falling back to last sized).
			var size = TryParseSizeFields( out var current ) ? current : _lastSizedViewport;
			_lastSizedViewport = size;
			SetViewport( size );
		}
	}

	private bool TryParseSizeFields( out Vector2 size )
	{
		size = Vector2.Zero;
		if ( _widthEdit is null || _heightEdit is null ) return false;
		if ( !int.TryParse( _widthEdit.Text, out var w ) || w < 16 || w > 16384 ) return false;
		if ( !int.TryParse( _heightEdit.Text, out var h ) || h < 16 || h > 16384 ) return false;
		size = new Vector2( w, h );
		return true;
	}

	// Editor.Option with Checkable=true updates Checked before firing the callback.
	private void OnChromeToggle()
	{
		_chromeHidden = _chromeHiddenOption.Checked;
		EditorCookie.Set( ChromeHiddenCookie, _chromeHidden );
		ApplyChromeVisibility();
		Log.Info( $"{LogPrefix} Chrome {(_chromeHidden ? "hidden (real view)" : "shown (scaffold)")}" );
	}

	private void OnGridShowToggle()
	{
		SetGrid( _gridShowOption.Checked, _gridSnap, _gridStep );
	}

	private void OnGridSnapToggle()
	{
		SetGrid( _gridShow, _gridSnapOption.Checked, _gridStep );
	}

	private void OnGridStepCommitted()
	{
		if ( _gridStepEdit is null ) return;
		if ( !int.TryParse( _gridStepEdit.Text, out var step ) || step < 1 || step > 999 )
		{
			// Invalid input — bounce the field back to the last good value.
			_gridStepEdit.Text = ((int)_gridStep).ToString();
			return;
		}
		SetGrid( _gridShow, _gridSnap, step );
	}

	private void SetGrid( bool show, bool snap, float step )
	{
		_gridShow = show;
		_gridSnap = snap;
		_gridStep = step;
		EditorCookie.Set( GridShowCookie, show );
		EditorCookie.Set( GridSnapCookie, snap );
		EditorCookie.Set( GridSizeCookie, step );
		if ( _viewportFrame is not null )
			_viewportFrame.SetGrid( new CanvasViewportFrame.GridSettings( show, snap, step ) );
		Log.Info( $"{LogPrefix} Grid set: show={show} snap={snap} step={(int)step}px" );
	}

	private void RestoreGridFromCookies()
	{
		_gridShow = EditorCookie.Get<bool>( GridShowCookie, true );
		_gridSnap = EditorCookie.Get<bool>( GridSnapCookie, true );
		var rawStep = EditorCookie.Get<float>( GridSizeCookie, DefaultGridStep );
		_gridStep = (rawStep >= 1f && rawStep <= 999f) ? rawStep : DefaultGridStep;
		if ( _gridStepEdit is not null )
			_gridStepEdit.Text = ((int)_gridStep).ToString();
		if ( _gridShowOption is not null )
			_gridShowOption.Checked = _gridShow;
		if ( _gridSnapOption is not null )
			_gridSnapOption.Checked = _gridSnap;
		if ( _viewportFrame is not null )
			_viewportFrame.SetGrid( new CanvasViewportFrame.GridSettings( _gridShow, _gridSnap, _gridStep ) );
		Log.Info( $"{LogPrefix} Grid restored: show={_gridShow} snap={_gridSnap} step={(int)_gridStep}px" );
	}

	// ── Artboard fill ──

	private void OnArtboardButton()
	{
		var menu = new Editor.Menu( this );
		AddArtboardOption( menu, ArtboardFillMode.Dark );
		AddArtboardOption( menu, ArtboardFillMode.Mid );
		AddArtboardOption( menu, ArtboardFillMode.Light );
		AddArtboardOption( menu, ArtboardFillMode.Checker );
		menu.AddSeparator();
		menu.AddOption( new Editor.Option
		{
			Text = "Custom…",
			Icon = _artboardFill == ArtboardFillMode.Custom ? "check" : "palette",
			Triggered = OpenArtboardCustomPicker,
		} );
		menu.OpenAtCursor();
	}

	private void AddArtboardOption( Editor.Menu menu, ArtboardFillMode mode )
	{
		var current = _artboardFill == mode;
		menu.AddOption( new Editor.Option
		{
			Text = mode.DisplayLabel(),
			Icon = current ? "check" : "",
			Checkable = true,
			Checked = current,
			Triggered = () => SetArtboardFill( mode, _artboardCustom ),
		} );
	}

	private void OpenArtboardCustomPicker()
	{
		Editor.ColorPicker.OpenColorPopup( _artboardCustom, color =>
		{
			SetArtboardFill( ArtboardFillMode.Custom, color );
		} );
	}

	private void SetArtboardFill( ArtboardFillMode mode, Color custom )
	{
		_artboardFill = mode;
		_artboardCustom = custom;
		EditorCookie.Set( ArtboardFillCookie, mode.ToString() );
		EditorCookie.Set( ArtboardCustomCookie, custom.Hex );
		PushArtboardFillToScene();
		UpdateArtboardOptionTooltip();
		Log.Info( $"{LogPrefix} Artboard fill set: {mode.DisplayLabel()}{(mode == ArtboardFillMode.Custom ? $" ({custom.Hex})" : "")}" );
	}

	private void PushArtboardFillToScene()
	{
		if ( Canvas?.DesignerScene is not { } scene ) return;
		scene.ArtboardFill = _artboardFill;
		scene.ArtboardCustomColor = _artboardCustom;
	}

	private void RestoreArtboardFillFromCookies()
	{
		var rawMode = EditorCookie.Get<string>( ArtboardFillCookie, null );
		if ( !string.IsNullOrEmpty( rawMode ) && System.Enum.TryParse<ArtboardFillMode>( rawMode, ignoreCase: true, out var parsed ) )
			_artboardFill = parsed;
		else
			_artboardFill = ArtboardFillMode.Dark;

		var rawCustom = EditorCookie.Get<string>( ArtboardCustomCookie, null );
		if ( !string.IsNullOrEmpty( rawCustom ) )
		{
			var c = Color.Parse( rawCustom );
			if ( c.HasValue ) _artboardCustom = c.Value;
		}

		PushArtboardFillToScene();
		Log.Info( $"{LogPrefix} Artboard fill restored: {_artboardFill.DisplayLabel()}" );
	}

	private void UpdateArtboardOptionTooltip()
	{
		if ( _artboardOption is null ) return;
		_artboardOption.ToolTip = _artboardFill == ArtboardFillMode.Custom
			? $"Artboard: Custom ({_artboardCustom.Hex})  (click to change)"
			: $"Artboard: {_artboardFill.DisplayLabel()}  (click to change)";
	}

	private void ApplyChromeVisibility()
	{
		var root = Canvas?.DesignerScene?.Root;
		if ( root is null || !root.IsValid ) return;
		root.SetClass( LiveTreeMirror.ChromeHiddenClass, _chromeHidden );
	}

	private void SetViewport( Vector2? size )
	{
		_viewport = size;
		EditorCookie.Set( CanvasSizeCookie,
			size.HasValue ? $"{(int)size.Value.x}x{(int)size.Value.y}" : FitCookieValue );
		if ( _viewportFrame is not null )
			_viewportFrame.Viewport = size;
		if ( _fitOption is not null )
			_fitOption.Checked = !size.HasValue;
		Log.Info( $"{LogPrefix} Viewport set: {(size.HasValue ? $"{(int)size.Value.x}×{(int)size.Value.y}" : "Fit")}" );
	}

	private void RestoreViewportFromCookie()
	{
		var raw = EditorCookie.Get<string>( CanvasSizeCookie, null );

		if ( string.IsNullOrEmpty( raw ) )
		{
			_viewport = DefaultViewport;
			_lastSizedViewport = DefaultViewport;
			Log.Info( $"{LogPrefix} Viewport default (no cookie): {(int)DefaultViewport.x}×{(int)DefaultViewport.y}" );
			return;
		}
		if ( raw == FitCookieValue )
		{
			_viewport = null;
			// Keep _lastSizedViewport at its default 1920×1080 — flipping Fit off lands somewhere sane.
			Log.Info( $"{LogPrefix} Viewport restored: Fit" );
			return;
		}
		var parts = raw.Split( 'x' );
		if ( parts.Length == 2
			&& int.TryParse( parts[0], out var w )
			&& int.TryParse( parts[1], out var h )
			&& w >= 16 && h >= 16 )
		{
			_viewport = new Vector2( w, h );
			_lastSizedViewport = new Vector2( w, h );
			Log.Info( $"{LogPrefix} Viewport restored: {w}×{h}" );
		}
		else
		{
			Log.Warning( $"{LogPrefix} Saved viewport unparseable: \"{raw}\"; falling back to Fit" );
			_viewport = null;
		}
	}

	private void UpdateTargetOptionTooltip()
	{
		if ( _targetOption is null ) return;
		_targetOption.ToolTip = $"Render target: {_previewTarget.DisplayLabel()}  (click to change · Reset pan & zoom available)";
	}

	private void AddRenderTargetSection( Editor.Menu menu )
	{
		void AddTarget( PreviewTargetMode mode )
		{
			var current = _previewTarget == mode;
			menu.AddOption( new Editor.Option
			{
				Text = mode.DisplayLabel(),
				Icon = current ? "check" : "",
				Checkable = true,
				Checked = current,
				Triggered = () => SetPreviewTarget( mode ),
			} );
		}

		menu.AddHeading( "Render target" );
		AddTarget( PreviewTargetMode.None );
		AddTarget( PreviewTargetMode.ScreenPanel );
		AddTarget( PreviewTargetMode.WorldPanel );
	}

	private void SetPreviewTarget( PreviewTargetMode mode )
	{
		if ( _previewTarget == mode ) return;
		_previewTarget = mode;
		EditorCookie.Set( PreviewTargetCookie, mode.ToString() );
		if ( _viewportFrame is not null )
			_viewportFrame.PreviewTarget = mode;
		UpdateTargetOptionTooltip();
		Log.Info( $"{LogPrefix} Preview target set: {mode.DisplayLabel()}" );
	}

	private void RestorePreviewTargetFromCookie()
	{
		var raw = EditorCookie.Get<string>( PreviewTargetCookie, null );
		if ( string.IsNullOrEmpty( raw ) )
		{
			_previewTarget = PreviewTargetMode.None;
			return;
		}
		if ( System.Enum.TryParse<PreviewTargetMode>( raw, ignoreCase: true, out var mode ) )
		{
			_previewTarget = mode;
			Log.Info( $"{LogPrefix} Preview target restored: {mode.DisplayLabel()}" );
		}
		else
		{
			Log.Warning( $"{LogPrefix} Saved preview target unparseable: \"{raw}\"; defaulting to None" );
			_previewTarget = PreviewTargetMode.None;
		}
	}

	[Shortcut( "razordesigner.new", "CTRL+N", ShortcutType.Window )]
	private void ShortcutNew() => OnNew();

	[Shortcut( "razordesigner.open", "CTRL+O", ShortcutType.Window )]
	private void ShortcutOpen() => OnOpen();

	[Shortcut( "razordesigner.save", "CTRL+S", ShortcutType.Window )]
	private void ShortcutSave() => OnSave();

	[Shortcut( "razordesigner.duplicate", "CTRL+D", ShortcutType.Window )]
	private void ShortcutDuplicate() => OnDuplicate();

	[Shortcut( "razordesigner.copy", "CTRL+C", ShortcutType.Window )]
	private void ShortcutCopy() => OnCopy();

	[Shortcut( "razordesigner.paste", "CTRL+V", ShortcutType.Window )]
	private void ShortcutPaste() => OnPaste();

	private void Build()
	{
		Log.Info( $"{LogPrefix} Build: rebuilding canvas" );
		// New canvas = new RootPanel = empty StyleSheet; drop caches tied to old root.
		_lastAppliedCss = null;
		_previewSheet = null;
		_canvasHost.Clear( true );

		_viewportFrame = new CanvasViewportFrame( this );
		_viewportFrame.TrackedFilenameSource = () => TrackedFilename;
		// Plumb mirror + selection so the Zoom HUD's Fit / Sel can resolve them on demand (grd-vngf).
		_viewportFrame.Mirror = _mirror;
		_viewportFrame.Selection = _selection;
		Canvas = new DesignerCanvas( _viewportFrame );
		Canvas.CanvasClicked += _controller.OnCanvasClicked;
		Canvas.CanvasPanDragged += d => _viewportFrame.Pan( d );
		Canvas.RecordDropped += _controller.OnCanvasRecordDropped;
		Canvas.TemplateDropped += _controller.OnCanvasTemplateDropped;
		Canvas.Overlay = _overlay;
		Canvas.CanvasMoved += _controller.OnCanvasMoved;
		Canvas.CanvasHoverEnded += _controller.OnCanvasHoverEnded;
		Canvas.CanvasReleased += _controller.OnCanvasReleased;
		_viewportFrame.Canvas = Canvas;
		_viewportFrame.Viewport = _viewport;
		_viewportFrame.PreviewTarget = _previewTarget;
		_viewportFrame.SetGrid( new CanvasViewportFrame.GridSettings( _gridShow, _gridSnap, _gridStep ) );
		PushArtboardFillToScene();
		_canvasHost.Add( _viewportFrame );

		_controller.RepopulateMirrorAndRefresh();
	}

	private static bool _stylesheetParseProbeRan;
	private static void RunStylesheetParseProbe()
	{
		if ( _stylesheetParseProbeRan ) return;
		_stylesheetParseProbeRan = true;
		try
		{
			var p = new Panel();
			p.StyleSheet.Parse( ".x { width: 100px; }" );
			p.StyleSheet.Parse( ".x { width: 200px; }" );
			var sheets = System.Linq.Enumerable.ToList( p.AllStyleSheets );
			Log.Info( $"[stylesheet-parse-probe] sheets={sheets.Count} (expect 1 if fixed, 2 if leak)" );
			foreach ( var s in sheets )
				Log.Info( $"[stylesheet-parse-probe]   FileName=\"{s.FileName ?? "<null>"}\"" );
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"[stylesheet-parse-probe] failed: {ex.Message}" );
		}
	}

	private void ApplyPreviewStylesheet()
	{
		RunStylesheetParseProbe();

		var canvas = Canvas;
		if ( canvas is null ) return;
		var root = canvas.DesignerScene?.Root;
		if ( root is null || !root.IsValid ) return;

		var css = DocumentSerializer.GeneratePreviewStylesheet( _document, _theme );
		if ( css == _lastAppliedCss )
		{
			Log.Info( $"{LogPrefix} ApplyPreviewStylesheet ({css.Length} chars): unchanged, skipped" );
			return;
		}
		Log.Info( $"{LogPrefix} ApplyPreviewStylesheet ({css.Length} chars)" );

		if ( _previewSheet is not null )
		{
			root.StyleSheet.Remove( _previewSheet );
		}
		try
		{
			_previewSheet = StyleSheet.FromString( css, "preview" );
		}
		catch ( System.Exception ex )
		{
			Log.Warning( ex, $"{LogPrefix} StyleSheet.FromString failed for theme \"{_theme.Name}\" ({_theme.Source}): {ex.Message}" );
			Log.Warning( $"{LogPrefix} CSS first 400 chars: {css.Substring( 0, System.Math.Min( 400, css.Length ) )}" );
			if ( _previewSheet is not null )
				root.StyleSheet.Add( _previewSheet );
			return;
		}
		root.StyleSheet.Add( _previewSheet );

		ForceFullRestyle( root );

		_lastAppliedCss = css;
	}

	public void PinPreviewState( ControlRecord target, PseudoKind? state )
	{
		if ( target is null )
		{
			Log.Info( $"{LogPrefix} PinPreviewState: target=null, no-op" );
			return;
		}

		// Always clear all three before setting the chosen one — the picker is single-select.
		_mirror.SetPseudoClassForRecord( target, PseudoClass.Hover,  false );
		_mirror.SetPseudoClassForRecord( target, PseudoClass.Active, false );
		_mirror.SetPseudoClassForRecord( target, PseudoClass.Focus,  false );

		if ( state == PseudoKind.Hover )  _mirror.SetPseudoClassForRecord( target, PseudoClass.Hover,  true );
		if ( state == PseudoKind.Active ) _mirror.SetPseudoClassForRecord( target, PseudoClass.Active, true );
		if ( state == PseudoKind.Focus )  _mirror.SetPseudoClassForRecord( target, PseudoClass.Focus,  true );

		_overlay?.SetPreviewActive( state.HasValue );

		Log.Info( $"{LogPrefix} PinPreviewState: target={target.ClassName} state={state}" );
	}

	private void OpenStateStylingForTarget( ControlRecord target )
	{
		if ( _stateStyling is null )
		{
			_stateStyling = new StateStylingWindow(
				_document,
				_selection,
				onValueChanged: () => { /* wired in Phase F */ ApplyPreviewStylesheet(); _inspector?.NotifyValueChanged(); },
				onPinChanged: PinPreviewState );
			_stateStyling.Closed += () => { _stateStyling = null; };
			Log.Info( $"{LogPrefix} StateStylingWindow created" );
		}

		_stateStyling.Show();
		Log.Info( $"{LogPrefix} StateStylingWindow activated for target={target?.ClassName}" );
	}

	// Resolved once: Sandbox.UI.Panel.DirtyStylesRecursive() — internal, no public equivalent.
	private System.Reflection.MethodInfo _dirtyStylesRecursive;
	private bool _dirtyStylesRecursiveResolved;

	private void ForceFullRestyle( Panel root )
	{
		if ( root is null || !root.IsValid ) return;

		if ( !_dirtyStylesRecursiveResolved )
		{
			_dirtyStylesRecursiveResolved = true;
			for ( var t = root.GetType(); t is not null && _dirtyStylesRecursive is null; t = t.BaseType )
			{
				_dirtyStylesRecursive = t.GetMethod( "DirtyStylesRecursive",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
					binder: null, types: System.Type.EmptyTypes, modifiers: null );
			}
			Log.Info( $"{LogPrefix} ForceFullRestyle: DirtyStylesRecursive resolved={_dirtyStylesRecursive is not null}" );
		}

		if ( _dirtyStylesRecursive is not null )
		{
			try { _dirtyStylesRecursive.Invoke( root, null ); }
			catch ( System.Exception ex ) { Log.Warning( $"{LogPrefix} ForceFullRestyle: DirtyStylesRecursive threw: {ex.GetType().Name}: {ex.Message}" ); }
			return;
		}

		root.AddClass( "__rd_force_restyle__" );
		root.RemoveClass( "__rd_force_restyle__" );
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );
		if ( e.Key == KeyCode.Delete )
		{
			var multi = _hierarchy?.SelectedRecords;
			if ( multi is { Count: > 0 } )
			{
				Log.Info( $"{LogPrefix} OnKeyPress Delete: {multi.Count} record(s) from hierarchy" );
				foreach ( var r in multi )
				{
					if ( r is null || r == _document.RootRecord ) continue;
					_controller.DeleteRecordCascade( r );
				}
				e.Accepted = true;
			}
			else if ( _selection?.Selected is not null )
			{
				_controller.DeleteRecordCascade( _selection.Selected );
				e.Accepted = true;
			}
		}

		// Arrow keys: nudge/resize the canvas selection. Alt = resize, otherwise move; Shift = ×10 step.
		{
			int sx = 0, sy = 0;
			switch ( e.Key )
			{
				case KeyCode.Left:  sx = -1; break;
				case KeyCode.Right: sx = +1; break;
				case KeyCode.Up:    sy = -1; break;
				case KeyCode.Down:  sy = +1; break;
			}
			if ( sx != 0 || sy != 0 )
			{
				bool shift = e.HasShift;
				bool alt   = e.HasAlt;
				float step = shift ? 10f : 1f;
				// NudgeSelected params are (dx, dy, dw, dh): sx (horizontal) → dw on resize / dx on move; sy (vertical) → dh / dy.
				bool handled = alt
					? _controller.NudgeSelected( 0f, 0f, sx * step, sy * step )
					: _controller.NudgeSelected( sx * step, sy * step, 0f, 0f );
				if ( handled ) e.Accepted = true;
			}
		}
	}

	private void UpdateFileLabel()
	{
		if ( _fileLabel is null ) return;
		var name = string.IsNullOrEmpty( _trackedPath )
			? "Untitled"
			: System.IO.Path.GetFileName( _trackedPath );
		_fileLabel.Text = name;
		_fileLabel.ToolTip = string.IsNullOrEmpty( _trackedPath ) ? null : _trackedPath;
	}

	private void OnNew()
	{
		Log.Info( $"{LogPrefix} New" );
		_selection?.Deselect();
		_inspector?.SetTarget( null );
		_document.Clear();
		_trackedPath = null;
		ApplyPreviewStylesheet();
		_controller.RefreshHierarchy();
		UpdateFileLabel();
	}

	private void OnOpen()
	{
		var dialog = new FileDialog( null )
		{
			Title = "Open Razor Designer file",
			DefaultSuffix = ".razor",
		};
		dialog.SetFindFile();
		dialog.SetModeOpen();
		dialog.SetNameFilter( "Razor Designer (*.razor *.razor.json)" );

		if ( !dialog.Execute() )
		{
			Log.Info( $"{LogPrefix} Open cancelled" );
			return;
		}

		var result = DocumentIO.Load( dialog.SelectedFile );

		if ( result.DriftDetected )
		{
			Log.Info( $"{LogPrefix} Open: drift detected — showing 3-button modal" );
			new DriftDialog( this ).Show(
				result.LegacyRazorPath,
				result.ResolvedPath,
				choice =>
				{
					switch ( choice )
					{
						case DriftDialog.Choice.ReImport:
							Log.Info( $"{LogPrefix} Open drift: Re-import chosen" );
							var (impDoc, impDiags) = LegacyRazorImporter.Import( result.LegacyRazorPath );
							if ( impDoc == null )
							{
								var errMsg = impDiags.Count > 0 ? impDiags[impDiags.Count - 1].Message : "unknown";
								Log.Warning( $"{LogPrefix} Open drift: re-import failed — {errMsg}" );
								return;
							}
							ApplyOpenResult( impDoc, result.LegacyRazorPath, impDiags );
							break;

						case DriftDialog.Choice.Discard:
							Log.Info( $"{LogPrefix} Open drift: Discard chosen — using .razor.json doc" );
							ApplyOpenResult( result.Document, result.ResolvedPath,
								System.Array.Empty<ValidationDiagnostic>() );
							break;

						case DriftDialog.Choice.Cancel:
							Log.Info( $"{LogPrefix} Open drift: Cancel — open aborted, current document unchanged" );
							break;
					}
				} );
			return;
		}

		if ( !result.Success )
		{
			Log.Warning( $"{LogPrefix} Open failed" );
			foreach ( var w in result.Warnings )
				Log.Warning( $"{LogPrefix} Open error: {w}" );
			return;
		}

		// Stamp mismatch (non-blocking): load proceeds with the JSON doc, banner surfaced in inspector.
		if ( result.StampMismatch )
		{
			Log.Info( $"{LogPrefix} Open: stamp-mismatch banner — .razor does not match IR" );
			var mismatchDiag = new ValidationDiagnostic(
				NodeId:   null,
				Severity: DiagnosticSeverity.Warn,
				Code:     "stamp-mismatch",
				Message:  ".razor file does not match IR. Save to refresh the .razor, or use 'Re-import from .razor' to lift hand-edits." );
			_stampMismatchDiags = new[] { mismatchDiag };
		}
		else
		{
			_stampMismatchDiags = null;
		}

		ApplyOpenResult( result.Document, result.ResolvedPath, System.Array.Empty<ValidationDiagnostic>() );

		// After the document is applied, surface stamp-mismatch banner if present.
		if ( _stampMismatchDiags is not null )
		{
			_inspector?.SetDiagnostics( _stampMismatchDiags );
			Log.Warning( $"{LogPrefix} Open: stamp-mismatch banner active. Ctrl+S to refresh .razor, or use Re-import button." );
		}

		// Surface migrated-from-legacy diagnostic in inspector.
		if ( result.MigratedFromLegacy )
		{
			Log.Info( $"{LogPrefix} Open: migrated from legacy .razor — surfacing diagnostic" );
			var migDiag = new ValidationDiagnostic(
				NodeId:   null,
				Severity: DiagnosticSeverity.Warn,
				Code:     "legacy-migrated",
				Message:  $"Migrated from legacy .razor. A new .razor.json has been created. Save to update the .razor with the IR hash stamp." );
			_inspector?.SetDiagnostics( new[] { migDiag } );
		}

		Log.Info( $"{LogPrefix} Open: loaded {_document.RootRecord.Children.Count} root child(ren); " +
		          $"{result.Warnings.Count} warning(s); _trackedPath -> {_trackedPath}" );
		foreach ( var w in result.Warnings )
			Log.Info( $"{LogPrefix} Open warning: {w}" );
	}

	private void ApplyOpenResult(
		DesignerDocument doc,
		string resolvedPath,
		IReadOnlyList<ValidationDiagnostic> diags )
	{
		if ( doc == null ) return;

		_selection?.Deselect();
		_inspector?.SetTarget( null );
		_document.Clear();
		foreach ( var child in doc.RootRecord.Children )
			_document.RootRecord.Children.Add( child );

		_trackedPath = resolvedPath;
		if ( _trackedPath is not null &&
		     _trackedPath.EndsWith( ".razor.json", System.StringComparison.OrdinalIgnoreCase ) )
			_trackedPath = _trackedPath[..^5];   // strip ".json" → "…/Foo.razor"

		_controller.RepopulateMirrorAndRefresh();

		// Surface any diagnostics from this load (e.g. re-import warnings).
		if ( diags is { Count: > 0 } )
			_inspector?.SetDiagnostics( diags );

		// Clear a prior stamp-mismatch banner on clean loads.
		if ( _stampMismatchDiags is not null && diags is { Count: 0 } )
		{
			_stampMismatchDiags = null;
		}

		Log.Info( $"{LogPrefix} ApplyOpenResult: doc applied ({_document.RootRecord.Children.Count} root children); _trackedPath={_trackedPath}" );
		UpdateFileLabel();
	}

	private void OnCodeButton()
	{
		var current = _document?.Wiring ?? Grains.RazorDesigner.Wiring.WiringEnvelope.Empty;
		Log.Info( $"{LogPrefix} OnCodeButton: opening dialog with {current.Symbols.Count} symbol(s)" );
		new Grains.RazorDesigner.CodeDialog.CodeDialog( this, current ).Show(
			onConfirm: env => _controller.ApplyWiringChange( env ),
			onCancel:  () => Log.Info( $"{LogPrefix} OnCodeButton: cancelled" ) );
	}

	private void OnReimportFromRazor()
	{
		// Compute the .razor stem from the tracked path (which might be the .razor.json path).
		var stem = _trackedPath;
		if ( stem is null )
		{
			Log.Warning( $"{LogPrefix} Re-import: no tracked path" );
			return;
		}
		if ( stem.EndsWith( ".razor.json", System.StringComparison.OrdinalIgnoreCase ) )
			stem = stem[..^5];

		if ( !System.IO.File.Exists( stem ) )
		{
			Log.Warning( $"{LogPrefix} Re-import: .razor file not found: {stem}" );
			return;
		}

		Log.Info( $"{LogPrefix} Re-import: running LegacyRazorImporter on {stem}" );
		var (impDoc, impDiags) = LegacyRazorImporter.Import( stem );

		if ( impDoc == null )
		{
			// Import failed — show an error in the inspector.
			var errMsg = impDiags.Count > 0 ? impDiags[impDiags.Count - 1].Message : "unknown error";
			Log.Warning( $"{LogPrefix} Re-import: failed — {errMsg}" );
			_inspector?.SetDiagnostics( impDiags );
			return;
		}

		// Compute loss between the imported doc and the current IR document.
		var loss = LegacyRazorImporter.DescribeLoss( impDoc, _document );
		Log.Info( $"{LogPrefix} Re-import: DescribeLoss returned {loss.Count} line(s)" );

		if ( loss.Count > 0 )
		{
			// Show the loss-preview modal.
			new LossPreviewDialog( this ).Show(
				loss,
				stem,
				onConfirm: () =>
				{
					Log.Info( $"{LogPrefix} Re-import: loss-preview confirmed — grafting imported doc" );
					ApplyOpenResult( impDoc, _trackedPath, impDiags );
				},
				onCancel: () =>
				{
					Log.Info( $"{LogPrefix} Re-import: loss-preview cancelled" );
				} );
		}
		else
		{
			// No detectable loss — apply directly.
			Log.Info( $"{LogPrefix} Re-import: no loss detected — applying directly" );
			ApplyOpenResult( impDoc, _trackedPath, impDiags );
		}
	}

	private void OnSave()
	{
		if ( string.IsNullOrEmpty( _trackedPath ) )
		{
			var dialog = new FileDialog( null )
			{
				Title = "Save Razor Designer Output",
				DefaultSuffix = ".razor",
			};
			dialog.SetFindFile();
			dialog.SetModeSave();
			dialog.SetNameFilter( "Razor (*.razor)" );
			// SelectedFile is read-only; SelectFile prefills the name field.
			dialog.SelectFile( "MyMenu.razor" );

			if ( !dialog.Execute() )
			{
				Log.Info( $"{LogPrefix} Save cancelled" );
				return;
			}

			_trackedPath = dialog.SelectedFile;
			if ( !_trackedPath.EndsWith( ".razor", System.StringComparison.OrdinalIgnoreCase ) )
				_trackedPath += ".razor";
			UpdateFileLabel();
		}

		var result = DocumentIO.Save( _document, _trackedPath, _theme );
		ApplySaveResult( result );
	}

	private void OnDuplicate()
	{
		var multi = _hierarchy?.SelectedRecords;
		if ( multi is { Count: > 0 } )
		{
			Log.Info( $"{LogPrefix} Shortcut Duplicate: {multi.Count} record(s)" );
			_controller.DuplicateRecords( multi ); 
		}
		else if ( _selection?.Selected is not null )
		{
			Log.Info( $"{LogPrefix} Shortcut Duplicate: 1 record" );
			_controller.DuplicateRecords( new[] { _selection.Selected } );
		}
	}

	private void OnCopy()
	{
		var multi = _hierarchy?.SelectedRecords;
		if ( multi is { Count: > 0 } )
		{
			Log.Info( $"{LogPrefix} Shortcut Copy: {multi.Count} record(s)" );
			_controller.OnHierarchyCopyRequested( multi ); 
		}
		else if ( _selection?.Selected is not null )
		{
			Log.Info( $"{LogPrefix} Shortcut Copy: 1 record" );
			_controller.OnHierarchyCopyRequested( new[] { _selection.Selected } );
		}
	}

	private void OnPaste()
	{
		var target = _selection?.Selected ?? _document?.RootRecord;
		
		if ( target is not null )
		{
			Log.Info( $"{LogPrefix} Shortcut Paste: onto {target.ClassName}" );
			_controller.OnHierarchyPasteRequested( target );
		}
	}

	private void ApplySaveResult( SaveResult result )
	{
		if ( !result.RazorExported )
		{
			var errorCount = result.Diagnostics?.Count( d => d.Severity == DiagnosticSeverity.Error ) ?? 0;
			_razorExportFailedSuffix = $" ⚠ razor export failed ({errorCount} error(s))";
			// Title is the dock window title. Widget.WindowTitle sets the caption on the dock.
			WindowTitle = $"Razor Designer{_razorExportFailedSuffix}";
			Log.Warning( $"{LogPrefix} Save: .razor not exported — {errorCount} error(s). See inspector for details. .razor.json was saved." );
		}
		else
		{
			_razorExportFailedSuffix = null;
			WindowTitle = "Razor Designer";
			Log.Info( $"{LogPrefix} Save: .razor exported successfully." );
		}
	}
}
