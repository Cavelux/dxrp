using Sandbox.UI;

namespace Goo;

// Renders the active drag's ghost at the cursor; an empty Container when no drag is in flight, so it costs nothing. Its own cell positions it for subtree-only re-diff later.
public sealed class DragLayer<T> : Cell<Container>
{
    public DragContext<T> Context = null!;     // set via configure

    protected override Container Build()
    {
        if (!Context.IsDragging || Context.Ghost is null)
            return new Container();

        return new Container
        {
            Position = PositionMode.Absolute,
            Left = Context.Pos.x,
            Top = Context.Pos.y,
            PointerEvents = PointerEvents.None,
            // Keyed wrapper so the idle(0)->dragging(1) child-count change diffs as a keyed insert,
            // not an all-unkeyed positional list whose length changed (Reconciler length warning).
            Children = { new Container { Key = "ghost", Children = { Context.Ghost() } } },
        };
    }
}
