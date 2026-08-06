using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// Shared Tier-2 recycle machinery for fiber-backed virtual panels: each visible row keeps a
/// persistent Goo fiber and rediffs on item change instead of BaseVirtualPanel's delete+recreate,
/// and scrolled-out row panels pool for recycling. Subclasses supply only the layout math
/// (BaseVirtualPanel's abstract hooks). Stateful nodes inside rows (e.g. TextEntry editors)
/// should carry a per-item Key so a recycled panel never inherits another item's private
/// engine state.
/// </summary>
public abstract class GooVirtualPanel : BaseVirtualPanel
{
    internal RowBuilder? Row;
    internal Action? RootRebuild;

    sealed class RowState
    {
        public Panel Panel = null!;
        public Fiber? Fiber;
        public object? Data = Unset;
    }

    static readonly object Unset = new();
    readonly Dictionary<int, RowState> _rows = new();
    readonly Stack<RowState> _pool = new();

    // Base Tick's private RefreshCreated does delete+recreate; replace the whole loop (Panel.Tick
    // is empty, and base's private CheckSourceForChanges is covered by Goo's count capture).
    public override void Tick()
    {
        if (!Perf.Enabled) { TickCore(); return; }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        TickCore();
        Perf.RecordPhase(PerfPhase.VirtualTick, System.Diagnostics.Stopwatch.GetTimestamp() - t0);
    }

    void TickCore()
    {
        if (ComputedStyle is null || !IsVisible) return;

        UpdateLayoutSpacing(new(
            ComputedStyle.ColumnGap?.Value ?? 0,
            ComputedStyle.RowGap?.Value ?? 0
        ));

        if (!UpdateLayout() && !NeedsRebuild) return;
        NeedsRebuild = false;

        GetVisibleRange(out var first, out var pastEnd);
        Evict(first, pastEnd - 1);
        for (int i = first; i < pastEnd; i++)
            RefreshRow(i);
    }

    // Pool instead of delete: the fiber rides along so a recycled row rediffs into its next item.
    void Evict(int minInclusive, int maxInclusive)
    {
        _removals.Clear();
        foreach (var idx in _rows.Keys)
            if (idx < minInclusive || idx > maxInclusive || !HasData(idx))
                _removals.Add(idx);

        foreach (var idx in _removals)
        {
            if (!_rows.Remove(idx, out var row)) continue;
            row.Panel.Style.Display = DisplayMode.None;
            row.Panel.Style.Dirty();
            _pool.Push(row);
        }
        _removals.Clear();
    }

    void RefreshRow(int i)
    {
        if (!HasData(i)) return;
        var data = _items[i];

        if (!_rows.TryGetValue(i, out var row))
        {
            if (_pool.TryPop(out row))
            {
                row.Panel.Style.Display = DisplayMode.Flex;
                row.Panel.Style.Dirty();
            }
            else
            {
                row = new RowState { Panel = Add.Panel("cell") };
                row.Panel.Style.Position = PositionMode.Absolute;
            }
            _rows[i] = row;
        }

        // Rediff only when the item changed; an unchanged (or recycled-back) item costs nothing.
        if (!EqualityComparer<object>.Default.Equals(row.Data, data))
        {
            row.Data = data;
            if (Row is not null)
                RowMounter.DiffInto(row.Panel, ref row.Fiber, Row, data, RootRebuild);
        }

        if (i == _items.Count - 1 && !_lastCellCreated)
        {
            _lastCellCreated = true;
            OnLastCell?.Invoke();
        }

        PositionPanel(i, row.Panel);
    }

    protected override void FinalLayoutChildren(Vector2 offset)
    {
        foreach (var kv in _rows)
            kv.Value.Panel.FinalLayout(offset);

        var rect = Box.Rect;
        rect.Position -= ScrollOffset;
        rect.Height = MathF.Max(GetTotalHeight(_items.Count) * ScaleToScreen, rect.Height);
        ConstrainScrolling(rect.Size);
    }

    public override void OnDeleted()
    {
        base.OnDeleted();
        foreach (var kv in _rows) Reconciler.TeardownFiberTree(kv.Value.Fiber);
        while (_pool.TryPop(out var pooled)) Reconciler.TeardownFiberTree(pooled.Fiber);
        _rows.Clear();
    }
}
