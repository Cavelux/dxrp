using System;
using Sandbox;
using Sandbox.UI;

namespace Goo.Internal;

internal sealed class StatefulTextEntry : Sandbox.UI.TextEntry, IStatefulHost, IStatefulEventHost
{
    StateController? _state;

    internal Action<string>? _onChange;
    internal Action<string>? _onSubmit;
    internal Action? _onFocus;
    internal Action<string>? _onBlur;
    internal Action? _onCancel;

    internal Func<char, bool>?   _canEnterChar;
    internal Func<string, bool>? _validate;
    internal Action<bool>?       _onValidationChanged;
    bool _lastInvalid;

    internal Action<MousePanelEvent>? _onClick;
    internal Action<MousePanelEvent>? _onRightClick;
    internal Action<MousePanelEvent>? _onMiddleClick;
    internal Action<MousePanelEvent>? _onMouseEnter;
    internal Action<MousePanelEvent>? _onMouseLeave;
    bool _hoverInside;   // gates true enter/leave over the engine's bubbling over/out
    internal Action<MousePanelEvent>? _onMouseDown;
    internal Action<MousePanelEvent>? _onMouseUp;
    internal Action<MousePanelEvent>? _onMouseMove;
    internal bool _userSetPointerEvents;
    internal Action? _requestRebuild;
    public Action? RequestRebuild { set => _requestRebuild = value; }

    public StatefulTextEntry()
    {
        // Wire the engine's OnTextEdited through _onChange for per-keystroke notifications.
        OnTextEdited = newValue => { _onChange?.Invoke(newValue); if (_onChange != null) _requestRebuild?.Invoke(); };
    }

    // Without IStatefulHost the Applier silently discarded the variant sheet for TextEntry,
    // and SplitBaseColors had already moved the declared base FontColor/BackgroundColor into
    // it, so any Hover/Active/Focus color turned the typed text engine-default black.
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

    public void ClearStateVariants()
    {
        _state?.ClearVariants();
        // TextEntry is intrinsically focusable (the engine ctor sets AcceptsFocus = true).
        // ClearVariants resets AcceptsFocus for plain panels; restore it or a variant-free
        // entry stops receiving focus after its first rebuild and typing breaks.
        AcceptsFocus = true;
    }

    public bool HasActiveStateVariants => _state?.HasActiveVariants ?? false;

    // AND-compose the Goo predicate after the engine's rules (CharacterRegex / Numeric / Multiline).
    public override bool CanEnterCharacter(char c)
        => base.CanEnterCharacter(c) && (_canEnterChar?.Invoke(c) ?? true);

    // Engine runs UpdateValidation() + OnTextEdited() here; merge the Goo Validate predicate and fire OnValidationChanged on a flip.
    public override void OnValueChanged()
    {
        base.OnValueChanged();
        ApplyPredicateAndNotify();
    }

    // Tighten HasValidationErrors with the Goo predicate, then fire OnValidationChanged (and rebuild) only on a validity transition.
    internal void ApplyPredicateAndNotify()
    {
        if (_validate != null && !_validate(Text ?? string.Empty))
        {
            HasValidationErrors = true;
            SetClass("invalid", true);
        }

        if (HasValidationErrors != _lastInvalid)
        {
            _lastInvalid = HasValidationErrors;
            _onValidationChanged?.Invoke(HasValidationErrors);
            if (_onValidationChanged != null) _requestRebuild?.Invoke();
        }
    }

    // Recompute validity (engine rules + predicate) without an edit event; the Applier calls this after props change.
    internal void RecomputeValidation()
    {
        UpdateValidation();
        ApplyPredicateAndNotify();
    }

    // Engine fires "onsubmit" itself on Enter (no Submit method); hook OnEvent to react.
    protected override void OnEvent(PanelEvent e)
    {
        base.OnEvent(e);
        if (e.Name == "onsubmit") { _onSubmit?.Invoke(Text ?? string.Empty); if (_onSubmit != null) _requestRebuild?.Invoke(); }
        // Escape fires "oncancel" via the engine's Cancel(); value-less, same path as onsubmit.
        if (e.Name == "oncancel") { _onCancel?.Invoke(); if (_onCancel != null) _requestRebuild?.Invoke(); }
    }

    // Call base first so the engine's focus/blur work runs before we observe the committed Text.
    protected override void OnFocus(PanelEvent e)
    {
        base.OnFocus(e);
        if (_onFocus != null) { _onFocus.Invoke(); _requestRebuild?.Invoke(); }
    }

    protected override void OnBlur(PanelEvent e)
    {
        base.OnBlur(e);
        if (_onBlur != null) { _onBlur.Invoke(Text ?? string.Empty); _requestRebuild?.Invoke(); }
    }

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
        _onMouseDown != null || _onMouseUp != null || _onMouseMove != null ||
        _onChange != null || _onSubmit != null ||
        _onFocus != null || _onBlur != null || _onCancel != null;

    public bool UserSetPointerEvents
    {
        get => _userSetPointerEvents;
        set => _userSetPointerEvents = value;
    }

    protected override void OnClick(MousePanelEvent e)       { base.OnClick(e);       EventDispatch.Fire(_onClick, e, _requestRebuild); }
    protected override void OnRightClick(MousePanelEvent e)  { base.OnRightClick(e);  EventDispatch.Fire(_onRightClick, e, _requestRebuild); }
    protected override void OnMiddleClick(MousePanelEvent e) { base.OnMiddleClick(e); EventDispatch.Fire(_onMiddleClick, e, _requestRebuild); }
    protected override void OnMouseOver(MousePanelEvent e)   { base.OnMouseOver(e);   EventDispatch.FireEnter(ref _hoverInside, _onMouseEnter, e, _requestRebuild); }
    protected override void OnMouseOut(MousePanelEvent e)    { base.OnMouseOut(e);    EventDispatch.FireLeave(this, ref _hoverInside, _onMouseLeave, e, _requestRebuild); }
    protected override void OnMouseDown(MousePanelEvent e)   { base.OnMouseDown(e);   EventDispatch.Fire(_onMouseDown, e, _requestRebuild); }
    protected override void OnMouseUp(MousePanelEvent e)     { base.OnMouseUp(e);     EventDispatch.Fire(_onMouseUp, e, _requestRebuild); }
    protected override void OnMouseMove(MousePanelEvent e)   { base.OnMouseMove(e);   EventDispatch.Fire(_onMouseMove, e, _requestRebuild); }
}
