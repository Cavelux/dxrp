using System;
using Grains.RazorDesigner.Selection;
using Sandbox;
using Sandbox.UI;

namespace Grains.RazorDesigner.Canvas;

// The eight resize grips, in reading order. Body == "drag to move".
public enum CanvasGrab { None, Body, NW, N, NE, E, SE, S, SW, W }

public sealed class OverlayController
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const float HandlePx = 12f;       // CSS-px size of a handle square
	private const float OutsetPx = 2f;        // overlay rect bleeds this far outside the target

	private readonly SelectionController _selection;

	private Panel _overlay;                    // the outline rect
	// Indexed by (int)CanvasGrab; CanvasGrab.W == 9 is the largest, so length 10. [0]=None/[1]=Body unused.
	private readonly Panel[] _handles = new Panel[(int)CanvasGrab.W + 1];

	// Last on-screen geometry in widget px (top-left origin), for hit-testing. Default = empty/off.
	private Rect _bodyRectWidget;
	private readonly Rect[] _handleRectsWidget = new Rect[(int)CanvasGrab.W + 1];
	private bool _visible;

	private Label _dimBadge;                    // small "W × H" / "Δ x, y" pill shown during a drag
	private string _badgeText;                  // null/empty == hidden

	// Hover overlay (Tier 2 feature I, grd-snsc): faint outline tracking the deepest node under cursor.
	private Panel _hoverOverlay;                // the hover rect (sibling of _overlay under canvas root)
	private Label _hoverLabel;                  // classname chip, child of _hoverOverlay
	private Document.ControlRecord _hoverTarget; // current hover record; null == hidden
	private bool _hoverVisible;

	private string _hoverLabelLastText;
	private float _hoverLabelLastFontSize = -1f;
	private bool _hoverLabelStylesPinned;

	private bool _previewActive;
	public void SetPreviewActive( bool active )
	{
		if ( active == _previewActive ) return;
		_previewActive = active;
		if ( _hoverTarget?.LivePanel is { IsValid: true } p )
			p.Switch( PseudoClass.Hover, !active );
		Log.Info( $"{LogPrefix} OverlayController.SetPreviewActive: {active}" );
	}

	public OverlayController( SelectionController selection )
	{
		_selection = selection;
		Log.Info( $"{LogPrefix} OverlayController ctor" );
	}

	/// <summary>(Re)create the overlay panel + handles under <paramref name="rootPanel"/>. Call after every mirror repopulate.</summary>
	public void Reattach( Panel rootPanel )
	{
		_overlay = null;
		for ( int i = 0; i < _handles.Length; i++ ) _handles[i] = null;
		_visible = false;
		_dimBadge = null;
		_badgeText = null;
		_hoverOverlay = null;
		_hoverLabel = null;
		_hoverTarget = null;
		_hoverVisible = false;
		_hoverLabelLastText = null;
		_hoverLabelLastFontSize = -1f;
		_hoverLabelStylesPinned = false;

		if ( rootPanel is null || !rootPanel.IsValid )
		{
			Log.Warning( $"{LogPrefix} OverlayController.Reattach: root not valid; overlay not created" );
			return;
		}

		_overlay = rootPanel.AddChild<Panel>();
		_overlay.AddClass( "rd-selection-overlay" );
		var s = _overlay.Style;
		s.Position = PositionMode.Absolute;
		s.BorderColor = Color.Parse( "#3da9fc" ) ?? Color.Cyan;
		s.BorderWidth = 1f;
		s.PointerEvents = PointerEvents.None;
		s.ZIndex = 10000;
		_overlay.Style.Set( "display", "none" );

		for ( int i = (int)CanvasGrab.NW; i <= (int)CanvasGrab.W; i++ )
		{
			var h = _overlay.AddChild<Panel>();
			h.AddClass( "rd-selection-handle" );
			var hs = h.Style;
			hs.Position = PositionMode.Absolute;
			hs.Width = HandlePx;
			hs.Height = HandlePx;
			hs.BackgroundColor = Color.White;
			hs.BorderColor = Color.Parse( "#3da9fc" ) ?? Color.Cyan;
			hs.BorderWidth = 1f;
			hs.PointerEvents = PointerEvents.None;
			_handles[i] = h;
		}

		_dimBadge = _overlay.AddChild<Label>();
		_dimBadge.AddClass( "rd-dim-badge" );
		{
			var bs = _dimBadge.Style;
			bs.Position = PositionMode.Absolute;
			bs.BackgroundColor = Color.Parse( "#3da9fc" ) ?? Color.Cyan;
			bs.FontColor = Color.Parse( "#06121f" ) ?? Color.Black;
			bs.PointerEvents = PointerEvents.None;
			bs.Set( "border-radius", "4px" );
			bs.Set( "padding", "2px 6px" );
			bs.Set( "white-space", "nowrap" );
			bs.Set( "display", "none" );
		}

		_hoverOverlay = rootPanel.AddChild<Panel>();
		_hoverOverlay.AddClass( "rd-hover-overlay" );
		{
			var hs = _hoverOverlay.Style;
			hs.Position = PositionMode.Absolute;
			var hoverColor = (Color.Parse( "#3da9fc" ) ?? Color.Cyan).WithAlpha( 0.5f );
			hs.BorderColor = hoverColor;
			hs.BorderWidth = 1f;
			hs.PointerEvents = PointerEvents.None;
			hs.ZIndex = 9999;
			hs.Set( "display", "none" );
		}

		_hoverLabel = _hoverOverlay.AddChild<Label>();
		_hoverLabel.AddClass( "rd-hover-label" );
		{
			var ls = _hoverLabel.Style;
			ls.Position = PositionMode.Absolute;
			var chipBg = (Color.Parse( "#3da9fc" ) ?? Color.Cyan).WithAlpha( 0.7f );
			ls.BackgroundColor = chipBg;
			ls.FontColor = Color.Parse( "#06121f" ) ?? Color.Black;
			ls.PointerEvents = PointerEvents.None;
			ls.Set( "white-space", "nowrap" );
			ls.Set( "display", "none" );
		}

		Log.Info( $"{LogPrefix} OverlayController.Reattach: overlay + 8 handles + dim badge + hover rect + hover label created under root" );
	}

	/// <summary>True when the overlay is currently shown (a non-root control is selected with a live panel).</summary>
	public bool Visible => _visible;
	public Rect BodyRectWidget => _bodyRectWidget;
	public Rect HandleRectWidget( CanvasGrab g ) => _handleRectsWidget[(int)g];

	/// <summary>Set the text of the drag dimension badge, or null/empty to hide it. Positioned next Tick.</summary>
	public void SetDimBadge( string text )
	{
		_badgeText = text;
		if ( string.IsNullOrEmpty( text ) && _dimBadge is { IsValid: true } )
			_dimBadge.Style.Set( "display", "none" );
		// Show + positioning are deferred to Tick so it lays out with the current frame's `scale`.
	}

	/// <summary>Which grab (if any) the widget-px point lands on. Handles win over the body. Returns None when hidden.</summary>
	public CanvasGrab HitTest( Vector2 widgetPx )
	{
		if ( !_visible ) return CanvasGrab.None;
		for ( int i = (int)CanvasGrab.NW; i <= (int)CanvasGrab.W; i++ )
			if ( _handleRectsWidget[i].IsInside( widgetPx ) ) return (CanvasGrab)i;
		return _bodyRectWidget.IsInside( widgetPx ) ? CanvasGrab.Body : CanvasGrab.None;
	}

	public void SetHoverTarget( Document.ControlRecord target )
	{
		if ( target == _hoverTarget ) return;

		if ( !_previewActive )
		{
			if ( _hoverTarget?.LivePanel is { IsValid: true } oldP )
				oldP.Switch( PseudoClass.Hover, false );
			if ( target?.LivePanel is { IsValid: true } newP )
				newP.Switch( PseudoClass.Hover, true );
		}

		_hoverTarget = target;
		Log.Info( $"{LogPrefix} hover target → {target?.ClassName ?? "<none>"}" );
	}

	public void Tick( DesignerScene scene, float widgetDpiScale )
	{
		if ( _overlay is null || !_overlay.IsValid )
		{
			_visible = false;
			if ( _hoverVisible && _hoverOverlay is { IsValid: true } )
			{
				_hoverOverlay.Style.Set( "display", "none" );
				_hoverVisible = false;
			}
			return;
		}

		var sel = _selection?.Selected;
		var live = sel?.LivePanel;
		var isRoot = sel is not null && scene is not null && live == scene.Root;
		bool selectionValid = sel is not null && !isRoot && live is not null && live.IsValid && !_previewActive;
		if ( !selectionValid )
		{
			if ( _visible ) { _overlay.Style.Set( "display", "none" ); _visible = false; }
		}

		var geo = CanvasGeometry.From( scene, widgetDpiScale );

		if ( selectionValid )
		{
			var targetFb = geo.BorderBoxFb( live ); // framebuffer px, absolute
			var targetCss = geo.FbToCss( targetFb ); // overlay is a child of Root, so Root-CSS == overlay-CSS

			float outsetCss = geo.ConstantWidget( OutsetPx );
			float handleCss = geo.ConstantWidget( HandlePx );

			float cssLeft = targetCss.Left - outsetCss;
			float cssTop = targetCss.Top - outsetCss;
			float cssW = targetCss.Width + outsetCss * 2f;
			float cssH = targetCss.Height + outsetCss * 2f;

			var s = _overlay.Style;
			s.Left = cssLeft; s.Top = cssTop; s.Width = cssW; s.Height = cssH;
			s.BorderWidth = MathF.Max( geo.ConstantWidget( 1f ), 0.5f );
			if ( !_visible ) { s.Set( "display", "flex" ); _visible = true; }

			// place the 8 handles, centred on the corners/edge-midpoints, in the overlay's local CSS space
			PlaceHandle( CanvasGrab.NW, 0f, 0f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.N, 0.5f, 0f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.NE, 1f, 0f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.E, 1f, 0.5f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.SE, 1f, 1f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.S, 0.5f, 1f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.SW, 0f, 1f, cssW, cssH, handleCss );
			PlaceHandle( CanvasGrab.W, 0f, 0.5f, cssW, cssH, handleCss );

			// record on-screen widget-px rects for hit-testing.
			_bodyRectWidget = geo.FbToWidget( targetFb );
			float halfWidget = HandlePx * 0.5f + 1f;
			RecordHandleWidgetRect( CanvasGrab.NW, _bodyRectWidget.Left, _bodyRectWidget.Top, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.N, _bodyRectWidget.Left + _bodyRectWidget.Width * 0.5f, _bodyRectWidget.Top, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.NE, _bodyRectWidget.Left + _bodyRectWidget.Width, _bodyRectWidget.Top, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.E, _bodyRectWidget.Left + _bodyRectWidget.Width, _bodyRectWidget.Top + _bodyRectWidget.Height * 0.5f, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.SE, _bodyRectWidget.Left + _bodyRectWidget.Width, _bodyRectWidget.Top + _bodyRectWidget.Height, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.S, _bodyRectWidget.Left + _bodyRectWidget.Width * 0.5f, _bodyRectWidget.Top + _bodyRectWidget.Height, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.SW, _bodyRectWidget.Left, _bodyRectWidget.Top + _bodyRectWidget.Height, halfWidget );
			RecordHandleWidgetRect( CanvasGrab.W, _bodyRectWidget.Left, _bodyRectWidget.Top + _bodyRectWidget.Height * 0.5f, halfWidget );

			if ( _dimBadge is { IsValid: true } )
			{
				if ( string.IsNullOrEmpty( _badgeText ) )
				{
					_dimBadge.Style.Set( "display", "none" );
				}
				else
				{
					_dimBadge.Text = _badgeText;
					var bs = _dimBadge.Style;
					bs.Set( "display", "flex" );
					bs.FontSize = geo.ConstantWidget( 13f );
					bs.Set( "border-radius", $"{geo.ConstantWidget( 4f )}px" );
					bs.Set( "padding", $"{geo.ConstantWidget( 2f )}px {geo.ConstantWidget( 6f )}px" );
					bs.Right = geo.ConstantWidget( 4f );
					bs.Bottom = geo.ConstantWidget( 4f );
					bs.Left = null;
					bs.Top = null;
				}
			}
		}

		if ( _hoverOverlay is { IsValid: true } )
		{
			var hoverTarget = _hoverTarget;
			var hoverLive = hoverTarget?.LivePanel;
			var hoverIsRoot = scene is not null && hoverLive == scene.Root;
			bool show = hoverTarget is not null
				&& hoverLive is { IsValid: true }
				&& hoverTarget != _selection?.Selected
				&& !hoverIsRoot
				&& !_previewActive;

			if ( !show )
			{
				if ( _hoverVisible ) { _hoverOverlay.Style.Set( "display", "none" ); _hoverVisible = false; }
			}
			else
			{
				var hoverFb = geo.BorderBoxFb( hoverLive );
				var hoverCss = geo.FbToCss( hoverFb );
				var hs = _hoverOverlay.Style;
				hs.Left = hoverCss.Left;
				hs.Top = hoverCss.Top;
				hs.Width = hoverCss.Width;
				hs.Height = hoverCss.Height;
				hs.BorderWidth = MathF.Max( geo.ConstantWidget( 1f ), 0.5f );
				if ( !_hoverVisible ) { hs.Set( "display", "flex" ); _hoverVisible = true; }

				if ( _hoverLabel is { IsValid: true } )
				{
					var ls = _hoverLabel.Style;
					var newText = hoverTarget.ClassName ?? string.Empty;
					if ( _hoverLabelLastText != newText )
					{
						_hoverLabel.Text = newText;
						_hoverLabelLastText = newText;
					}

					var newFontSize = geo.ConstantWidget( 11f );
					// Tolerance: only re-emit on changes > 0.25 widget-px — anything smaller is float jitter.
					if ( MathF.Abs( newFontSize - _hoverLabelLastFontSize ) > 0.25f )
					{
						ls.FontSize = newFontSize;
						ls.Set( "border-radius", $"{geo.ConstantWidget( 3f )}px" );
						ls.Set( "padding", $"{geo.ConstantWidget( 1f )}px {geo.ConstantWidget( 4f )}px" );
						_hoverLabelLastFontSize = newFontSize;
					}

					// Pin once on first show — these props never change while the label is visible.
					if ( !_hoverLabelStylesPinned )
					{
						ls.Set( "bottom", "100%" );    // chip sits above the rect
						ls.Left = 0f;                    // flush left
						ls.Right = null;
						ls.Top = null;
						ls.Set( "display", "flex" );
						_hoverLabelStylesPinned = true;
					}
				}
			}
		}
	}

	private void PlaceHandle( CanvasGrab g, float fx, float fy, float overlayW, float overlayH, float handleCss )
	{
		var h = _handles[(int)g];
		if ( h is null || !h.IsValid ) return;
		var hs = h.Style;
		hs.Width = handleCss;
		hs.Height = handleCss;
		hs.BorderWidth = MathF.Max( handleCss / HandlePx, 0.5f ); // == 1/scale; keeps the 1px border ~constant on screen
		hs.Left = overlayW * fx - handleCss * 0.5f;
		hs.Top = overlayH * fy - handleCss * 0.5f;
	}

	private void RecordHandleWidgetRect( CanvasGrab g, float cx, float cy, float half )
	{
		_handleRectsWidget[(int)g] = new Rect( cx - half, cy - half, half * 2f, half * 2f );
	}
}
