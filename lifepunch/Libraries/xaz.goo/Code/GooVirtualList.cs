using System;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// Fiber-backed virtual list: the Tier-2 recycle machinery of <see cref="GooVirtualPanel"/> with
/// fixed-height vertical rows. The list-mode default behind <c>Virtual.List</c>; opt in explicitly
/// via <c>Virtual.Custom(items, () => new GooVirtualList { ItemHeight = h }, row)</c>.
/// </summary>
public sealed class GooVirtualList : GooVirtualPanel
{
    float _itemHeight = 24f;
    public float ItemHeight { get => _itemHeight; set => _itemHeight = MathF.Max(1f, value); }

    // Vertical fixed-height layout math, redone here: the engine's VerticalListLayout is
    // internal to Sandbox.UI.
    Rect _inner;
    Rect _outer;
    float _scroll;
    Vector2 _spacing;
    Vector2 _rowSize;
    int _layoutHash;

    protected override void UpdateLayoutSpacing(Vector2 spacing) => _spacing = spacing;

    protected override bool UpdateLayout()
    {
        var hash = HashCode.Combine(Box.RectInner, ScaleFromScreen, ScrollOffset.y * ScaleFromScreen, _itemHeight, _spacing);
        if (hash == _layoutHash) return false;
        _layoutHash = hash;

        var inner = Box.RectInner;
        inner.Position = Box.RectInner.Position - Box.Rect.Position;
        _inner = inner * ScaleFromScreen;
        _outer = Box.Rect * ScaleFromScreen;
        _scroll = ScrollOffset.y * ScaleFromScreen;
        _rowSize = new Vector2(MathF.Max(1f, _inner.Width), _itemHeight);
        return true;
    }

    protected override void GetVisibleRange(out int first, out int pastEnd)
    {
        float step = MathF.Max(1f, _rowSize.y + _spacing.y);
        int top = ((_scroll - _inner.Top) / step).FloorToInt();
        if (top < 0) top = 0;
        int fit = (_outer.Height / step).CeilToInt() + 1;
        first = top;
        pastEnd = top + fit;
    }

    protected override void PositionPanel(int index, Panel panel)
    {
        float step = _rowSize.y + _spacing.y;
        panel.Style.Left = _inner.Left;
        panel.Style.Top = _inner.Top + index * step;
        panel.Style.Width = _rowSize.x;
        panel.Style.Height = _rowSize.y;
        panel.Style.Dirty();
    }

    protected override float GetTotalHeight(int itemCount)
    {
        float step = _rowSize.y + _spacing.y;
        float paddingY = _outer.Height - _inner.Height;
        if (step <= 0f) return MathF.Max(0f, paddingY);
        return itemCount * step + MathF.Max(0f, paddingY);
    }
}
