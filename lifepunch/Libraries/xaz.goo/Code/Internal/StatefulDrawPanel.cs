using System;
using Sandbox;
using Sandbox.Rendering;
using Sandbox.UI;
using Goo.Animation;

namespace Goo.Internal;

internal sealed class StatefulDrawPanel : Panel, IPanelDraw, IStatefulHost, IStatefulEventHost
{
    StateController? _state;

    internal Action<MousePanelEvent>? _onClick;
    internal Action<MousePanelEvent>? _onRightClick;
    internal Action<MousePanelEvent>? _onMiddleClick;
    internal Action<MousePanelEvent>? _onMouseEnter;
    internal Action<MousePanelEvent>? _onMouseLeave;
    bool _hoverInside;   // gates true enter/leave over the engine's bubbling over/out
    internal Action<MousePanelEvent>? _onMouseDown;
    internal Action<MousePanelEvent>? _onMouseUp;
    internal Action<MousePanelEvent>? _onMouseMove;
    internal Action<Vector2>?         _onMouseWheel;
    internal bool _userSetPointerEvents;
    internal Action? _requestRebuild;
    public Action? RequestRebuild { set => _requestRebuild = value; }

    internal Action<DragEvent>?  _onDragStart;
    internal Action<DragEvent>?  _onDrag;
    internal Action<DragEvent>?  _onDragEnd;
    internal Action<PanelEvent>? _onDragEnter;
    internal Action<PanelEvent>? _onDragLeave;
    internal Action<PanelEvent>? _onDrop;
    bool _wantsDrag;

    // User immediate-mode draw callback (null = no custom draw).
    internal DrawCallback? _draw;

    // Custom-shader draw (separate channel from the Canvas _draw callback).
    internal ShaderEffect? _effect;

    // Declared layout-move transition; null = snap (default). Clearing also drops any in-flight glide.
    LayoutTransition? _layoutTransition;
    GlideState _glide;
    Vector2 _lastLayoutPos;
    bool _hasLayoutPos;

    internal void SetLayoutTransition(LayoutTransition? transition)
    {
        _layoutTransition = transition;
        if (transition is null) _glide = default;
    }

    public void ApplyStateVariants(
        Color? baseBg,  Color? baseFg,
        Color? hoverBg, Color? activeBg, Color? focusBg,
        Color? hoverFg, Color? activeFg, Color? focusFg,
        int? transitionMs)
    {
        _state ??= new StateController(this);
        _state.ApplyVariants(
            baseBg, baseFg,
            hoverBg, activeBg, focusBg,
            hoverFg, activeFg, focusFg,
            transitionMs);
    }

    public void ClearStateVariants() => _state?.ClearVariants();

    public bool HasActiveStateVariants => _state?.HasActiveVariants ?? false;

    public void ApplyEvents(in BlobEvents events)
    {
        _onClick      = events.OnClick;
        _onRightClick = events.OnRightClick;
        _onMiddleClick = events.OnMiddleClick;
        _onMouseEnter = events.OnMouseEnter;
        _onMouseLeave = events.OnMouseLeave;
        _onMouseDown  = events.OnMouseDown;
        _onMouseUp    = events.OnMouseUp;
        _onMouseMove  = events.OnMouseMove;
        _onMouseWheel = events.OnMouseWheel;
        _onDragStart = events.OnDragStart;
        _onDrag      = events.OnDrag;
        _onDragEnd   = events.OnDragEnd;
        _onDragEnter = events.OnDragEnter;
        _onDragLeave = events.OnDragLeave;
        _onDrop      = events.OnDrop;
        _wantsDrag   = events.WantsDrag;
    }

    public bool HasEventHandlers =>
        _onClick != null || _onRightClick != null || _onMiddleClick != null || _onMouseEnter != null || _onMouseLeave != null ||
        _onMouseDown != null || _onMouseUp != null || _onMouseMove != null || _onMouseWheel != null ||
        _onDragStart != null || _onDrag != null || _onDragEnd != null ||
        _onDragEnter != null || _onDragLeave != null || _onDrop != null;

    public bool UserSetPointerEvents
    {
        get => _userSetPointerEvents;
        set => _userSetPointerEvents = value;
    }

    // Immediate-mode custom draw, run inside the engine's batched OnDraw pass via the Goo Canvas facade.
    public override void OnDraw()
    {
        if (_draw is null) return;
        _draw(new Canvas(Box.Rect));
    }

    // IPanelDraw custom-shader paint, interleaved into the engine batched UI render (post-#4692).
    void IPanelDraw.Draw(CommandList cl)
    {
        if (_effect is null) return;
        // BoxSize (set in Apply from Box.Rect) is PHYSICAL px; this is the physical-per-CSS DPI scale
        // so a shader can convert any CSS-px length L to physical via L*DpiScale and stay crisp at any DPI.
        cl.Attributes.Set("DpiScale", ScaleToScreen);
        _effect.Apply(cl, Box.Rect);
        cl.DrawQuad(Box.Rect, _effect.Material, Color.White);
    }

    // Runs inside FinalLayout after yoga assigns the rect; banking here sees real positions the same frame.
    public override void OnLayout(ref Rect rect)
    {
        base.OnLayout(ref rect);
        if (_layoutTransition is { } t && _hasLayoutPos)
        {
            var jump = _lastLayoutPos - rect.Position;
            if (jump.LengthSquared > 0.01f)
            {
                if (!_glide.IsActive) _glide = new GlideState(t.Ms, t.Easing);
                _glide.Bank(jump);
            }
        }
        _lastLayoutPos = rect.Position;
        _hasLayoutPos = true;
        rect.Position += _glide.Offset;
    }

    // Re-dirty each frame so either draw channel updates continuously; a live glide also re-runs layout.
    public override void Tick()
    {
        base.Tick();
        if (_glide.IsActive)
        {
            _glide.Advance(Time.Delta);
            Style.Dirty();
            MarkRenderDirty();
        }
        if (_draw is not null || _effect is not null)
            MarkRenderDirty();
    }

    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);
        EventDispatch.Fire(_onClick, e, _requestRebuild);
    }

    protected override void OnRightClick(MousePanelEvent e)
    {
        base.OnRightClick(e);
        EventDispatch.Fire(_onRightClick, e, _requestRebuild);
    }

    protected override void OnMiddleClick(MousePanelEvent e)
    {
        base.OnMiddleClick(e);
        EventDispatch.Fire(_onMiddleClick, e, _requestRebuild);
    }

    protected override void OnMouseOver(MousePanelEvent e)
    {
        base.OnMouseOver(e);
        EventDispatch.FireEnter(ref _hoverInside, _onMouseEnter, e, _requestRebuild);
    }

    protected override void OnMouseOut(MousePanelEvent e)
    {
        base.OnMouseOut(e);
        EventDispatch.FireLeave(this, ref _hoverInside, _onMouseLeave, e, _requestRebuild);
    }

    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        EventDispatch.Fire(_onMouseDown, e, _requestRebuild);
    }

    protected override void OnMouseUp(MousePanelEvent e)
    {
        base.OnMouseUp(e);
        EventDispatch.Fire(_onMouseUp, e, _requestRebuild);
    }

    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        EventDispatch.Fire(_onMouseMove, e, _requestRebuild);
    }

    // The engine calls this on the deepest hovered panel and, by default, bubbles to the parent
    // when this panel cannot scroll (Panel.OnMouseWheel -> TryScroll -> Parent). A user handler
    // consumes the wheel (no base call) so it does not leak into an ancestor scroll container;
    // with no handler we defer to base to preserve default scrolling/bubbling.
    public override void OnMouseWheel(Vector2 value)
    {
        if (_onMouseWheel is not null)
        {
            EventDispatch.Fire(_onMouseWheel, value, _requestRebuild);
            return;
        }
        base.OnMouseWheel(value);
    }

    public override bool WantsDrag => _wantsDrag;

    // Route every drag event through EventDispatch.Fire so the AutoRebuildOnEvents contract holds:
    // a handler that mutates state to reposition/resize gets an automatic rebuild, same as OnClick et al.
    protected override void OnDragStart( DragEvent e ) => EventDispatch.Fire( _onDragStart, e, _requestRebuild );
    protected override void OnDrag( DragEvent e ) => EventDispatch.Fire( _onDrag, e, _requestRebuild );
    protected override void OnDragEnd( DragEvent e ) => EventDispatch.Fire( _onDragEnd, e, _requestRebuild );

    protected override void OnDragEnter( PanelEvent e )
    {
        base.OnDragEnter( e );
        EventDispatch.Fire( _onDragEnter, e, _requestRebuild );
    }

    protected override void OnDragLeave( PanelEvent e )
    {
        base.OnDragLeave( e );
        EventDispatch.Fire( _onDragLeave, e, _requestRebuild );
    }

    protected override void OnDrop( PanelEvent e )
    {
        base.OnDrop( e );
        EventDispatch.Fire( _onDrop, e, _requestRebuild );
    }
}
