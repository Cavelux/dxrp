using System;
using Editor;
using Sandbox;

namespace Grains.RazorDesigner.Canvas;

public sealed class CoordChip : Widget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private const float Inset        = 12f;
	private const float ChipHeight   = 24f;
	private const float CornerRadius = 5f;
	private const float PaddingX     = 8f;
	private const int   FontSizePx   = 11;

	private static readonly Color BgColor     = new( 0.078f, 0.086f, 0.110f, 0.92f ); // ~ rgba(20,22,28,0.92)
	private static readonly Color BorderColor = new( 0.227f, 0.255f, 0.314f, 1f );    // ~ #3a4150
	private static readonly Color FgColor     = new( 0.812f, 0.835f, 0.886f, 1f );    // ~ #cfd5e2

	// Widest expected coord text — used by PreferredSize lazy measurement.
	private const string MeasureTemplate = "x: -9999  y: -9999";

	private readonly DesignerCanvas _canvas;
	private readonly CanvasViewportFrame _frame;

	private string _text = "x: 0  y: 0";
	private float _preferredWidth;

	private int _lastGeometryHash = -1;

	public CoordChip( DesignerCanvas canvas, CanvasViewportFrame frame ) : base( frame )
	{
		_canvas = canvas ?? throw new ArgumentNullException( nameof( canvas ) );
		_frame  = frame  ?? throw new ArgumentNullException( nameof( frame ) );

		TranslucentBackground = true;
		NoSystemBackground = true;

		WindowFlags = WindowFlags.FramelessWindowHint | WindowFlags.Tool;
		TransparentForMouseEvents = true;

		Visible = false; // hidden until first CanvasMoved.

		_canvas.CanvasMoved      += OnCanvasMoved;
		_canvas.CanvasHoverEnded += OnHoverEnded;

		Log.Info( $"{LogPrefix} CoordChip ctor (top-level frameless window)" );
	}

	public override void OnDestroyed()
	{
		_canvas.CanvasMoved      -= OnCanvasMoved;
		_canvas.CanvasHoverEnded -= OnHoverEnded;
		base.OnDestroyed();
	}

	[EditorEvent.Frame]
	private void TrackCanvas()
	{
		if ( !_canvas.IsValid() || !Visible ) return;

		var pref = PreferredSize;
		var canvasScreen = _canvas.ScreenPosition;
		var canvasSize = _canvas.Size;

		var hash = HashCode.Combine( canvasScreen, canvasSize, pref );
		if ( hash == _lastGeometryHash ) return;
		_lastGeometryHash = hash;

		Size = pref;
		Position = new Vector2(
			canvasScreen.x + 12f,
			canvasScreen.y + canvasSize.y - pref.y - 12f );
	}

	public Vector2 PreferredSize
	{
		get
		{
			EnsureWidthMeasured();
			return new Vector2( _preferredWidth, ChipHeight );
		}
	}

	private void EnsureWidthMeasured()
	{
		if ( _preferredWidth > 0f ) return;
		Paint.SetDefaultFont( FontSizePx );
		var measured = Paint.MeasureText( MeasureTemplate ).x;
		_preferredWidth = measured + PaddingX * 2f;
	}

	private void OnCanvasMoved( Vector2 widgetPos, Sandbox.KeyboardModifiers _ )
	{
		if ( _canvas?.DesignerScene is not { } scene ) return;
		var dpi = _canvas.DpiScale > 0.01f ? _canvas.DpiScale : 1f;

		var geo = CanvasGeometry.From( scene, dpi );
		var css = geo.WidgetToCss( widgetPos );

		_text = $"x: {(int)MathF.Round( css.x )}  y: {(int)MathF.Round( css.y )}";

		if ( !Visible )
		{
			_lastGeometryHash = -1;
			Visible = true;
		}

		Update();
	}

	private void OnHoverEnded()
	{
		if ( !Visible ) return;

		if ( _canvas.IsValid() )
		{
			var cursor = (Vector2)Editor.Application.CursorPosition;
			var canvasRect = new Rect( _canvas.ScreenPosition, _canvas.Size );
			if ( canvasRect.IsInside( cursor ) ) return;
		}

		Visible = false;
		Update();
	}

	protected override void OnPaint()
	{
		var rect = LocalRect;

		Paint.SetBrush( BgColor );
		Paint.SetPen( BorderColor );
		Paint.DrawRect( rect, CornerRadius );

		Paint.SetDefaultFont( FontSizePx );
		Paint.SetPen( FgColor );
		Paint.DrawText( rect, _text, TextFlag.Center );
	}
}
