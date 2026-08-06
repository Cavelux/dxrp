#nullable enable
using System;
using System.Diagnostics;
using System.Text;

namespace Goo;

internal enum PerfPhase
{
    PanelEnable,
    PanelTick,
    PanelMountBuild,
    PanelMountDiff,
    PanelMountApply,
    PanelRebuildBuild,
    PanelRebuildDiff,
    PanelRebuildApply,
    PanelReturnAll,
    PanelDisableTeardown,
    ViewTick,
    ViewBuild,
    ViewDiff,
    ViewApply,
    ViewReturnAll,
    ViewDeleteTeardown,
    VirtualTick,      // GooVirtualList per-frame engine Tick (layout + evict + row refresh)
    VirtualRowDiff    // RowMounter.DiffInto: one row's build+diff+apply
}

internal enum PerfEvent
{
    PanelIdleFrame,
    PanelMovingFrame,
    PanelEventRebuild,
    PanelRemount
}

/// <summary>Opt-in profiler for the live pipeline. Set <see cref="Enabled"/> = true, exercise the UI, then read <see cref="Report"/>. Headless benches cover Build and Diff time and allocation in isolation; the engine Apply path only runs in-game (Panel needs the native layout runtime), so this accumulates per-phase wall time and per-op-kind costs across rebuilds until <see cref="Reset"/>. Allocation tracking is headless-only because sandboxed code cannot access managed allocation counters.</summary>
public static class Perf
{
    /// <summary>Master switch. Off (the default) costs one branch per rebuild and per op.</summary>
    public static bool Enabled;

    static readonly int OpKindCount = (int)OpKind.SetLayoutTransition + 1;
    static readonly int PhaseCount = (int)PerfPhase.VirtualRowDiff + 1;
    static readonly int EventCount = (int)PerfEvent.PanelRemount + 1;

    static long _rebuilds, _mounts;
    static long _buildTicks, _diffTicks, _applyTicks, _mountTicks;
    static readonly long[] _opCounts = new long[OpKindCount];
    static readonly long[] _opTicks  = new long[OpKindCount];
    static readonly long[] _phaseCounts = new long[PhaseCount];
    static readonly long[] _phaseTicks = new long[PhaseCount];
    static readonly long[] _eventCounts = new long[EventCount];

    internal static void RecordRebuild(bool mount, long buildTicks, long diffTicks, long applyTicks)
    {
        _rebuilds++;
        _buildTicks += buildTicks; _diffTicks += diffTicks; _applyTicks += applyTicks;
        if (mount)
        {
            _mounts++;
            _mountTicks += buildTicks + diffTicks + applyTicks;
            RecordPhase(PerfPhase.PanelMountBuild, buildTicks);
            RecordPhase(PerfPhase.PanelMountDiff, diffTicks);
            RecordPhase(PerfPhase.PanelMountApply, applyTicks);
        }
        else
        {
            RecordPhase(PerfPhase.PanelRebuildBuild, buildTicks);
            RecordPhase(PerfPhase.PanelRebuildDiff, diffTicks);
            RecordPhase(PerfPhase.PanelRebuildApply, applyTicks);
        }
    }

    internal static void RecordOp(OpKind kind, long ticks)
    {
        _opCounts[(int)kind]++;
        _opTicks[(int)kind] += ticks;
    }

    internal static void RecordPhase(PerfPhase phase, long ticks)
    {
        _phaseCounts[(int)phase]++;
        _phaseTicks[(int)phase] += ticks;
    }

    internal static void RecordEvent(PerfEvent lifecycleEvent)
    {
        _eventCounts[(int)lifecycleEvent]++;
    }

    /// <summary>Zero every accumulator. Call before the scenario you want to measure.</summary>
    public static void Reset()
    {
        _rebuilds = 0; _mounts = 0;
        _buildTicks = 0; _diffTicks = 0; _applyTicks = 0; _mountTicks = 0;
        Array.Clear(_opCounts); Array.Clear(_opTicks);
        Array.Clear(_phaseCounts); Array.Clear(_phaseTicks); Array.Clear(_eventCounts);
    }

    /// <summary>Human-readable summary: per-phase totals and averages, mount cost, and a per-op-kind table sorted by total time.</summary>
    public static string Report()
    {
        if (_rebuilds == 0 && TotalOps() == 0 && TotalPhases() == 0 && TotalEvents() == 0)
            return "goo perf: no samples (set Perf.Enabled = true and exercise the UI)";

        var sb = new StringBuilder();
        sb.Append("goo perf: rebuilds: ").Append(_rebuilds).Append(", mounts: ").Append(_mounts);
        if (_mounts > 0) sb.Append(" (avg ").Append(Us(_mountTicks / _mounts).ToString("F1")).Append("us)");
        sb.AppendLine();

        AppendPhase(sb, "build", _buildTicks);
        AppendPhase(sb, "diff",  _diffTicks);
        AppendPhase(sb, "apply", _applyTicks);

        var phaseOrder = new int[PhaseCount];
        for (int i = 0; i < PhaseCount; i++) phaseOrder[i] = i;
        Array.Sort(phaseOrder, (a, b) => _phaseTicks[b].CompareTo(_phaseTicks[a]));
        foreach (int p in phaseOrder)
        {
            if (_phaseCounts[p] == 0) continue;
            sb.Append("  phase ").Append((PerfPhase)p)
              .Append(" x").Append(_phaseCounts[p])
              .Append(": ").Append(Us(_phaseTicks[p]).ToString("F1")).Append("us total, ")
              .Append((Us(_phaseTicks[p]) / _phaseCounts[p]).ToString("F2")).AppendLine("us avg");
        }

        for (int e = 0; e < EventCount; e++)
        {
            if (_eventCounts[e] == 0) continue;
            sb.Append("  event ").Append((PerfEvent)e)
              .Append(" x").Append(_eventCounts[e]).AppendLine();
        }

        // Op table, slowest kind first.
        var order = new int[OpKindCount];
        for (int i = 0; i < OpKindCount; i++) order[i] = i;
        Array.Sort(order, (a, b) => _opTicks[b].CompareTo(_opTicks[a]));
        foreach (int k in order)
        {
            if (_opCounts[k] == 0) continue;
            sb.Append("  op ").Append((OpKind)k)
              .Append(" x").Append(_opCounts[k])
              .Append(": ").Append(Us(_opTicks[k]).ToString("F1")).Append("us total, ")
              .Append((Us(_opTicks[k]) / _opCounts[k]).ToString("F2")).AppendLine("us avg");
        }
        return sb.ToString();
    }

    static void AppendPhase(StringBuilder sb, string name, long ticks)
    {
        sb.Append("  ").Append(name).Append(": ").Append(Us(ticks).ToString("F1")).Append("us total");
        if (_rebuilds > 0) sb.Append(", ").Append((Us(ticks) / _rebuilds).ToString("F2")).Append("us/rebuild");
        sb.AppendLine();
    }

    static double Us(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

    static long TotalOps()
    {
        long n = 0;
        for (int i = 0; i < OpKindCount; i++) n += _opCounts[i];
        return n;
    }

    static long TotalPhases()
    {
        long n = 0;
        for (int i = 0; i < PhaseCount; i++) n += _phaseCounts[i];
        return n;
    }

    static long TotalEvents()
    {
        long n = 0;
        for (int i = 0; i < EventCount; i++) n += _eventCounts[i];
        return n;
    }
}
