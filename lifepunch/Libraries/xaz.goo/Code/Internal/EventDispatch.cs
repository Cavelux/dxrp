using System;
using Sandbox;
using Sandbox.UI;

namespace Goo.Internal;

/// <summary>Invokes a user event handler and, if it ran, requests a rebuild.
/// Centralizes the "auto-rebuild after a handler fires" rule (the GooPanel
/// AutoRebuildOnEvents contract) for every event-host panel. Generic over the
/// payload so mouse (MousePanelEvent), wheel (Vector2), and drag (DragEvent /
/// PanelEvent) handlers all route through the same path.</summary>
internal static class EventDispatch
{
    public static void Fire<T>(Action<T>? handler, T e, Action? requestRebuild)
    {
        if (handler == null) return;
        handler.Invoke(e);
        requestRebuild?.Invoke();
    }

    public static void FireEnter(ref bool inside, Action<MousePanelEvent>? handler, MousePanelEvent e, Action? requestRebuild)
    {
        if (inside) return;
        inside = true;
        Fire(handler, e, requestRebuild);
    }

    public static void FireLeave(Panel self, ref bool inside, Action<MousePanelEvent>? handler, MousePanelEvent e, Action? requestRebuild)
    {
        if (!inside) return;
        if (self.IsInside(Mouse.Position)) return;
        inside = false;
        Fire(handler, e, requestRebuild);
    }

    // Fires enter/leave on an outline-crossing transition. The caller computes nowInside against the
    // drawn outline (not the rect), so this drives precise enter/leave from OnMouseMove/OnMouseOut.
    public static void FireCrossing(ref bool inside, bool nowInside,
        Action<MousePanelEvent>? onEnter, Action<MousePanelEvent>? onLeave,
        MousePanelEvent e, Action? requestRebuild)
    {
        if (nowInside == inside) return;
        inside = nowInside;
        Fire(nowInside ? onEnter : onLeave, e, requestRebuild);
    }
}
