using System;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// Fiber-backed virtual grid: the Tier-2 recycle machinery of <see cref="GooVirtualPanel"/> with
/// the engine VirtualGrid's column-flow math (cells stretch to a flush right edge, preserving
/// aspect). The grid-mode default behind <c>Virtual.Grid</c>.
/// </summary>
public sealed class GooVirtualGrid : GooVirtualPanel
{
    float _itemWidth = 100f;
    float _itemHeight = 100f;

    public Vector2 ItemSize
    {
        get => new(_itemWidth, _itemHeight);
        set
        {
            _itemWidth = MathF.Max(1f, value.x);
            _itemHeight = MathF.Max(1f, value.y);
        }
    }

    // Grid layout math, redone here: the engine's GridLayout is internal to Sandbox.UI.Layout.
    // Mirrors its ScaleUp=true behavior (stretch cell width flush, height keeps aspect).
    Rect _inner;
    Rect _outer;
    float _scroll;
    Vector2 _spacing;
    Vector2 _cellSize;
    int _columns = 1;
    int _layoutHash;

    protected override void UpdateLayoutSpacing(Vector2 spacing) => _spacing = spacing;

    protected override bool UpdateLayout()
    {
        var hash = HashCode.Combine(Box.RectInner, ScaleFromScreen, ScrollOffset.y * ScaleFromScreen, _itemWidth, _itemHeight, _spacing);
        if (hash == _layoutHash) return false;
        _layoutHash = hash;

        _cellSize = new Vector2(_itemWidth, _itemHeight);

        var inner = Box.RectInner;
        inner.Position = Box.RectInner.Position - Box.Rect.Position;
        _inner = inner * ScaleFromScreen;
        _outer = Box.Rect * ScaleFromScreen;
        _scroll = ScrollOffset.y * ScaleFromScreen;

        float stepX = _cellSize.x + _spacing.x;
        _columns = stepX > 0f ? ((_inner.Width + _spacing.x) / stepX).FloorToInt() : 1;
        if (_columns < 1) _columns = 1;

        // Stretch X to fill; preserve aspect (Y scales with X).
        float totalSpacing = (_columns - 1) * _spacing.x;
        _cellSize.x = (_inner.Width - totalSpacing) / _columns;
        float aspect = _itemHeight / _itemWidth;
        _cellSize.y = MathF.Max(1f, _cellSize.x * aspect);

        return true;
    }

    protected override void GetVisibleRange(out int first, out int pastEnd)
    {
        float rowStep = MathF.Max(1f, _cellSize.y + _spacing.y);
        int topRow = ((_scroll - _inner.Top) / rowStep).FloorToInt();
        if (topRow < 0) topRow = 0;
        int rowsFit = (_outer.Height / rowStep).CeilToInt() + 1;
        first = Math.Max(0, topRow * _columns);
        pastEnd = first + rowsFit * _columns;
    }

    protected override void PositionPanel(int index, Panel panel)
    {
        int col = index % _columns;
        int row = index / _columns;
        float stepX = _cellSize.x + _spacing.x;
        float stepY = _cellSize.y + _spacing.y;
        panel.Style.Left = _inner.Left + col * stepX;
        panel.Style.Top = _inner.Top + row * stepY;
        panel.Style.Width = _cellSize.x;
        panel.Style.Height = _cellSize.y;
        panel.Style.Dirty();
    }

    protected override float GetTotalHeight(int itemCount)
    {
        float rowStep = _cellSize.y + _spacing.y;
        float paddingY = _outer.Height - _inner.Height;
        if (rowStep <= 0f) return MathF.Max(0f, paddingY);
        float rows = MathF.Ceiling(itemCount / (float)_columns);
        return rows * rowStep + MathF.Max(0f, paddingY);
    }
}
