using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Selection;
using Sandbox;
using Sandbox.UI;
using Length = Grains.RazorDesigner.Document.Length;

namespace Grains.RazorDesigner.Canvas;

public sealed class CanvasManipulator
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly SelectionController _selection;
	private readonly OverlayController _overlay;
	private readonly System.Func<DesignerScene> _getScene;
	private readonly System.Func<float> _getWidgetDpiScale;
	private readonly System.Action<ControlRecord> _commit;
	private readonly System.Func<(bool Enabled, float StepCss)> _getGridSnap;
	private bool _bypassUsedThisDrag;

	private bool _dragging;
	private CanvasGrab _grab;
	private ControlRecord _record;
	private Vector2 _anchorWidget;        // press point, widget px
	private float _origCssLeft, _origCssTop, _origCssW, _origCssH; // resolved CSS-px geometry at drag start
	private Rect _pressOuterFb;           // the control's margin box (framebuffer px) at drag start
	private Rect _pressParentInnerFb;     // the parent's content box (framebuffer px) at drag start
	private bool _appliedAnything;        // true once OnMove has written at least one inline style this drag
	private float _finalLeft, _finalTop, _finalW, _finalH; // last applied geometry, committed on release
	private bool _flippedThisDrag;
	private bool _flipPinnedSize;         // true when the flip pinned Width/Height (containers only)
	private bool _axisLatched;            // true once a Shift-held body drag has picked an axis this drag
	private bool _axisIsX;                // which axis the body drag is locked to (valid when _axisLatched)

	public CanvasManipulator(
		SelectionController selection,
		OverlayController overlay,
		System.Func<DesignerScene> getScene,
		System.Func<float> getWidgetDpiScale,
		System.Action<ControlRecord> commit,
		System.Func<(bool Enabled, float StepCss)> getGridSnap )
	{
		_selection = selection;
		_overlay = overlay;
		_getScene = getScene;
		_getWidgetDpiScale = getWidgetDpiScale;
		_commit = commit;
		_getGridSnap = getGridSnap;
		Log.Info( $"{LogPrefix} CanvasManipulator ctor" );
	}

	public bool IsDragging => _dragging;

	/// <summary>Returns true if this press starts a drag (caller must then NOT run click-to-select).</summary>
	public bool OnPress( Vector2 widgetPx, bool isRightClick )
	{
		if ( isRightClick ) return false;
		var grab = _overlay?.HitTest( widgetPx ) ?? CanvasGrab.None;
		if ( grab == CanvasGrab.None ) return false;

		var rec = _selection?.Selected;
		if ( rec is null || rec.LivePanel is null || !rec.LivePanel.IsValid ) return false;

		var scene = _getScene();
		if ( scene is null ) return false;

		_dragging = true;
		_grab = grab;
		_record = rec;
		_anchorWidget = widgetPx;
		_flippedThisDrag = false;
		_flipPinnedSize = false;
		_appliedAnything = false;
		_axisLatched = false;
		_axisIsX = false;
		_bypassUsedThisDrag = false;

		var geo = CanvasGeometry.From( scene, _getWidgetDpiScale() );
		var live0 = rec.LivePanel;
		var fb = geo.BorderBoxFb( live0 );             // rendered border box (framebuffer px)
		var m = live0.Box.Margin;                      // resolved margins (framebuffer px)
		var parent0 = live0.Parent;
		_pressOuterFb = geo.MarginBoxFb( live0 );      // margin box — clamp THIS inside the parent's content box
		_pressParentInnerFb = (parent0 is { IsValid: true }) ? geo.ContentBoxFb( parent0 ) : geo.RootBoundsFb;

		(_origCssLeft, _origCssTop, _origCssW, _origCssH) =
			CanvasGeometry.ResolveAbsoluteCss( fb, _pressParentInnerFb, m, geo.Scale );
		_finalLeft = _origCssLeft; _finalTop = _origCssTop; _finalW = _origCssW; _finalH = _origCssH;

		Log.Info( $"{LogPrefix} CanvasManipulator.OnPress: grab={_grab} on {rec.ClassName} (seed L={_origCssLeft:F0} T={_origCssTop:F0} W={_origCssW:F0} H={_origCssH:F0}; margin L={m.Left:F0} T={m.Top:F0})" );
		return true;
	}

	public void OnMove( Vector2 widgetPx, Sandbox.KeyboardModifiers mods )
	{
		if ( !_dragging ) return;
		if ( _record?.LivePanel is null || !_record.LivePanel.IsValid ) { Abort(); return; }

		EnsureAbsoluteIfNeeded();

		var scene = _getScene();
		if ( scene is null ) return;
		var geo = CanvasGeometry.From( scene, _getWidgetDpiScale() );
		var scale = geo.Scale;
		var d = geo.WidgetDeltaToCss( widgetPx - _anchorWidget ); // CSS-px delta

		// working geometry in CSS px, starting from the drag-start resolved values
		float left = _origCssLeft, top = _origCssTop, w = _origCssW, h = _origCssH;
		const float MinSize = 4f;

		switch ( _grab )
		{
			case CanvasGrab.Body: left += d.x; top += d.y; break;
			case CanvasGrab.E:  w += d.x; break;
			case CanvasGrab.S:  h += d.y; break;
			case CanvasGrab.SE: w += d.x; h += d.y; break;
			case CanvasGrab.W:  left += d.x; w -= d.x; break;
			case CanvasGrab.N:  top += d.y; h -= d.y; break;
			case CanvasGrab.NW: left += d.x; w -= d.x; top += d.y; h -= d.y; break;
			case CanvasGrab.NE: top += d.y; h -= d.y; w += d.x; break;
			case CanvasGrab.SW: left += d.x; w -= d.x; h += d.y; break;
		}

		bool shift = (mods & Sandbox.KeyboardModifiers.Shift) != 0;
		bool alt = (mods & Sandbox.KeyboardModifiers.Alt) != 0;

		if ( _grab == CanvasGrab.Body )
		{
			// alt has no effect on body moves; it only applies to resize grabs below
			if ( shift )
			{
				if ( !_axisLatched )
				{
					_axisLatched = true;
					_axisIsX = System.MathF.Abs( d.x ) >= System.MathF.Abs( d.y );
					Log.Info( $"{LogPrefix} CanvasManipulator: axis-lock latched (axisIsX={_axisIsX}) on {_record?.ClassName}" );
				}
				if ( _axisIsX ) top = _origCssTop; else left = _origCssLeft;
			}
			else
			{
				_axisLatched = false; // releasing Shift mid-drag drops the lock
			}
		}
		else // a resize grab
		{
			// Hoist center coords so they're in scope for both the Alt branch and the post-clamp re-center.
			float cx = _origCssLeft + _origCssW * 0.5f;
			float cy = _origCssTop + _origCssH * 0.5f;

			if ( shift && _origCssW > 0.001f && _origCssH > 0.001f )
			{
				float ratio = _origCssW / _origCssH; // W per H
				bool widthDriven = _grab switch
				{
					CanvasGrab.E or CanvasGrab.W => true,
					CanvasGrab.N or CanvasGrab.S => false,
					_ => System.MathF.Abs( w - _origCssW ) >= System.MathF.Abs( h - _origCssH ),
				};
				if ( widthDriven )
				{
					float newH = w / ratio;
					if ( _grab is CanvasGrab.NW or CanvasGrab.NE or CanvasGrab.N ) top += (h - newH);
					h = newH;
				}
				else
				{
					float newW = h * ratio;
					// newH/newW may go negative if the dragged dimension did; the MinSize clamp below recovers it (bounded overshoot)
					if ( _grab is CanvasGrab.NW or CanvasGrab.SW or CanvasGrab.W ) left += (w - newW);
					w = newW;
				}
			}

			if ( alt )
			{
				float newW = _origCssW + (w - _origCssW) * 2f;
				float newH = _origCssH + (h - _origCssH) * 2f;
				w = newW; h = newH;
				left = cx - w * 0.5f;
				top = cy - h * 0.5f;
			}

			if ( w < MinSize ) { if ( _grab is CanvasGrab.W or CanvasGrab.NW or CanvasGrab.SW ) left -= (MinSize - w); w = MinSize; }
			if ( h < MinSize ) { if ( _grab is CanvasGrab.N or CanvasGrab.NW or CanvasGrab.NE ) top -= (MinSize - h); h = MinSize; }

			// Alt center-resize: the MinSize floor above may have moved an edge; re-pin to the original center.
			if ( alt )
			{
				left = cx - w * 0.5f;
				top = cy - h * 0.5f;
			}
		}

		{
			bool ctrl = (mods & Sandbox.KeyboardModifiers.Ctrl) != 0;
			if ( ctrl ) _bypassUsedThisDrag = true;

			var grid = _getGridSnap();
			if ( grid.Enabled && !ctrl && grid.StepCss > 0.001f )
			{
				float origRight  = _origCssLeft + _origCssW;
				float origBottom = _origCssTop  + _origCssH;
				float step = grid.StepCss;
				static float Snap( float v, float s ) => System.MathF.Round( v / s ) * s;

				switch ( _grab )
				{
					case CanvasGrab.Body:
						left = Snap( left, step );
						top  = Snap( top,  step );
						break;

					case CanvasGrab.E:
					{
						float r = Snap( left + w, step );
						w = System.MathF.Max( MinSize, r - left );
						break;
					}
					case CanvasGrab.W:
					{
						float l = Snap( left, step );
						left = l;
						w = System.MathF.Max( MinSize, origRight - l );
						break;
					}
					case CanvasGrab.S:
					{
						float b = Snap( top + h, step );
						h = System.MathF.Max( MinSize, b - top );
						break;
					}
					case CanvasGrab.N:
					{
						float t = Snap( top, step );
						top = t;
						h = System.MathF.Max( MinSize, origBottom - t );
						break;
					}

					case CanvasGrab.SE:
					{
						float r = Snap( left + w, step );
						float b = Snap( top  + h, step );
						w = System.MathF.Max( MinSize, r - left );
						h = System.MathF.Max( MinSize, b - top );
						break;
					}
					case CanvasGrab.NW:
					{
						float l = Snap( left, step );
						float t = Snap( top,  step );
						left = l; top = t;
						w = System.MathF.Max( MinSize, origRight  - l );
						h = System.MathF.Max( MinSize, origBottom - t );
						break;
					}
					case CanvasGrab.NE:
					{
						float r = Snap( left + w, step );
						float t = Snap( top,      step );
						top = t;
						w = System.MathF.Max( MinSize, r - left );
						h = System.MathF.Max( MinSize, origBottom - t );
						break;
					}
					case CanvasGrab.SW:
					{
						float l = Snap( left,      step );
						float b = Snap( top  + h,  step );
						left = l;
						w = System.MathF.Max( MinSize, origRight - l );
						h = System.MathF.Max( MinSize, b - top );
						break;
					}
				}

				if ( shift && _grab != CanvasGrab.Body && _origCssW > 0.001f && _origCssH > 0.001f )
				{
					float ratio = _origCssW / _origCssH;
					bool widthDriven = _grab switch
					{
						CanvasGrab.E or CanvasGrab.W => true,
						CanvasGrab.N or CanvasGrab.S => false,
						_ => System.MathF.Abs( w - _origCssW ) >= System.MathF.Abs( h - _origCssH ),
					};
					if ( widthDriven )
					{
						float newH = w / ratio;
						if ( _grab is CanvasGrab.NW or CanvasGrab.NE or CanvasGrab.N ) top += (h - newH);
						h = System.MathF.Max( MinSize, newH );
					}
					else
					{
						float newW = h * ratio;
						if ( _grab is CanvasGrab.NW or CanvasGrab.SW or CanvasGrab.W ) left += (w - newW);
						w = System.MathF.Max( MinSize, newW );
					}
				}

				// Alt center-symm re-pin: recompute mirror against start center post-snap.
				if ( alt && _grab != CanvasGrab.Body )
				{
					float cx = _origCssLeft + _origCssW * 0.5f;
					float cy = _origCssTop  + _origCssH * 0.5f;
					left = cx - w * 0.5f;
					top  = cy - h * 0.5f;
				}
			}
		}

		var parentPanel = _record.LivePanel.Parent;
		var innerFb = (parentPanel is { IsValid: true }) ? parentPanel.Box.RectInner : _pressParentInnerFb;
		{
			float dL = (left - _origCssLeft) * scale, dT = (top - _origCssTop) * scale;
			float dW = (_grab == CanvasGrab.Body) ? 0f : (w - _origCssW) * scale;
			float dH = (_grab == CanvasGrab.Body) ? 0f : (h - _origCssH) * scale;
			float oL = _pressOuterFb.Left + dL, oT = _pressOuterFb.Top + dT;
			float oW = _pressOuterFb.Width + dW, oH = _pressOuterFb.Height + dH;
			if ( innerFb.Width >= oW )
			{
				float cL = System.Math.Clamp( oL, innerFb.Left, innerFb.Left + innerFb.Width - oW );
				left += (cL - oL) / scale;
			}
			if ( innerFb.Height >= oH )
			{
				float cT = System.Math.Clamp( oT, innerFb.Top, innerFb.Top + innerFb.Height - oH );
				top += (cT - oT) / scale;
			}
		}

		var st = _record.LivePanel.Style;
		bool isAbsolute = _flippedThisDrag || _record.Position == PositionKind.Absolute;
		if ( isAbsolute )
		{
			st.Left = left;
			st.Top = top;
		}
		if ( _grab != CanvasGrab.Body )
		{
			st.Width = w;
			st.Height = h;
		}
		st.Dirty();

		_finalLeft = left; _finalTop = top; _finalW = w; _finalH = h;
		_appliedAnything = true;

		// Drag badge: size for a resize grab, signed offset for a body (move) grab.
		string badge = _grab == CanvasGrab.Body
			? $"Δ {Signed( left - _origCssLeft )}, {Signed( top - _origCssTop )}"
			: $"{System.MathF.Round( w )} × {System.MathF.Round( h )}";
		_overlay?.SetDimBadge( badge );
	}

	public void OnRelease( Vector2 widgetPx )
	{
		if ( !_dragging ) return;
		var rec = _record;
		var flipped = _flippedThisDrag;
		var resized = _grab != CanvasGrab.Body;
		var applied = _appliedAnything;
		_dragging = false;
		_grab = CanvasGrab.None;
		_record = null;
		_overlay?.SetDimBadge( null );

		if ( rec is null ) return;

		if ( !applied )
		{
			// Press + release with no movement (e.g. clicking the already-selected control's body).
			Log.Info( $"{LogPrefix} CanvasManipulator.OnRelease: no movement on {rec.ClassName}; nothing to commit" );
			return;
		}

		if ( flipped ) rec.Position = PositionKind.Absolute;
		bool isAbsolute = rec.Position == PositionKind.Absolute;
		if ( isAbsolute )
		{
			rec.Left = Length.Px( _finalLeft );
			rec.Top = Length.Px( _finalTop );
		}
		if ( resized )
		{
			rec.Width = Length.Px( _finalW );
			rec.Height = Length.Px( _finalH );
		}
		else if ( flipped && _flipPinnedSize )
		{
			rec.Width = Length.Px( _finalW );
			rec.Height = Length.Px( _finalH );
		}

		if ( rec.LivePanel is { IsValid: true } live )
		{
			var st = live.Style;
			st.Left = null;
			st.Top = null;
			st.Width = null;
			st.Height = null;
			st.Position = null;
			st.Dirty();
		}

		var snap = _getGridSnap();
		if ( snap.Enabled || _bypassUsedThisDrag )
			Log.Info( $"{LogPrefix} CanvasManipulator: snap step={snap.StepCss:F0}px enabled={snap.Enabled} bypassUsed={_bypassUsedThisDrag}" );

		Log.Info( $"{LogPrefix} CanvasManipulator.OnRelease: committing {rec.ClassName} (L={_finalLeft:F0} T={_finalTop:F0} W={_finalW:F0} H={_finalH:F0} abs={isAbsolute})" );
		_commit( rec );
	}

	private static bool NeedsAbsolute( CanvasGrab g ) =>
		g is CanvasGrab.Body or CanvasGrab.NW or CanvasGrab.N or CanvasGrab.NE or CanvasGrab.W or CanvasGrab.SW;

	private void EnsureAbsoluteIfNeeded()
	{
		if ( _flippedThisDrag ) return;
		if ( !NeedsAbsolute( _grab ) ) return;
		_flippedThisDrag = true;
		_flipPinnedSize = ContractScanner.Table.Get( _record.Type ).IsContainer;

		if ( _record.Position == PositionKind.Absolute )
			return; // already absolute — nothing to flip; the working baseline is its resolved geometry

		var st = _record.LivePanel.Style;
		st.Position = PositionMode.Absolute;
		st.Left = _origCssLeft;
		st.Top = _origCssTop;
		if ( _flipPinnedSize )
		{
			st.Width = _origCssW;
			st.Height = _origCssH;
		}
		else
		{
			_pressOuterFb = new Rect( _pressOuterFb.Left, _pressOuterFb.Top, 0f, 0f );
		}
		st.Dirty();
		Log.Info( $"{LogPrefix} CanvasManipulator: auto-flipped {_record.ClassName} to Position=Absolute (inline, pinSize={_flipPinnedSize})" );
	}

	// "+24" / "-8" / "0" — signed integer px for the move badge.
	private static string Signed( float v )
	{
		int n = (int)System.MathF.Round( v );
		return n > 0 ? $"+{n}" : n.ToString();
	}

	private void Abort()
	{
		Log.Warning( $"{LogPrefix} CanvasManipulator: live panel went stale mid-drag; aborting" );
		_dragging = false;
		_grab = CanvasGrab.None;
		_record = null;
		_flippedThisDrag = false;
		_flipPinnedSize = false;
		_appliedAnything = false;
		_axisLatched = false;
		_axisIsX = false;
		_bypassUsedThisDrag = false;
		_overlay?.SetDimBadge( null );
	}
}
