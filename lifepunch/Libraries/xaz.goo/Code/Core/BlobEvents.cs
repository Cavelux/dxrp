using System;
using Sandbox.UI;

namespace Goo;

internal struct BlobEvents
{
    public Action<MousePanelEvent>? OnClick;
    public Action<MousePanelEvent>? OnRightClick;
    public Action<MousePanelEvent>? OnMiddleClick;
    public Action<MousePanelEvent>? OnMouseEnter;
    public Action<MousePanelEvent>? OnMouseLeave;
    public Action<MousePanelEvent>? OnMouseDown;
    public Action<MousePanelEvent>? OnMouseUp;
    public Action<MousePanelEvent>? OnMouseMove;
    public Action<Vector2>?         OnMouseWheel;

    public Action<DragEvent>?  OnDragStart;
    public Action<DragEvent>?  OnDrag;
    public Action<DragEvent>?  OnDragEnd;
    public Action<PanelEvent>? OnDragEnter;
    public Action<PanelEvent>? OnDragLeave;
    public Action<PanelEvent>? OnDrop;

    public bool AnyNonNull => OnClick != null
        || OnRightClick != null
        || OnMiddleClick != null
        || OnMouseEnter != null
        || OnMouseLeave != null
        || OnMouseDown != null
        || OnMouseUp != null
        || OnMouseMove != null
        || OnMouseWheel != null
        || OnDragStart != null
        || OnDrag != null
        || OnDragEnd != null
        || OnDragEnter != null
        || OnDragLeave != null
        || OnDrop != null;

    public bool WantsDrag => OnDragStart != null || OnDrag != null || OnDragEnd != null;

    public static bool ContentsEqual(in BlobEvents a, in BlobEvents b)
        => Equals(a.OnClick,       b.OnClick)
        && Equals(a.OnRightClick,  b.OnRightClick)
        && Equals(a.OnMiddleClick, b.OnMiddleClick)
        && Equals(a.OnMouseEnter,  b.OnMouseEnter)
        && Equals(a.OnMouseLeave,  b.OnMouseLeave)
        && Equals(a.OnMouseDown,   b.OnMouseDown)
        && Equals(a.OnMouseUp,     b.OnMouseUp)
        && Equals(a.OnMouseMove,   b.OnMouseMove)
        && Equals(a.OnMouseWheel,  b.OnMouseWheel)
        && Equals(a.OnDragStart,   b.OnDragStart)
        && Equals(a.OnDrag,        b.OnDrag)
        && Equals(a.OnDragEnd,     b.OnDragEnd)
        && Equals(a.OnDragEnter,   b.OnDragEnter)
        && Equals(a.OnDragLeave,   b.OnDragLeave)
        && Equals(a.OnDrop,        b.OnDrop);
}
