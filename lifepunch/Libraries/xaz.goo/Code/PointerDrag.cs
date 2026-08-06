using Sandbox;

namespace Goo;

// Frame-polled pointer drag for move/resize handles. Bypasses native ondrag, which starves
// when the cursor leaves a hittable panel (engine-fact-native-drag-editor-viewport). The owner
// holds an instance, begins it on the handle OnMouseDown, ends it on OnMouseUp, and reads the
// live value each frame in Tick. The owner stays the single writer of the dragged value.
// Drive something that follows the cursor (a moving card, a growing corner grip) so OnMouseUp
// stays deliverable; call End() on teardown as a backstop.
public sealed class PointerDrag
{
    Vector2 _startCursor;
    Vector2 _startValue;
    public bool Active { get; private set; }

    public void Begin( Vector2 startValue ) => Begin( startValue, Mouse.Position );
    public void End() => Active = false;
    public Vector2 Current( float scale ) => Current( Mouse.Position, scale );
    // Null-safe ScaleFromScreen lookup; pass the owning panel (may be null pre-mount).
    public Vector2 Current( Sandbox.UI.Panel? panel ) => Current( Mouse.Position, panel );

    internal void Begin( Vector2 startValue, Vector2 cursor )
    {
        _startValue = startValue; _startCursor = cursor; Active = true;
    }

    internal Vector2 Current( Vector2 cursor, Sandbox.UI.Panel? panel )
        => Current( cursor, panel?.ScaleFromScreen ?? 1f );

    internal Vector2 Current( Vector2 cursor, float scale )
        => _startValue + ( cursor - _startCursor ) * scale;
}
