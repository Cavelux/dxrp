using System;
using Sandbox;
using Sandbox.UI;

namespace Goo;

// Cell: a draggable carrying a typed payload. Owns its own being-dragged visual flag so the
// original can dim while in flight. Wires engine drag events into the shared DragContext.
public sealed class DragSource<T> : Cell<Container>
{
    public DragContext<T> Context = null!;     // set via Cell.Mount configure
    public T Payload = default!;               // set via configure
    public Func<bool, Container> Content = null!;  // receives isDragging; renders the tile
    public Func<Container>? Ghost;             // optional ghost; defaults to Content(false)

    bool _dragging;

    protected override Container Build() => new Container
    {
        PointerEvents = PointerEvents.All,
        OnDragStart = e =>
        {
            _dragging = true;
            // ScreenPosition is rendered px; the ghost layer consumes Context.Pos as CSS-px Left/Top.
            // Convert via the source panel's ScaleFromScreen (=1/ScaleToScreen) so the ghost tracks
            // the cursor under UI scaling (engine-fact-panel-screen-position-helpers). No-op at 1.0.
            Context.Begin(Payload, Ghost ?? (() => Content(false)),
                e.ScreenPosition * (e.This?.ScaleFromScreen ?? 1f));
            Rebuild();
        },
        OnDrag = e =>
        {
            Context.Move(e.ScreenPosition * (e.This?.ScaleFromScreen ?? 1f));
            Rebuild();
        },
        OnDragEnd = _ =>
        {
            _dragging = false;
            Context.Release();
            Rebuild();
        },
        Children = { Content(_dragging) },
    };
}
