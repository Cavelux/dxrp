using System;
using System.Linq;
using System.Reflection;
using Sandbox;
using Sandbox.UI;

namespace Grains.RazorDesigner.Canvas;

public sealed class DesignerScene : IDisposable
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public static readonly Color ArtboardTintColor   = new( 0.137f, 0.165f, 0.200f ); // ~#232a33
	public static readonly Color WorkspaceClearColor = new( 0.055f, 0.067f, 0.078f ); // ~#0e1114
	public static readonly Color HalvesLineColor    = new( 1f, 1f, 1f, 0.22f );      // halves major-line tint

	// Chrome label tints (workspace ScreenPanel runs at Scale=dpiScale so font sizes etc. are widget-px).
	public static readonly Color ChromeMutedColor   = new( 0.541f, 0.573f, 0.643f );      // #8a92a4
	public static readonly Color ChromeBrightColor  = new( 0.812f, 0.835f, 0.886f );      // #cfd5e2
	public static readonly Color ChromePadBgColor   = new( 0.055f, 0.067f, 0.078f, 0.95f );

	public Scene Scene { get; }
	public CameraComponent Camera { get; }
	public ScreenPanel ScreenPanel { get; }
	public Panel Root => ScreenPanel.GetPanel();

	public ScreenPanel WorkspaceScreenPanel { get; }
	public Panel WorkspaceRoot => WorkspaceScreenPanel.GetPanel();

	// Workspace ScreenPanel was added in the pivot — needs its own isolation + reflection plumbing.
	private bool _workspaceLayoutIsolated;
	private bool _workspaceRootLogged;

	public System.Func<string> FilenameSource { get; set; }

	private Panel _backdrop;
	private bool _backdropProbeLogged;

	private Panel _halfVertical;
	private Panel _halfHorizontal;
	private bool _halvesProbeLogged;

	private Panel _grid;
	private float _gridLastStepCss = -1f;
	private Vector2? _gridLastViewport;
	private bool _gridProbeLogged;
	private const float MinGridStepWidgetPx = 3f; // grid hides below this widget-px step (density clamp)
	private static readonly Color GridLineColor = new( 1f, 1f, 1f, 0.045f );

	public bool GridShow { get; set; }
	public float GridStepCss { get; set; } = 8f;

	public ArtboardFillMode ArtboardFill { get; set; } = ArtboardFillMode.Dark;
	public Color ArtboardCustomColor { get; set; } = new( 0.137f, 0.165f, 0.200f );

	private Panel _workspaceFill;
	private Sandbox.UI.Label _filenameHeader;
	private Sandbox.UI.Label _widthLabel;
	private Panel _widthLabelPad;
	private Sandbox.UI.Label _heightLabel;
	private Panel _heightLabelPad;
	private bool _workspaceChromeProbeLogged;

	public Vector2? ViewportLogical { get; set; }

	public PreviewTargetMode PreviewTarget { get; set; } = PreviewTargetMode.None;

	public float Zoom { get; set; } = 1f;
	public Vector2 PanOffsetWidgetPx { get; set; } = Vector2.Zero;

	public float CurrentScale { get; private set; } = 1f;
	public Rect RootBoundsFb { get; private set; }

	private bool _rootLogged;
	private bool _layoutIsolated;
	private bool _reflectionLogged;

	private MethodInfo _rootLayoutMethod;
	private MethodInfo _rootBuildDescriptorsMethod;
	private PropertyInfo _rootScaleProperty;
	private PropertyInfo _rootPanelBoundsProperty;

	public DesignerScene()
	{
		Log.Info( $"{LogPrefix} DesignerScene ctor (CreateEditorScene + EditorTick + two ScreenPanels)" );

		Scene = Scene.CreateEditorScene();
		Scene.Name = "Razor Designer";

		using ( Scene.Push() )
		{
			var cameraGo = new GameObject( true, "camera" );
			Camera = cameraGo.AddComponent<CameraComponent>();
			Camera.BackgroundColor = WorkspaceClearColor;
			Camera.IsMainCamera = false;

			// Order matters: register Workspace first so it renders BEHIND the artboard.
			var workspaceGo = new GameObject( true, "workspace-ui" );
			WorkspaceScreenPanel = workspaceGo.AddComponent<ScreenPanel>();
			WorkspaceScreenPanel.TargetCamera = Camera;
			ForceLifecycle( WorkspaceScreenPanel );

			var uiGo = new GameObject( true, "artboard-ui" );
			ScreenPanel = uiGo.AddComponent<ScreenPanel>();
			ScreenPanel.TargetCamera = Camera;
			ForceLifecycle( ScreenPanel );
		}
	}

	private static void ForceLifecycle( Component component )
	{
		var type = component.GetType();
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

		var awake = type.GetMethod( "OnAwake", flags );
		var enabled = type.GetMethod( "OnEnabled", flags );

		try
		{
			awake?.Invoke( component, null );
			enabled?.Invoke( component, null );
			Log.Info( $"{LogPrefix} ForceLifecycle({type.Name}) ok awake={awake != null} enabled={enabled != null}" );
		}
		catch ( System.Exception ex )
		{
			Log.Error( $"{LogPrefix} ForceLifecycle({type.Name}) threw: {ex.GetType().Name}: {ex.Message}" );
			throw;
		}
	}

	// Call BEFORE Scene.Destroy(); component needs a valid scene to tear down.
	private static void ForceTeardown( Component component )
	{
		if ( component is null ) return;

		var type = component.GetType();
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

		var disabled = type.GetMethod( "OnDisabled", flags );
		var destroy = type.GetMethod( "OnDestroy", flags );

		try
		{
			disabled?.Invoke( component, null );
			destroy?.Invoke( component, null );
			Log.Info( $"{LogPrefix} ForceTeardown({type.Name}) ok disabled={disabled != null} destroy={destroy != null}" );
		}
		catch ( System.Exception ex )
		{
			Log.Error( $"{LogPrefix} ForceTeardown({type.Name}) threw: {ex.GetType().Name}: {ex.Message}" );
		}
	}

	private static bool TryIsolateRootFromMenuPump( Panel root )
	{
		if ( root is null ) return false;

		try
		{
			var globalContextType = AppDomain.CurrentDomain.GetAssemblies()
				.Select( a => SafeGetType( a, "Sandbox.Engine.GlobalContext" ) )
				.FirstOrDefault( t => t is not null );

			if ( globalContextType is null )
			{
				Log.Warning( $"{LogPrefix} IsolateRoot: GlobalContext type not found" );
				return false;
			}

			var current = globalContextType
				.GetProperty( "Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static )
				?.GetValue( null );
			if ( current is null )
			{
				Log.Warning( $"{LogPrefix} IsolateRoot: GlobalContext.Current is null" );
				return false;
			}

			var uiSystem = current.GetType()
				.GetProperty( "UISystem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance )
				?.GetValue( current );
			if ( uiSystem is null )
			{
				Log.Warning( $"{LogPrefix} IsolateRoot: GlobalContext.UISystem is null" );
				return false;
			}

			var removeRoot = uiSystem.GetType().GetMethod(
				"RemoveRoot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
			if ( removeRoot is null )
			{
				Log.Warning( $"{LogPrefix} IsolateRoot: UISystem.RemoveRoot not found" );
				return false;
			}

			removeRoot.Invoke( uiSystem, new object[] { root } );
			Log.Info( $"{LogPrefix} IsolateRoot: removed from UISystem.RootPanels (engine layout pump will skip it)" );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"{LogPrefix} IsolateRoot threw: {ex.GetType().Name}: {ex.Message}" );
			return false;
		}
	}

	private static Type SafeGetType( Assembly a, string fullName )
	{
		try { return a.GetType( fullName, throwOnError: false ); }
		catch { return null; }
	}

	private void DriveLayout( Panel root, float widthPx, float heightPx, float dpiScale )
	{
		if ( widthPx < 1f || heightPx < 1f ) return;
		if ( dpiScale < 0.01f ) dpiScale = 1.0f;

		var pinned = PreviewTarget.IsPinned() ? PreviewTarget.PinnedScale() : 0f;
		var (bounds, scale, _) = ComputeView( new Vector2( widthPx, heightPx ), dpiScale, ViewportLogical, pinned, Zoom, PanOffsetWidgetPx );

		CurrentScale = scale;
		RootBoundsFb = bounds;

		var rootType = root.GetType();
		EnsureReflectionResolved( rootType );

		_rootPanelBoundsProperty?.SetValue( root, bounds );
		_rootScaleProperty?.SetValue( root, scale );

		try
		{
			_rootLayoutMethod?.Invoke( root, null );
			_rootBuildDescriptorsMethod?.Invoke( root, new object[] { 1.0f } );
		}
		catch ( Exception ex )
		{
			Log.Error( $"{LogPrefix} DriveLayout threw: {ex.GetType().Name}: {ex.Message}" );
		}
	}

	public static (Rect Bounds, float Scale, Vector2 ClampedPanWidgetPx) ComputeView(
		Vector2 widgetSize, float dpiScale, Vector2? viewportLogical, float pinnedScale, float zoom, Vector2 panOffsetWidgetPx )
	{
		if ( dpiScale < 0.01f ) dpiScale = 1f;
		var fbW = widgetSize.x * dpiScale;
		var fbH = widgetSize.y * dpiScale;
		if ( fbW < 1f || fbH < 1f ) return ( new Rect( 0, 0, MathF.Max( fbW, 1f ), MathF.Max( fbH, 1f ) ), dpiScale, Vector2.Zero );

		if ( !(viewportLogical is { } vp && vp.x >= 1f && vp.y >= 1f) )
			return ( new Rect( 0, 0, fbW, fbH ), dpiScale, Vector2.Zero );

		var baseScale = pinnedScale > 0.001f
			? pinnedScale                              // grd-z71: 1 logical px == pinnedScale framebuffer px
			: MathF.Min( fbW / vp.x, fbH / vp.y );     // scale-to-fit on the smaller axis
		if ( zoom < 0.001f ) zoom = 1f;
		var scale = baseScale * zoom;
		if ( scale < 0.001f ) scale = dpiScale;

		var panelW = vp.x * scale;
		var panelH = vp.y * scale;
		var panFb = panOffsetWidgetPx * dpiScale;

		var centeredX = (fbW - panelW) * 0.5f;
		var centeredY = (fbH - panelH) * 0.5f;
		var keepX = MathF.Min( 80f * dpiScale, MathF.Min( panelW, fbW ) );
		var keepY = MathF.Min( 80f * dpiScale, MathF.Min( panelH, fbH ) );
		var x = Math.Clamp( centeredX + panFb.x, keepX - panelW, fbW - keepX );
		var y = Math.Clamp( centeredY + panFb.y, keepY - panelH, fbH - keepY );

		var bounds = new Rect( MathF.Floor( x ), MathF.Floor( y ), MathF.Floor( panelW ), MathF.Floor( panelH ) );
		return ( bounds, scale, new Vector2( x - centeredX, y - centeredY ) / dpiScale );
	}

	private void EnsureReflectionResolved( Type rootType )
	{
		_rootLayoutMethod ??= ResolveMethod( rootType, "Layout", Type.EmptyTypes );
		_rootBuildDescriptorsMethod ??= ResolveMethod( rootType, "BuildDescriptors", new[] { typeof( float ) } );
		_rootScaleProperty ??= ResolveProperty( rootType, "Scale", typeof( float ) );
		_rootPanelBoundsProperty ??= ResolveProperty( rootType, "PanelBounds", typeof( Rect ) );

		if ( !_reflectionLogged )
		{
			Log.Info( $"{LogPrefix} DriveLayout reflection resolved: " +
				$"Layout={_rootLayoutMethod is not null} " +
				$"BuildDescriptors={_rootBuildDescriptorsMethod is not null} " +
				$"Scale={_rootScaleProperty is not null} " +
				$"PanelBounds={_rootPanelBoundsProperty is not null}" );
			_reflectionLogged = true;
		}
	}

	// Walk up: methods we want are on RootPanel base, not concrete GameRootPanel.
	private static MethodInfo ResolveMethod( Type startType, string name, Type[] paramTypes )
	{
		for ( var t = startType; t is not null; t = t.BaseType )
		{
			var method = t.GetMethod( name,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				binder: null,
				types: paramTypes,
				modifiers: null );
			if ( method is not null ) return method;
		}
		return null;
	}

	private static PropertyInfo ResolveProperty( Type startType, string name, Type expectedType )
	{
		for ( var t = startType; t is not null; t = t.BaseType )
		{
			var prop = t.GetProperty( name,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
			if ( prop is not null && prop.PropertyType == expectedType )
				return prop;
		}
		return null;
	}

	public void Update( float widthPx, float heightPx, float dpiScale )
	{
		var root = Root;
		var workspaceRoot = WorkspaceRoot;
		if ( !root.IsValid() ) return;

		if ( !_layoutIsolated )
		{
			_layoutIsolated = TryIsolateRootFromMenuPump( root );
		}

		if ( workspaceRoot.IsValid() && !_workspaceLayoutIsolated )
		{
			_workspaceLayoutIsolated = TryIsolateRootFromMenuPump( workspaceRoot );
		}

		if ( !_rootLogged )
		{
			Log.Info( $"{LogPrefix} root valid (width={widthPx} height={heightPx} dpiScale={dpiScale})" );
			_rootLogged = true;
		}

		if ( workspaceRoot.IsValid() && !_workspaceRootLogged )
		{
			Log.Info( $"{LogPrefix} workspace root valid (width={widthPx} height={heightPx} dpiScale={dpiScale})" );
			_workspaceRootLogged = true;
		}

		EnsureBackdrop( root );
		EnsureHalves( root );
		if ( workspaceRoot.IsValid() ) EnsureWorkspaceChrome( workspaceRoot );

		DriveLayout( root, widthPx, heightPx, dpiScale );
		if ( workspaceRoot.IsValid() ) DriveWorkspaceLayout( workspaceRoot, widthPx, heightPx, dpiScale );

		EnsureGrid( root, dpiScale );

		// Position chrome AFTER DriveLayout so RootBoundsFb is current for this frame.
		UpdateWorkspaceChromePositions( widthPx, heightPx, dpiScale );
	}

	private void EnsureBackdrop( Panel root )
	{
		if ( _backdrop is null || !_backdrop.IsValid || _backdrop.Parent != root )
		{
			_backdrop = new Panel( root );
			_appliedFill = (ArtboardFillMode)(-1);
			_backdrop.AddClass( "designer-backdrop" );
			_backdrop.Style.Position = PositionMode.Absolute;
			_backdrop.Style.Left = Length.Pixels( 0 );
			_backdrop.Style.Top = Length.Pixels( 0 );
			_backdrop.Style.Width = Length.Percent( 100 );
			_backdrop.Style.Height = Length.Percent( 100 );
			_backdrop.Style.ZIndex = -1000;
			_backdrop.Style.PointerEvents = PointerEvents.None;

			try { root.SetChildIndex( _backdrop, 0 ); }
			catch ( Exception ex ) { Log.Warning( $"{LogPrefix} EnsureBackdrop SetChildIndex threw: {ex.GetType().Name}: {ex.Message}" ); }

			if ( !_backdropProbeLogged )
			{
				Log.Info( $"{LogPrefix} EnsureBackdrop: backdrop installed (fill={ArtboardFill.DisplayLabel()}, box-shadow DISABLED for triage, target=Root, zIndex=-1000)" );
				_backdropProbeLogged = true;
			}
		}

		ApplyArtboardFill( _backdrop );
	}

	private ArtboardFillMode _appliedFill = (ArtboardFillMode)(-1);
	private Color _appliedCustom = Color.Black;
	private void ApplyArtboardFill( Panel backdrop )
	{
		var mode = ArtboardFill;
		var custom = ArtboardCustomColor;
		if ( mode == _appliedFill && (mode != ArtboardFillMode.Custom || custom == _appliedCustom) )
			return;

		_appliedFill = mode;
		_appliedCustom = custom;

		if ( mode == ArtboardFillMode.Checker )
		{
			backdrop.Style.BackgroundColor = Color.White;
			backdrop.Style.BackgroundImage = GetOrCreateCheckerTexture();
		}
		else
		{
			// Strip the texture if we're switching out of Checker, then apply the flat color.
			backdrop.Style.BackgroundImage = null;
			var color = mode == ArtboardFillMode.Custom ? custom : mode.PresetColor();
			backdrop.Style.BackgroundColor = color;
		}

		Log.Info( $"{LogPrefix} ArtboardFill applied: {mode.DisplayLabel()}{(mode == ArtboardFillMode.Custom ? $" ({custom.Hex})" : "")}" );
	}

	private static Texture _checkerTexture;
	private static Texture GetOrCreateCheckerTexture()
	{
		if ( _checkerTexture is not null && _checkerTexture.IsLoaded ) return _checkerTexture;

		const int size = 32;
		const int half = size / 2;
		// Two-tone cool gray. Same dark family as the Dark preset, just split into two tones.
		var darkRgb  = new byte[] { 42, 45, 51 };   // ~#2a2d33
		var lightRgb = new byte[] { 54, 58, 66 };   // ~#363a42

		var data = new byte[size * size * 4];
		for ( int y = 0; y < size; y++ )
		for ( int x = 0; x < size; x++ )
		{
			var quadrant = (x < half) ^ (y < half);
			var rgb = quadrant ? lightRgb : darkRgb;
			int i = (y * size + x) * 4;
			data[i + 0] = rgb[0];
			data[i + 1] = rgb[1];
			data[i + 2] = rgb[2];
			data[i + 3] = 255;
		}

		_checkerTexture = Texture.Create( size, size, ImageFormat.RGBA8888 )
			.WithName( "designer-artboard-checker" )
			.WithData( data )
			.Finish();

		Log.Info( $"[Grains.RazorDesigner] GetOrCreateCheckerTexture: built {size}x{size} two-tone tile" );
		return _checkerTexture;
	}

	private void EnsureHalves( Panel root )
	{
		EnsureHalf( ref _halfVertical, root, "designer-halves-v", isVertical: true );
		EnsureHalf( ref _halfHorizontal, root, "designer-halves-h", isVertical: false );

		var visible = GridShow;
		var display = visible ? DisplayMode.Flex : DisplayMode.None;
		if ( _halfVertical is not null && _halfVertical.IsValid ) _halfVertical.Style.Display = display;
		if ( _halfHorizontal is not null && _halfHorizontal.IsValid ) _halfHorizontal.Style.Display = display;

		if ( !_halvesProbeLogged )
		{
			Log.Info( $"{LogPrefix} EnsureHalves: halves lines installed (zIndex=-999, alpha=0.22, visible={visible})" );
			_halvesProbeLogged = true;
		}
	}

	private void EnsureHalf( ref Panel slot, Panel root, string className, bool isVertical )
	{
		if ( slot is not null && slot.IsValid && slot.Parent == root )
			return;

		slot = new Panel( root );
		slot.AddClass( className );
		var s = slot.Style;
		s.Position = PositionMode.Absolute;
		s.BackgroundColor = HalvesLineColor;
		s.ZIndex = -999;
		s.PointerEvents = PointerEvents.None;

		if ( isVertical )
		{
			s.Left = Length.Percent( 50 );
			s.Top = Length.Pixels( 0 );
			s.Width = Length.Pixels( 1 );
			s.Height = Length.Percent( 100 );
		}
		else
		{
			s.Left = Length.Pixels( 0 );
			s.Top = Length.Percent( 50 );
			s.Width = Length.Percent( 100 );
			s.Height = Length.Pixels( 1 );
		}
	}

	private void EnsureGrid( Panel root, float dpiScale )
	{
		if ( dpiScale < 0.01f ) dpiScale = 1f;

		// Density clamp: stepWidget = StepCss * (fb-per-css) / (fb-per-widget) = widget-per-css * StepCss.
		var stepWidget = GridStepCss * CurrentScale / dpiScale;
		var viewport = ViewportLogical;
		var shouldShow = GridShow
			&& GridStepCss > 0.001f
			&& stepWidget >= MinGridStepWidgetPx
			&& viewport is { } vpCheck && vpCheck.x >= 1f && vpCheck.y >= 1f;

		if ( !shouldShow )
		{
			if ( _grid is { IsValid: true } ) _grid.Style.Set( "display", "none" );
			return;
		}

		// Lazy-create container (self-healing — covers Repopulate's DeleteChildren wipe).
		if ( _grid is null || !_grid.IsValid || _grid.Parent != root )
		{
			_grid = new Panel( root );
			_grid.AddClass( "designer-grid" );
			var s = _grid.Style;
			s.Position = PositionMode.Absolute;
			s.Left = Length.Pixels( 0 );
			s.Top = Length.Pixels( 0 );
			s.Width = Length.Percent( 100 );
			s.Height = Length.Percent( 100 );
			s.PointerEvents = PointerEvents.None;
			s.ZIndex = -998; // above halves (-999), below user content (0)
			_gridLastStepCss = -1f;       // force line rebuild on next pass
			_gridLastViewport = null;

			if ( !_gridProbeLogged )
			{
				Log.Info( $"{LogPrefix} EnsureGrid: grid container installed (zIndex=-998, thin-panel lines)" );
				_gridProbeLogged = true;
			}
		}

		// Rebuild line children only when step or viewport changes.
		var vp = viewport.Value;
		if ( MathF.Abs( GridStepCss - _gridLastStepCss ) > 0.01f || _gridLastViewport != vp )
		{
			_grid.DeleteChildren( immediate: true );

			var step = GridStepCss;
			var midX = vp.x * 0.5f;
			var midY = vp.y * 0.5f;
			int linesCreated = 0;

			int halfStepsX = (int)MathF.Floor( midX / step );
			float startX = midX - halfStepsX * step;
			for ( float x = startX; x < vp.x - 0.5f; x += step )
			{
				if ( x < 0.5f ) continue;
				if ( MathF.Abs( x - midX ) < 0.5f ) continue; // skip halves overlap
				var line = new Panel( _grid );
				var ls = line.Style;
				ls.Position = PositionMode.Absolute;
				ls.Left = Length.Pixels( x );
				ls.Top = Length.Pixels( 0 );
				ls.Width = Length.Pixels( 1 );
				ls.Height = Length.Percent( 100 );
				ls.BackgroundColor = GridLineColor;
				ls.PointerEvents = PointerEvents.None;
				linesCreated++;
			}

			int halfStepsY = (int)MathF.Floor( midY / step );
			float startY = midY - halfStepsY * step;
			for ( float y = startY; y < vp.y - 0.5f; y += step )
			{
				if ( y < 0.5f ) continue;
				if ( MathF.Abs( y - midY ) < 0.5f ) continue; // skip halves overlap
				var line = new Panel( _grid );
				var ls = line.Style;
				ls.Position = PositionMode.Absolute;
				ls.Left = Length.Pixels( 0 );
				ls.Top = Length.Pixels( y );
				ls.Width = Length.Percent( 100 );
				ls.Height = Length.Pixels( 1 );
				ls.BackgroundColor = GridLineColor;
				ls.PointerEvents = PointerEvents.None;
				linesCreated++;
			}

			_gridLastStepCss = GridStepCss;
			_gridLastViewport = vp;
			Log.Info( $"{LogPrefix} EnsureGrid: rebuilt {linesCreated} line panels (step={step:F1} CSS, vp={vp.x:F0}x{vp.y:F0}, centered)" );
		}

		_grid.Style.Set( "display", "flex" );
	}

	private void DriveWorkspaceLayout( Panel root, float widthPx, float heightPx, float dpiScale )
	{
		if ( widthPx < 1f || heightPx < 1f ) return;
		if ( dpiScale < 0.01f ) dpiScale = 1.0f;

		var fbW = widthPx * dpiScale;
		var fbH = heightPx * dpiScale;
		var fullFb = new Rect( 0, 0, fbW, fbH );

		var rootType = root.GetType();
		EnsureReflectionResolved( rootType );

		_rootPanelBoundsProperty?.SetValue( root, fullFb );
		_rootScaleProperty?.SetValue( root, dpiScale );

		try
		{
			_rootLayoutMethod?.Invoke( root, null );
			_rootBuildDescriptorsMethod?.Invoke( root, new object[] { 1.0f } );
		}
		catch ( Exception ex )
		{
			Log.Error( $"{LogPrefix} DriveWorkspaceLayout threw: {ex.GetType().Name}: {ex.Message}" );
		}
	}

	private void EnsureWorkspaceChrome( Panel root )
	{
		EnsureWorkspaceFill( root );
		EnsureFilenameHeader( root );
		EnsureWidthLabel( root );
		EnsureHeightLabel( root );

		if ( !_workspaceChromeProbeLogged )
		{
			Log.Info( $"{LogPrefix} EnsureWorkspaceChrome: fill + header + width + height labels installed under WorkspaceRoot" );
			_workspaceChromeProbeLogged = true;
		}
	}

	private void EnsureWorkspaceFill( Panel root )
	{
		if ( _workspaceFill is not null && _workspaceFill.IsValid && _workspaceFill.Parent == root ) return;

		_workspaceFill = new Panel( root );
		_workspaceFill.AddClass( "designer-workspace-fill" );
		var s = _workspaceFill.Style;
		s.Position = PositionMode.Absolute;
		s.Left = Length.Pixels( 0 );
		s.Top = Length.Pixels( 0 );
		s.Width = Length.Percent( 100 );
		s.Height = Length.Percent( 100 );
		s.BackgroundColor = WorkspaceClearColor;
		s.ZIndex = -2000;
		s.PointerEvents = PointerEvents.None;

		s.SetBackgroundImage( GetOrCreateDotTexture() );

		// Put it first in the sibling list so chrome labels paint on top.
		try { root.SetChildIndex( _workspaceFill, 0 ); }
		catch ( Exception ex ) { Log.Warning( $"{LogPrefix} EnsureWorkspaceFill SetChildIndex threw: {ex.GetType().Name}: {ex.Message}" ); }
	}

	private static Texture _dotTexture;
	private static Texture GetOrCreateDotTexture()
	{
		if ( _dotTexture is not null && _dotTexture.IsLoaded ) return _dotTexture;

		const int size = 28;
		const int centre = size / 2;
		var data = new byte[size * size * 4]; // RGBA8888

		for ( int y = 0; y < size; y++ )
		for ( int x = 0; x < size; x++ )
		{
			var dx = x - centre;
			var dy = y - centre;
			var d2 = dx * dx + dy * dy;

			byte alpha = 0;
			if ( d2 == 0 ) alpha = 18;       // ~0.07 — exact centre
			else if ( d2 == 1 ) alpha = 10;  // ~0.04 — 4-neighbours
			else if ( d2 == 2 ) alpha = 4;   // ~0.015 — diagonals

			int i = (y * size + x) * 4;
			data[i + 0] = 255; // R
			data[i + 1] = 255; // G
			data[i + 2] = 255; // B
			data[i + 3] = alpha;
		}

		_dotTexture = Texture.Create( size, size, ImageFormat.RGBA8888 )
			.WithName( "designer-workspace-dot" )
			.WithData( data )
			.Finish();

		Log.Info( $"[Grains.RazorDesigner] GetOrCreateDotTexture: built {size}x{size} dot tile (RGBA8888, {data.Length} bytes)" );
		return _dotTexture;
	}

	private void EnsureFilenameHeader( Panel root )
	{
		if ( _filenameHeader is not null && _filenameHeader.IsValid && _filenameHeader.Parent == root ) return;

		_filenameHeader = root.AddChild<Sandbox.UI.Label>();
		_filenameHeader.AddClass( "designer-filename-header" );
		var s = _filenameHeader.Style;
		s.Position = PositionMode.Absolute;
		s.FontColor = ChromeBrightColor; // Sandbox.UI Label can't easily mix colors in one widget; pick the brighter tint.
		s.FontSize = 11f;
		s.PointerEvents = PointerEvents.None;
		s.Set( "white-space", "nowrap" );
		s.Set( "display", "none" ); // hidden until UpdateWorkspaceChromePositions decides to show
	}

	private void EnsureWidthLabel( Panel root )
	{
		if ( _widthLabelPad is not null && _widthLabelPad.IsValid && _widthLabelPad.Parent == root ) return;

		// Pad panel gives the label a subtle dark background pad (legibility over the dot pattern).
		_widthLabelPad = new Panel( root );
		_widthLabelPad.AddClass( "designer-dim-pad" );
		var ps = _widthLabelPad.Style;
		ps.Position = PositionMode.Absolute;
		ps.BackgroundColor = ChromePadBgColor;
		ps.PointerEvents = PointerEvents.None;
		ps.Set( "padding", "1px 6px" );
		ps.Set( "border-radius", "3px" );
		ps.Set( "display", "none" );

		_widthLabel = _widthLabelPad.AddChild<Sandbox.UI.Label>();
		_widthLabel.AddClass( "designer-width-label" );
		var ls = _widthLabel.Style;
		ls.FontColor = ChromeMutedColor;
		ls.FontSize = 10f;
		ls.PointerEvents = PointerEvents.None;
		ls.Set( "white-space", "nowrap" );
	}

	private void EnsureHeightLabel( Panel root )
	{
		if ( _heightLabelPad is not null && _heightLabelPad.IsValid && _heightLabelPad.Parent == root ) return;

		_heightLabelPad = new Panel( root );
		_heightLabelPad.AddClass( "designer-dim-pad" );
		var ps = _heightLabelPad.Style;
		ps.Position = PositionMode.Absolute;
		ps.BackgroundColor = ChromePadBgColor;
		ps.PointerEvents = PointerEvents.None;
		ps.Set( "padding", "4px 2px" );
		ps.Set( "border-radius", "3px" );
		ps.Set( "display", "none" );

		_heightLabel = _heightLabelPad.AddChild<Sandbox.UI.Label>();
		_heightLabel.AddClass( "designer-height-label" );
		var ls = _heightLabel.Style;
		ls.FontColor = ChromeMutedColor;
		ls.FontSize = 10f;
		ls.PointerEvents = PointerEvents.None;
		ls.Set( "white-space", "nowrap" );
		ls.Set( "text-align", "center" );
	}

	private string _filenameLastText;
	private string _widthLastText;
	private string _heightLastText;
	private bool _chromeVisibleLast;

	private void UpdateWorkspaceChromePositions( float widthPx, float heightPx, float dpiScale )
	{
		if ( dpiScale < 0.01f ) dpiScale = 1f;

		var shouldShow = ViewportLogical is { } vpCheck && vpCheck.x >= 1f && vpCheck.y >= 1f;
		if ( !shouldShow )
		{
			if ( _chromeVisibleLast )
			{
				if ( _filenameHeader is { IsValid: true } ) _filenameHeader.Style.Set( "display", "none" );
				if ( _widthLabelPad is { IsValid: true } )  _widthLabelPad.Style.Set( "display", "none" );
				if ( _heightLabelPad is { IsValid: true } ) _heightLabelPad.Style.Set( "display", "none" );
				_chromeVisibleLast = false;
			}
			return;
		}

		var vp = ViewportLogical.Value;

		var artLeft   = RootBoundsFb.Left   / dpiScale;
		var artTop    = RootBoundsFb.Top    / dpiScale;
		var artRight  = RootBoundsFb.Right  / dpiScale;
		var artBottom = RootBoundsFb.Bottom / dpiScale;
		var artCenterX = (artLeft + artRight) * 0.5f;
		var artCenterY = (artTop + artBottom) * 0.5f;

		if ( _filenameHeader is { IsValid: true } )
		{
			var fname = FilenameSource?.Invoke() ?? "Untitled";
			var newText = $"{fname} · {vp.x:F0}×{vp.y:F0}";
			if ( _filenameLastText != newText )
			{
				_filenameHeader.Text = newText;
				_filenameLastText = newText;
			}
			var fs = _filenameHeader.Style;
			fs.Left = artLeft;
			fs.Top = MathF.Max( 4f, artTop - 20f );
			if ( !_chromeVisibleLast ) fs.Set( "display", "flex" );
		}

		// Width label — centered above the artboard's top edge.
		if ( _widthLabelPad is { IsValid: true } && _widthLabel is { IsValid: true } )
		{
			var newText = $"{vp.x:F0}";
			if ( _widthLastText != newText )
			{
				_widthLabel.Text = newText;
				_widthLastText = newText;
			}
			var ps = _widthLabelPad.Style;
			ps.Left = artCenterX;
			ps.Top = MathF.Max( 4f, artTop - 18f );
			ps.Set( "transform", "translateX(-50%)" );
			if ( !_chromeVisibleLast ) ps.Set( "display", "flex" );
		}

		// Height label — centered on the artboard's right edge.
		if ( _heightLabelPad is { IsValid: true } && _heightLabel is { IsValid: true } )
		{
			var newRaw = $"{vp.y:F0}";
			if ( _heightLastText != newRaw )
			{
				_heightLabel.Text = string.Join( "\n", newRaw.ToCharArray() );
				_heightLastText = newRaw;
			}
			var ps = _heightLabelPad.Style;
			ps.Left = artRight + 8f;
			ps.Top = artCenterY;
			ps.Set( "transform", "translateY(-50%)" );
			if ( !_chromeVisibleLast ) ps.Set( "display", "flex" );
		}

		_chromeVisibleLast = true;
	}

	public void Dispose()
	{
		Log.Info( $"{LogPrefix} DesignerScene.Dispose" );

		ForceTeardown( ScreenPanel );
		ForceTeardown( WorkspaceScreenPanel );

		Scene?.Destroy();
	}
}
