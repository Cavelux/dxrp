using System;
using Sandbox;
using Sandbox.UI;

namespace Goo;

// Where within a drop zone an interaction landed, in the zone's own rendered frame (root-scaled px, not style px).
// Positional consumers divide by a pitch derived from ZoneSize, e.g. cellX = (int)(Local.x / (ZoneSize.x / Cols)).
// Never divide Local by a style constant: rendered px cancel in the ratio, layout px do not.
public readonly record struct DropLocation(Vector2 Local, Vector2 ZoneSize);

// A drop target for a typed payload; stops propagation so the innermost zone wins. See engine-fact-sbox-ui-drop-dispatch.
public sealed class DropZone<T> : Cell<Container>
{
    public DragContext<T> Context = null!;          // set via configure
    public Func<bool, Container> Content = null!;   // receives isHovered; renders the zone
    public Action<T, DropLocation>? OnDropPayload;  // consumer commit
    public Action<DropLocation?>? OnHover;          // location on enter (each cell crossing), null on leave

    bool _hovered;

    protected override Container Build() => new Container
    {
        PointerEvents = PointerEvents.All,
        OnDragEnter = e =>
        {
            _hovered = true;
            OnHover?.Invoke( Loc( e ) );
            Rebuild();
            e.StopPropagation();
        },
        OnDragLeave = e =>
        {
            // Co-located child targets bubble a 'leave' here; treat it as real only when the cursor is outside our rect. See engine-fact-sbox-ui-drop-dispatch.
            if ( e.This is { } stay && Within( stay ) )
            {
                e.StopPropagation();
                return;
            }
            _hovered = false;
            OnHover?.Invoke( null );
            Rebuild();
            e.StopPropagation();
        },
        OnDrop = e =>
        {
            _hovered = false;
            if ( Context.HasPayload )
                OnDropPayload?.Invoke( Context.Payload, Loc( e ) );
            OnHover?.Invoke( null );
            Context.Complete();
            Rebuild();
            e.StopPropagation();
        },
        Children = { Content( _hovered ) },
    };

    // e.This is the panel currently handling the event during bubble propagation = this zone.
    static DropLocation Loc( PanelEvent e )
    {
        var zone = e.This;
        if ( zone is null ) return default;
        return new DropLocation( zone.MousePosition, zone.Box.Rect.Size );
    }

    // True when the cursor still sits inside the zone's own rendered rect (a child-to-child move),
    // as opposed to having actually exited the zone. MousePosition is relative to the zone's
    // top-left in the same rendered frame as Box.Rect (engine-fact-sbox-ui-drop-dispatch).
    static bool Within( Panel zone )
    {
        var mp = zone.MousePosition;
        var size = zone.Box.Rect.Size;
        return mp.x >= 0f && mp.y >= 0f && mp.x <= size.x && mp.y <= size.y;
    }
}
