using System;
using Sandbox;
using Sandbox.UI;

namespace Goo.Internal;

internal sealed class StatefulWebPanel : Sandbox.UI.WebPanel, IStatefulHost, IStatefulEventHost
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
    internal bool    _userSetPointerEvents;
    internal Action? _requestRebuild;
    public Action? RequestRebuild { set => _requestRebuild = value; }

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
    }

    public bool HasEventHandlers =>
        _onClick != null || _onRightClick != null || _onMiddleClick != null || _onMouseEnter != null || _onMouseLeave != null ||
        _onMouseDown != null || _onMouseUp != null || _onMouseMove != null;

    public bool UserSetPointerEvents
    {
        get => _userSetPointerEvents;
        set => _userSetPointerEvents = value;
    }

    // (Engine #11055 corrected the WebPanel/Steam-HTML mousewheel sign; the former OnMouseWheel
    //  override is gone - keeping it would now double-invert. See bd engine-fact-webpanel-scroll-inverted.)

    protected override void OnClick(MousePanelEvent e)       { base.OnClick(e);       EventDispatch.Fire(_onClick, e, _requestRebuild); }
    protected override void OnRightClick(MousePanelEvent e)  { base.OnRightClick(e);  EventDispatch.Fire(_onRightClick, e, _requestRebuild); }
    protected override void OnMiddleClick(MousePanelEvent e) { base.OnMiddleClick(e); EventDispatch.Fire(_onMiddleClick, e, _requestRebuild); }
    protected override void OnMouseOver(MousePanelEvent e)   { base.OnMouseOver(e);   EventDispatch.FireEnter(ref _hoverInside, _onMouseEnter, e, _requestRebuild); }
    protected override void OnMouseOut(MousePanelEvent e)    { base.OnMouseOut(e);    EventDispatch.FireLeave(this, ref _hoverInside, _onMouseLeave, e, _requestRebuild); }
    protected override void OnMouseDown(MousePanelEvent e)   { base.OnMouseDown(e);   EventDispatch.Fire(_onMouseDown, e, _requestRebuild); }
    protected override void OnMouseUp(MousePanelEvent e)     { base.OnMouseUp(e);     EventDispatch.Fire(_onMouseUp, e, _requestRebuild); }
    protected override void OnMouseMove(MousePanelEvent e)   { base.OnMouseMove(e);   EventDispatch.Fire(_onMouseMove, e, _requestRebuild); }
}
