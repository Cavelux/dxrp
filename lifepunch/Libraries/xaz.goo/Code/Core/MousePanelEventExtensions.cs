using Sandbox.UI;

namespace Goo;

// Cursor coordinate conversion for MousePanelEvent.
// LocalPosition is relative to e.Target, not your positioning ancestor.
public static class MousePanelEventExtensions
{
    /// <summary>Translates cursor position from e.Target's frame into ancestor's frame.</summary>
    public static Vector2 PositionIn(this MousePanelEvent e, Panel ancestor)
    {
        if (e.Target == null || ancestor == null) return e.LocalPosition;
        return PositionInCore(e.Target.Box.Rect.Position, e.LocalPosition, ancestor.Box.Rect.Position);
    }

    internal static Vector2 PositionInCore(Vector2 targetScreenPos, Vector2 localPos, Vector2 ancestorScreenPos)
        => targetScreenPos + localPos - ancestorScreenPos;
}
