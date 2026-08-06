using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

namespace Grains.RazorDesigner.Canvas;

public readonly struct CanvasGeometry
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private static bool _logged;

	/// <summary>RootPanel.Scale — framebuffer px per CSS px. Clamped ≥ ~0.001 in <see cref="From"/>.</summary>
	public float Scale { get; }

	/// <summary>The RootPanel rect in framebuffer px, relative to the canvas widget's top-left.</summary>
	public Rect RootBoundsFb { get; }

	/// <summary>Framebuffer px per widget px. Clamped ≥ ~0.01 in <see cref="From"/>.</summary>
	public float DpiScale { get; }

	private CanvasGeometry( float scale, Rect rootBoundsFb, float dpiScale )
	{
		Scale = scale;
		RootBoundsFb = rootBoundsFb;
		DpiScale = dpiScale;
	}

	public static CanvasGeometry From( DesignerScene scene, float widgetDpiScale )
	{
		var scale = (scene is not null && scene.CurrentScale >= 0.001f) ? scene.CurrentScale : 1f;
		var dpi = widgetDpiScale >= 0.01f ? widgetDpiScale : 1f;
		var rootFb = scene?.RootBoundsFb ?? default;

		if ( !_logged )
		{
			Log.Info( $"{LogPrefix} CanvasGeometry first built (scale={scale:F3} rootFb={rootFb} dpi={dpi:F2})" );
			_logged = true;
		}

		return new CanvasGeometry( scale, rootFb, dpi );
	}

	// ---- point conversions (Vector2 → Vector2) ----
	public Vector2 WidgetToFb( Vector2 p ) => p * DpiScale;
	public Vector2 FbToWidget( Vector2 p ) => p / DpiScale;
	public Vector2 FbToCss( Vector2 p ) => new( (p.x - RootBoundsFb.Left) / Scale, (p.y - RootBoundsFb.Top) / Scale );
	public Vector2 CssToFb( Vector2 p ) => new( p.x * Scale + RootBoundsFb.Left, p.y * Scale + RootBoundsFb.Top );
	public Vector2 WidgetToCss( Vector2 p ) => FbToCss( WidgetToFb( p ) );
	public Vector2 CssToWidget( Vector2 p ) => FbToWidget( CssToFb( p ) );

	public Rect WidgetToFb( Rect r ) => new( r.Left * DpiScale, r.Top * DpiScale, r.Width * DpiScale, r.Height * DpiScale );
	public Rect FbToWidget( Rect r ) => new( r.Left / DpiScale, r.Top / DpiScale, r.Width / DpiScale, r.Height / DpiScale );
	public Rect FbToCss( Rect r ) => new( (r.Left - RootBoundsFb.Left) / Scale, (r.Top - RootBoundsFb.Top) / Scale, r.Width / Scale, r.Height / Scale );
	public Rect CssToFb( Rect r ) => new( r.Left * Scale + RootBoundsFb.Left, r.Top * Scale + RootBoundsFb.Top, r.Width * Scale, r.Height * Scale );
	public Rect WidgetToCss( Rect r ) => FbToCss( WidgetToFb( r ) );
	public Rect CssToWidget( Rect r ) => FbToWidget( CssToFb( r ) );

	// ---- deltas: scale only, no origin shift (drag math) ----
	public Vector2 WidgetDeltaToCss( Vector2 widgetDelta ) => widgetDelta * DpiScale / Scale;

	public float ConstantCss( float fbPx ) => fbPx / Scale;
	public float ConstantWidget( float widgetPx ) => widgetPx * DpiScale / Scale;

	// ---- live-panel box rects (framebuffer px). Caller must ensure p is a valid Panel. ----
	public Rect MarginBoxFb( Panel p ) => p.Box.RectOuter;
	public Rect BorderBoxFb( Panel p ) => p.Box.Rect;
	public Rect ContentBoxFb( Panel p ) => p.Box.RectInner;

	public Rect? SubtreeContentBoundsFb( IEnumerable<Panel> panels )
	{
		if ( panels is null ) return null;

		bool any = false;
		float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
		float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;

		foreach ( var p in panels )
		{
			if ( p is null || !p.IsValid ) continue;
			var r = p.Box.Rect; // border box, framebuffer px
			if ( r.Width <= 0f || r.Height <= 0f ) continue;
			if ( r.Left   < minX ) minX = r.Left;
			if ( r.Top    < minY ) minY = r.Top;
			if ( r.Right  > maxX ) maxX = r.Right;
			if ( r.Bottom > maxY ) maxY = r.Bottom;
			any = true;
		}

		if ( !any ) return null;
		return new Rect( minX, minY, maxX - minX, maxY - minY );
	}

	public static (float Left, float Top, float Width, float Height) ResolveAbsoluteCss(
		Rect borderBoxFb, Rect parentContentBoxFb, Margin marginFb, float scale )
	{
		if ( scale < 0.001f ) scale = 1f;
		// Only Left/Top margins shift the CSS `left`/`top`; Width/Height are border-box dimensions, so margin.Right/Bottom don't enter.
		return (
			(borderBoxFb.Left - parentContentBoxFb.Left) / scale - marginFb.Left / scale,
			(borderBoxFb.Top  - parentContentBoxFb.Top ) / scale - marginFb.Top  / scale,
			borderBoxFb.Width  / scale,
			borderBoxFb.Height / scale );
	}
}
