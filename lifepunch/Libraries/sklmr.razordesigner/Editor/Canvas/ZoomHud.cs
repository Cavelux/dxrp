using System;
using Editor;
using Grains.RazorDesigner.Selection;
using Sandbox;

namespace Grains.RazorDesigner.Canvas;

public sealed class ZoomHud : Widget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	// Visual tuning. All in widget px.
	private const float Inset           = 12f;
	private const float HudHeight       = 26f;
	private const float CornerRadius    = 5f;
	private const int   FontSizePx      = 12;
	private static readonly Color BgColor      = new( 0.078f, 0.086f, 0.110f, 0.92f ); // ~ rgba(20,22,28,0.92)
	private static readonly Color BorderColor  = new( 0.227f, 0.255f, 0.314f, 1f );    // ~ #3a4150
	private static readonly Color DividerColor = new( 0.165f, 0.180f, 0.220f, 1f );    // ~ #2a2e38
	private static readonly Color TextColor    = new( 0.878f, 0.894f, 0.918f, 1f );    // ~ #e0e3ea
	private static readonly Color PctColor     = new( 0.667f, 0.698f, 0.769f, 1f );    // ~ #aab2c4
	private static readonly Color IconColor    = new( 0.541f, 0.573f, 0.643f, 1f );    // ~ #8a92a4
	private static readonly Color HoverOverlay = new( 1f, 1f, 1f, 0.05f );
	private static readonly Color DisabledFg   = new( 1f, 1f, 1f, 0.25f );

	private readonly CanvasViewportFrame _frame;

	private SelectionController CurrentSelection => _frame.Selection;

	// Six segments, ordered left-to-right.
	private enum Segment { ZoomOut, Percent, ZoomIn, Fit, Reset, Selection }
	private static readonly string[] SegmentLabels = { "−", "100%", "+", "Fit", "1:1", "Sel" };
	private static readonly string[] SegmentTooltips =
	{
		"Zoom out (Ctrl+−)",
		"",
		"Zoom in (Ctrl+=)",
		"Fit content (Ctrl+0)",
		"Reset to 100% (Ctrl+1)",
		"Zoom to selection (Ctrl+Shift+0)",
	};

	private int _hoveredSegment = -1;
	private float[] _segmentWidths;

	private DesignerCanvas _canvas;
	private int _lastGeometryHash = -1;

	public ZoomHud( CanvasViewportFrame parent, DesignerCanvas canvas ) : base( parent )
	{
		_frame = parent ?? throw new ArgumentNullException( nameof( parent ) );
		_canvas = canvas ?? throw new ArgumentNullException( nameof( canvas ) );

		// Editor-side widgets do not draw at all by default; opt in.
		TranslucentBackground = true;
		NoSystemBackground = true;

		WindowFlags = WindowFlags.FramelessWindowHint | WindowFlags.Tool;

		MouseTracking = true;

		// Subscribe to view changes — refresh on every push.
		_frame.ZoomChanged += OnZoomChanged;

		Log.Info( $"{LogPrefix} ZoomHud ctor (top-level frameless window)" );
	}

	public override void OnDestroyed()
	{
		_frame.ZoomChanged -= OnZoomChanged;
		base.OnDestroyed();
	}

	[EditorEvent.Frame]
	private void TrackCanvas()
	{
		if ( !_canvas.IsValid() ) { Visible = false; return; }

		var shouldShow = _canvas.Visible && _frame.CanPanZoom;
		if ( Visible != shouldShow ) { Visible = shouldShow; if ( !shouldShow ) return; }
		if ( !shouldShow ) return;

		EnsureWidthsMeasured();
		var pref = PreferredSize;
		var canvasScreen = _canvas.ScreenPosition;
		var canvasSize = _canvas.Size;

		var hash = HashCode.Combine( canvasScreen, canvasSize, pref );
		if ( hash == _lastGeometryHash ) return;
		_lastGeometryHash = hash;

		Size = pref;
		Position = new Vector2(
			canvasScreen.x + canvasSize.x - pref.x - 12f,
			canvasScreen.y + canvasSize.y - pref.y - 12f );
		Update();
	}

	private void OnZoomChanged()
	{
		Update(); // schedule repaint; the % label re-renders from frame.Zoom.
	}

	public Vector2 PreferredSize
	{
		get
		{
			EnsureWidthsMeasured();
			float total = 0f;
			foreach ( var w in _segmentWidths ) total += w;
			return new Vector2( total, HudHeight );
		}
	}

	private void EnsureWidthsMeasured()
	{
		if ( _segmentWidths is not null ) return;
		_segmentWidths = new float[ SegmentLabels.Length ];
		Paint.SetDefaultFont( FontSizePx );
		for ( var i = 0; i < SegmentLabels.Length; i++ )
		{
			var text = i == (int)Segment.Percent ? "1000%" : SegmentLabels[i];
			var w = Paint.MeasureText( text ).x;
			_segmentWidths[i] = MathF.Max( 28f, w + 18f ); // 9px padding each side
		}
	}

	protected override void OnPaint()
	{
		EnsureWidthsMeasured();

		var rect = LocalRect;

		// Background + border.
		Paint.SetBrush( BgColor );
		Paint.SetPen( BorderColor );
		Paint.DrawRect( rect, CornerRadius );

		// Segments.
		float x = rect.Left;
		for ( var i = 0; i < SegmentLabels.Length; i++ )
		{
			var w = _segmentWidths[i];
			var segRect = new Rect( x, rect.Top, w, rect.Height );

			// Hover background. Don't highlight disabled Sel segment.
			if ( _hoveredSegment == i && IsSegmentEnabled( (Segment)i ) )
			{
				Paint.ClearPen();
				Paint.SetBrush( HoverOverlay );
				Paint.DrawRect( segRect, 0f );
			}

			// Divider (between segments).
			if ( i > 0 )
			{
				Paint.SetPen( DividerColor );
				Paint.DrawLine( new Vector2( x, rect.Top + 4f ), new Vector2( x, rect.Bottom - 4f ) );
			}

			// Label.
			var enabled = IsSegmentEnabled( (Segment)i );
			Paint.SetDefaultFont( FontSizePx );
			Paint.SetPen( ColorFor( (Segment)i, enabled ) );
			var text = (Segment)i == Segment.Percent ? CurrentPercentText() : SegmentLabels[i];
			Paint.DrawText( segRect, text, TextFlag.Center );

			x += w;
		}
	}

	private string CurrentPercentText()
	{
		var pct = (int)MathF.Round( _frame.Zoom * 100f );
		return pct + "%";
	}

	private bool IsSegmentEnabled( Segment seg ) =>
		seg != Segment.Selection || CurrentSelection?.Selected is not null;

	private Color ColorFor( Segment seg, bool enabled )
	{
		if ( !enabled ) return DisabledFg;
		return seg switch
		{
			Segment.ZoomOut or Segment.ZoomIn  => IconColor,
			Segment.Percent                    => PctColor,
			_                                  => TextColor,
		};
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );
		var newHover = SegmentAt( e.LocalPosition );
		if ( newHover != _hoveredSegment )
		{
			_hoveredSegment = newHover;
			Update();
		}
		if ( _hoveredSegment >= 0 )
			ToolTip = SegmentTooltips[_hoveredSegment];
	}

	protected override void OnMouseLeave()
	{
		base.OnMouseLeave();
		if ( _hoveredSegment != -1 )
		{
			_hoveredSegment = -1;
			Update();
		}
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );
		if ( !e.LeftMouseButton ) return;

		var seg = SegmentAt( e.LocalPosition );
		if ( seg < 0 ) return;
		if ( !IsSegmentEnabled( (Segment)seg ) ) return;

		DispatchSegment( (Segment)seg );
		e.Accepted = true;
	}

	private void DispatchSegment( Segment seg )
	{
		switch ( seg )
		{
			case Segment.ZoomOut:   _frame.ZoomBy( 1f / 1.25f ); break;
			case Segment.ZoomIn:    _frame.ZoomBy( 1.25f ); break;
			case Segment.Percent:   /* passive label — no-op */ break;
			case Segment.Fit:       _frame.ApplyFit(); break;
			case Segment.Reset:     _frame.ResetZoomOnly(); break;
			case Segment.Selection: ZoomToSelection(); break;
		}
	}

	private void ZoomToSelection()
	{
		var record = CurrentSelection?.Selected;
		var live = record?.LivePanel;
		if ( live is null || !live.IsValid )
		{
			Log.Info( $"{LogPrefix} ZoomHud Sel: no valid selection" );
			return;
		}
		_frame.ApplyZoomToRect( live.Box.Rect, /* maxZoom */ 4f, /* padding */ 0.10f );
	}

	private int SegmentAt( Vector2 localPos )
	{
		EnsureWidthsMeasured();
		if ( localPos.y < 0f || localPos.y > HudHeight ) return -1;
		float x = 0f;
		for ( var i = 0; i < _segmentWidths.Length; i++ )
		{
			x += _segmentWidths[i];
			if ( localPos.x < x ) return i;
		}
		return -1;
	}
}
