using System;
using Editor;
using Grains.RazorDesigner.Selection;
using Sandbox;

namespace Grains.RazorDesigner.Canvas;

public sealed class CanvasViewportFrame : Widget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private DesignerCanvas _canvas;
	private ZoomHud _hud;
	private CoordChip _coordChip;


	private Vector2? _viewport;
	private PreviewTargetMode _previewTarget = PreviewTargetMode.None;

	private float _zoom = 1.0f;
	private Vector2 _panOffset = Vector2.Zero;

	private const float MinZoom = 0.1f;
	private const float MaxZoom = 10f;

	private const float FitPaddingFraction       = 0.05f;
	private const float SelectionPaddingFraction = 0.10f;
	private const float FrameToContentMaxZoom    = 4.0f; // shared by Fit + Sel

	public CanvasViewportFrame( Widget parent ) : base( parent )
	{
		HorizontalSizeMode = SizeMode.Default | SizeMode.Expand;
		VerticalSizeMode = SizeMode.Default | SizeMode.Expand;
		AcceptDrops = true;
	}

	public DesignerCanvas Canvas
	{
		get => _canvas;
		set
		{
			_canvas = value;
			Log.Info( $"{LogPrefix} CanvasViewportFrame.Canvas setter fired (value={(value is null ? "null" : "valid")}, _hud={(_hud is null ? "null" : (_hud.IsValid() ? "valid" : "STALE"))}, _coordChip={(_coordChip is null ? "null" : (_coordChip.IsValid() ? "valid" : "STALE"))})" );
			if ( _canvas is not null )
			{
				// Back-reference so the canvas's OnKeyPress can dispatch zoom shortcuts to the frame.
				_canvas.ViewportFrame = this;

				// This frame is the sole layout authority; the canvas just fills it.
				_canvas.HorizontalSizeMode = SizeMode.Ignore;
				_canvas.VerticalSizeMode = SizeMode.Ignore;
				_canvas.MinimumWidth = 0;
				_canvas.MinimumHeight = 0;


				if ( TrackedFilenameSource is not null )
					_canvas.DesignerScene.FilenameSource = TrackedFilenameSource;

				PushGridToScene();

				if ( _hud is null || !_hud.IsValid() )
				{
					Log.Info( $"{LogPrefix} CanvasViewportFrame constructing ZoomHud (prior _hud={(_hud is null ? "null" : "STALE")})" );
					_hud = new ZoomHud( this, _canvas );
					_hud.Show();
					Log.Info( $"{LogPrefix} CanvasViewportFrame hosting ZoomHud" );
				}

				if ( _coordChip is null || !_coordChip.IsValid() )
				{
					Log.Info( $"{LogPrefix} CanvasViewportFrame constructing CoordChip (prior _coordChip={(_coordChip is null ? "null" : "STALE")})" );
					_coordChip = new CoordChip( _canvas, this );
					_coordChip.Show();
					_coordChip.Raise(); // ensure it paints on top of the canvas (same as ZoomHud).
					Log.Info( $"{LogPrefix} CanvasViewportFrame hosting CoordChip" );
				}
			}
			ApplyChildLayout();
		}
	}

	public Vector2? Viewport
	{
		get => _viewport;
		set
		{
			if ( _viewport == value ) return;
			_viewport = value;
			Log.Info( $"{LogPrefix} CanvasViewportFrame.Viewport={(value.HasValue ? $"{value.Value.x}x{value.Value.y}" : "Fit")}" );
			if ( _canvas is not null )
				_canvas.DesignerScene.ViewportLogical = value;
			// Mode change invalidates any prior pan/zoom (it was relative to a different surface).
			ResetView();
		}
	}

	public PreviewTargetMode PreviewTarget
	{
		get => _previewTarget;
		set
		{
			if ( _previewTarget == value ) return;
			_previewTarget = value;
			Log.Info( $"{LogPrefix} CanvasViewportFrame.PreviewTarget={value.DisplayLabel()}" );
			if ( _canvas is not null )
				_canvas.DesignerScene.PreviewTarget = value;
			ResetView();
		}
	}

	public LiveTreeMirror Mirror { get; set; }
	public SelectionController Selection { get; set; }

	private System.Func<string> _trackedFilenameSource;
	public System.Func<string> TrackedFilenameSource
	{
		get => _trackedFilenameSource;
		set
		{
			_trackedFilenameSource = value;
			if ( _canvas?.DesignerScene is { } scene )
				scene.FilenameSource = value;
		}
	}

	public readonly record struct GridSettings( bool Show, bool Snap, float StepCss );

	public GridSettings Grid { get; private set; } = new( false, false, 8f );

	/// <summary>Toolbar entry point. Idempotent — re-pushing identical settings is a no-op.</summary>
	public void SetGrid( GridSettings value )
	{
		if ( Grid == value ) return;
		Grid = value;
		Log.Info( $"{LogPrefix} Grid set: show={value.Show} snap={value.Snap} step={value.StepCss:F0}" );
		PushGridToScene();
		ApplyChildLayout();
	}

	private void PushGridToScene()
	{
		if ( _canvas?.DesignerScene is not { } scene ) return;
		scene.GridShow = Grid.Show;
		scene.GridStepCss = Grid.StepCss;
	}

	public event System.Action ZoomChanged;

	// True only when a fixed viewport is set — pan/zoom does nothing in Fit mode.
	public bool CanPanZoom => _viewport is { } v && v.x >= 1f && v.y >= 1f;

	public float Zoom => _zoom;
	public Vector2 PanOffset => _panOffset;

	public void ResetView()
	{
		_zoom = 1.0f;
		_panOffset = Vector2.Zero;
		ApplyChildLayout();
	}

	// Apply a screen-px pan delta (from DesignerCanvas's middle-drag — grd-bkgu). No-op in Fit mode.
	public void Pan( Vector2 deltaScreenPx )
	{
		if ( !CanPanZoom || _canvas is null ) return;
		_panOffset += deltaScreenPx;
		ReclampAndPush();
	}

	public void ZoomBy( float factor, Vector2? anchorWidgetPx = null )
	{
		if ( !CanPanZoom || _canvas is null || factor <= 0f ) return;

		var anchor = anchorWidgetPx ?? new Vector2( Size.x * 0.5f, Size.y * 0.5f );
		var dpi = _canvas.DpiScale > 0.01f ? _canvas.DpiScale : 1.0f;

		var r0 = PanelRectWidgetPx( dpi );
		var fx = r0.Width  > 1f ? (anchor.x - r0.Left) / r0.Width  : 0.5f;
		var fy = r0.Height > 1f ? (anchor.y - r0.Top ) / r0.Height : 0.5f;

		_zoom = Math.Clamp( _zoom * factor, MinZoom, MaxZoom );

		var r1 = PanelRectWidgetPx( dpi );
		_panOffset += new Vector2( (anchor.x - fx * r1.Width) - r1.Left, (anchor.y - fy * r1.Height) - r1.Top );

		ReclampAndPush();
	}

	public void SetView( float zoom, Vector2 panWidgetPx )
	{
		if ( !CanPanZoom ) return;
		_zoom = Math.Clamp( zoom, MinZoom, MaxZoom );
		_panOffset = panWidgetPx;
		ReclampAndPush();
	}

	public void ResetZoomOnly()
	{
		if ( !CanPanZoom ) return;
		_zoom = 1.0f;
		ReclampAndPush();
	}

	public void ApplyZoomToRect( Rect targetFb, float maxZoom, float paddingFraction )
	{
		if ( !CanPanZoom || _canvas is null ) return;
		if ( targetFb.Width <= 0f || targetFb.Height <= 0f )
		{
			Log.Warning( $"{LogPrefix} ApplyZoomToRect: degenerate target ({targetFb}); no-op" );
			return;
		}

		var dpi = _canvas.DpiScale > 0.01f ? _canvas.DpiScale : 1.0f;
		var fbW = MathF.Max( 1f, Size.x * dpi );
		var fbH = MathF.Max( 1f, Size.y * dpi );

		// Target zoom: the rect's scaled-up size should fit in (1 - 2*padding) of each axis.
		var availW = fbW * (1f - 2f * paddingFraction);
		var availH = fbH * (1f - 2f * paddingFraction);
		var zoomX  = availW / targetFb.Width;
		var zoomY  = availH / targetFb.Height;
		var targetZoom = MathF.Min( zoomX, zoomY );

		targetZoom *= _zoom;
		targetZoom = MathF.Min( targetZoom, maxZoom );
		targetZoom = Math.Clamp( targetZoom, MinZoom, MaxZoom );

		var pinned = PinnedScaleOrZero;
		var (centredBounds, scaleAtNewZoom, _) = DesignerScene.ComputeView(
			Size, dpi, _viewport, pinned, targetZoom, Vector2.Zero );

		var currentScale = _canvas.DesignerScene?.CurrentScale ?? 1f;
		if ( currentScale < 0.001f ) currentScale = 1f;
		var currentRootFb = _canvas.DesignerScene?.RootBoundsFb ?? new Rect( 0, 0, fbW, fbH );

		var targetCssX = (targetFb.Left   + targetFb.Width  * 0.5f - currentRootFb.Left) / currentScale;
		var targetCssY = (targetFb.Top    + targetFb.Height * 0.5f - currentRootFb.Top ) / currentScale;
		var targetCentreNewFb = new Vector2(
			centredBounds.Left + targetCssX * scaleAtNewZoom,
			centredBounds.Top  + targetCssY * scaleAtNewZoom );

		var canvasCentreFb = new Vector2( fbW * 0.5f, fbH * 0.5f );
		var requiredPanFb  = canvasCentreFb - targetCentreNewFb;
		var requiredPanWidget = requiredPanFb / dpi;

		SetView( targetZoom, requiredPanWidget );

		Log.Info( $"{LogPrefix} ApplyZoomToRect: targetFb={targetFb} → zoom={targetZoom:F3} pan=({requiredPanWidget.x:F1},{requiredPanWidget.y:F1})" );
	}

	public void ApplyFit()
	{
		if ( !CanPanZoom || _canvas is null ) return;

		// Build a CanvasGeometry snapshot.
		var geo = CanvasGeometry.From( _canvas.DesignerScene, _canvas.DpiScale );

		Rect targetFb;
		if ( Mirror is not null )
		{
			var bbox = geo.SubtreeContentBoundsFb( Mirror.TopLevelAuthoredPanels() );
			if ( bbox.HasValue )
			{
				targetFb = bbox.Value;
			}
			else
			{
				// Empty doc fallback — frame the RootPanel.
				targetFb = geo.RootBoundsFb;
				Log.Info( $"{LogPrefix} ApplyFit: empty content, falling back to RootPanel bbox" );
			}
		}
		else
		{
			targetFb = geo.RootBoundsFb;
			Log.Warning( $"{LogPrefix} ApplyFit: mirror unavailable; using RootPanel bbox" );
		}

		Log.Info( $"{LogPrefix} ApplyFit: targetFb={targetFb}" );
		ApplyZoomToRect( targetFb, FrameToContentMaxZoom, FitPaddingFraction );
	}

	protected override void OnMouseWheel( WheelEvent e )
	{
		if ( !CanPanZoom || _canvas is null )
			return; // not accepted -> bubbles

		var dpi = _canvas.DpiScale > 0.01f ? _canvas.DpiScale : 1.0f;
		var m = e.Position; // widget px (this frame == the canvas widget)

		// Fraction of the panel rect under the cursor — preserve it across the zoom change.
		var r0 = PanelRectWidgetPx( dpi );
		var fx = r0.Width > 1f ? (m.x - r0.Left) / r0.Width : 0.5f;
		var fy = r0.Height > 1f ? (m.y - r0.Top) / r0.Height : 0.5f;

		_zoom = Math.Clamp( _zoom * (1f + e.Delta * 0.001f), MinZoom, MaxZoom );

		// Re-pin so the same logical point stays under the cursor (zoom-toward-cursor).
		var r1 = PanelRectWidgetPx( dpi );
		_panOffset += new Vector2( (m.x - fx * r1.Width) - r1.Left, (m.y - fy * r1.Height) - r1.Top );

		ReclampAndPush();
		e.Accepted = true;
	}

	protected override void DoLayout()
	{
		base.DoLayout();
		ApplyChildLayout();
	}



	// Pin the canvas to fill us, re-clamp the pan to the (possibly new) size, and push the view to the scene.
	private void ApplyChildLayout()
	{
		if ( _canvas is null ) return;
		var size = Size;
		if ( size.x < 1f || size.y < 1f ) return;

		if ( _canvas.Position != Vector2.Zero || _canvas.Size != size )
		{
			_canvas.Position = Vector2.Zero;
			_canvas.Size = size;
		}



		ReclampAndPush();
	}

	private void ReclampAndPush()
	{
		if ( _canvas is null ) return;

		if ( CanPanZoom )
		{
			var dpi = _canvas.DpiScale > 0.01f ? _canvas.DpiScale : 1.0f;
			var (_, _, clampedPan) = DesignerScene.ComputeView( Size, dpi, _viewport, PinnedScaleOrZero, _zoom, _panOffset );
			_panOffset = clampedPan;
		}
		else
		{
			_zoom = 1.0f;
			_panOffset = Vector2.Zero;
		}

		if ( _canvas.DesignerScene is { } scene )
		{
			scene.Zoom = CanPanZoom ? _zoom : 1.0f;
			scene.PanOffsetWidgetPx = CanPanZoom ? _panOffset : Vector2.Zero;
		}

		ZoomChanged?.Invoke();
	}

	// The RootPanel rect in this widget's pixels, for cursor math. Mirrors what the scene will lay out.
	private Rect PanelRectWidgetPx( float dpi )
	{
		var (bounds, _, _) = DesignerScene.ComputeView( Size, dpi, _viewport, PinnedScaleOrZero, _zoom, _panOffset );
		return new Rect( bounds.Position / dpi, bounds.Size / dpi );
	}

	private float PinnedScaleOrZero => _previewTarget.IsPinned() ? _previewTarget.PinnedScale() : 0f;

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( new Color( 0.06f, 0.07f, 0.08f ) );
		Paint.DrawRect( LocalRect, 0f );
	}
}
