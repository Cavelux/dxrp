using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Canvas;

// Same shape as ShaderGraph PreviewPanel: override PreFrame to advance the scene.
public class DesignerCanvas : SceneRenderingWidget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly DesignerScene _designerScene;

	public DesignerCanvas( Widget parent ) : base( parent )
	{
		Log.Info( $"{LogPrefix} DesignerCanvas ctor" );

		_designerScene = new DesignerScene();

		Scene = _designerScene.Scene;
		Camera = _designerScene.Camera;

		HorizontalSizeMode = SizeMode.Default | SizeMode.Expand;
		VerticalSizeMode = SizeMode.Default | SizeMode.Expand;

		// Without AcceptDrops, OnDragHover/OnDragDrop are never invoked.
		AcceptDrops = true;

		MouseTracking = true;
	}

	public DesignerScene DesignerScene => _designerScene;

	// Assigned by DesignerWindow after construction (mirrors how _viewportFrame.Canvas is wired).
	public OverlayController Overlay { get; set; }

	public CanvasViewportFrame ViewportFrame { get; set; }

	public event System.Action<Vector2, bool, Sandbox.KeyboardModifiers> CanvasClicked;
	public event System.Action<Vector2, Sandbox.KeyboardModifiers> CanvasMoved;
	public event System.Action<Vector2> CanvasReleased;

	public event System.Action<Vector2> CanvasPanDragged; // delta, screen px

	// Fires when the cursor leaves the canvas widget. Used to clear hover-pick state.
	public event System.Action CanvasHoverEnded;

	public event System.Action<ControlType, Vector2> RecordDropped;
	// Mirrors RecordDropped but for saved palette templates.
	public event System.Action<Grains.RazorDesigner.Templates.PaletteTemplate, Vector2> TemplateDropped;

	private bool _middlePanning;
	private Vector2 _lastPanScreen;

	private const bool ProbeFrameCost = false;
	private const int ProbeWindow = 120;
	private readonly System.Diagnostics.Stopwatch _probeSw = new();
	private double _probeTickMs;
	private double _probeUpdateMs;
	private int _probeFrames;

	public override void OnDragHover( DragEvent ev )
	{
		base.OnDragHover( ev );
		if ( ev.Data.Object is ControlType || ev.Data.Object is Grains.RazorDesigner.Templates.PaletteTemplate )
		{
			ev.Action = DropAction.Copy;
		}
	}

	public override void OnDragDrop( DragEvent ev )
	{
		base.OnDragDrop( ev );
		if ( ev.Data.Object is ControlType type )
		{
			Log.Info( $"{LogPrefix} DesignerCanvas drop: {type} at widget ({ev.LocalPosition.x:F0}, {ev.LocalPosition.y:F0})" );
			RecordDropped?.Invoke( type, ev.LocalPosition );
		}
		else if ( ev.Data.Object is Grains.RazorDesigner.Templates.PaletteTemplate template )
		{
			Log.Info( $"{LogPrefix} DesignerCanvas drop: template \"{template.Name}\" at widget ({ev.LocalPosition.x:F0}, {ev.LocalPosition.y:F0})" );
			TemplateDropped?.Invoke( template, ev.LocalPosition );
		}
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( e.MiddleMouseButton )
		{
			// Swallow entirely — don't let base (SceneRenderingWidget) see the middle drag.
			_middlePanning = true;
			_lastPanScreen = e.ScreenPosition;
			e.Accepted = true;
			return;
		}
		base.OnMousePress( e );
		if ( e.LeftMouseButton || e.RightMouseButton )
		{
			var pos = e.LocalPosition;
			Log.Info( $"{LogPrefix} DesignerCanvas click at widget ({pos.x:F0}, {pos.y:F0}) right={e.RightMouseButton}" );
			CanvasClicked?.Invoke( pos, e.RightMouseButton, e.KeyboardModifiers );
		}
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( _middlePanning )
		{
			var s = e.ScreenPosition;
			CanvasPanDragged?.Invoke( s - _lastPanScreen );
			_lastPanScreen = s;
			e.Accepted = true;
			return;
		}
		base.OnMouseMove( e );
		CanvasMoved?.Invoke( e.LocalPosition, e.KeyboardModifiers );
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		if ( _middlePanning )
		{
			_middlePanning = false;
			e.Accepted = true;
			return;
		}
		base.OnMouseReleased( e );
		CanvasReleased?.Invoke( e.LocalPosition );
	}

	protected override void OnMouseLeave()
	{
		base.OnMouseLeave();
		CanvasHoverEnded?.Invoke();
	}

	protected override void PreFrame()
	{
		base.PreFrame();

		if ( !_designerScene.Scene.IsValid() )
			return;

		if ( ProbeFrameCost ) _probeSw.Restart();

		using ( _designerScene.Scene.Push() )
		{
			// EditorTick (not GameTick): preview scene, no SceneNetworkUpdate / fixed physics.
			_designerScene.Scene.EditorTick( RealTime.Now, RealTime.Delta );
		}

		if ( ProbeFrameCost ) { _probeTickMs += _probeSw.Elapsed.TotalMilliseconds; _probeSw.Restart(); }

		// Layout in framebuffer space (Size * DpiScale); DesignerScene sets Scale = DpiScale so authoring stays in logical px.
		_designerScene.Update( Size.x, Size.y, DpiScale );

		Overlay?.Tick( _designerScene, DpiScale );

		if ( ProbeFrameCost )
		{
			_probeUpdateMs += _probeSw.Elapsed.TotalMilliseconds;
			if ( ++_probeFrames >= ProbeWindow )
			{
				Log.Info( $"{LogPrefix} probe: EditorTick avg {_probeTickMs / _probeFrames:F3}ms | Update avg {_probeUpdateMs / _probeFrames:F3}ms (over {_probeFrames} frames, canvas {Size.x:F0}x{Size.y:F0})" );
				_probeTickMs = 0;
				_probeUpdateMs = 0;
				_probeFrames = 0;
			}
		}
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		if ( ViewportFrame is not { CanPanZoom: true } frame ) return;

		var mods  = e.KeyboardModifiers;
		var ctrl  = (mods & Sandbox.KeyboardModifiers.Ctrl)  != 0;
		var shift = (mods & Sandbox.KeyboardModifiers.Shift) != 0;
		var alt   = (mods & Sandbox.KeyboardModifiers.Alt)   != 0;

		if ( !ctrl || alt ) return; // every shortcut is Ctrl-based; Alt clears it.

		switch ( e.Key )
		{
			case KeyCode.Num0:
				if ( shift )
				{
					Log.Info( $"{LogPrefix} OnKeyPress Ctrl+Shift+0 → ZoomToSelection" );
					ZoomToSelectionViaFrame( frame );
				}
				else
				{
					Log.Info( $"{LogPrefix} OnKeyPress Ctrl+0 → ApplyFit" );
					frame.ApplyFit();
				}
				e.Accepted = true;
				break;

			case KeyCode.Num1:
				if ( shift ) return;
				Log.Info( $"{LogPrefix} OnKeyPress Ctrl+1 → ResetZoomOnly" );
				frame.ResetZoomOnly();
				e.Accepted = true;
				break;

			case KeyCode.Equal:
				if ( shift ) return;
				frame.ZoomBy( 1.25f );
				e.Accepted = true;
				break;

			case KeyCode.Minus:
				if ( shift ) return;
				frame.ZoomBy( 1f / 1.25f );
				e.Accepted = true;
				break;
		}
	}

	private void ZoomToSelectionViaFrame( CanvasViewportFrame frame )
	{
		var record = frame.Selection?.Selected;
		var live = record?.LivePanel;
		if ( live is null || !live.IsValid )
		{
			Log.Info( $"{LogPrefix} Ctrl+Shift+0: no valid selection" );
			return;
		}
		frame.ApplyZoomToRect( live.Box.Rect, /* maxZoom */ 4f, /* padding */ 0.10f );
	}

	public override void OnDestroyed()
	{
		Log.Info( $"{LogPrefix} DesignerCanvas.OnDestroyed" );
		base.OnDestroyed();
		_designerScene.Dispose();
	}
}
