using System;
using Sandbox;
using Sandbox.UI;

namespace Goo;

// One-shot blob mount for Virtual rows: the stock engine panels delete and recreate a row
// wholesale when its data changes, so no fiber survives past the apply and rows cannot hold
// private state. GooVirtualList uses the persistent-fiber variant instead.
internal static class RowMounter
{
    [ThreadStatic] static BuildContext? _ctx;
    [ThreadStatic] static bool _mounting;

    internal static void MountInto(Panel host, RowBuilder row, object data, Action? rootRebuild)
    {
        Fiber? fiber = null;
        DiffInto(host, ref fiber, row, data, rootRebuild);
    }

    // Persistent-fiber variant: GooVirtualList keeps the fiber per row and rediffs on item change.
    internal static void DiffInto(Panel host, ref Fiber? fiber, RowBuilder row, object data, Action? rootRebuild)
    {
        if (!Perf.Enabled) { DiffIntoCore(host, ref fiber, row, data, rootRebuild); return; }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        DiffIntoCore(host, ref fiber, row, data, rootRebuild);
        Perf.RecordPhase(PerfPhase.VirtualRowDiff, System.Diagnostics.Stopwatch.GetTimestamp() - t0);
    }

    static void DiffIntoCore(Panel host, ref Fiber? fiber, RowBuilder row, object data, Action? rootRebuild)
    {
        // ponytail: nested Virtual-in-row gets a throwaway context; the pooled one only serves the flat case
        var ctx = _mounting ? new BuildContext() : (_ctx ??= new BuildContext());
        var prev = BuildContext._current;
        bool prevMounting = _mounting;
        _mounting = true;
        BuildContext._current = ctx;
        ctx.RootRebuild = rootRebuild;
        ctx.AutoRebuildOnEvents = rootRebuild is not null;
        try
        {
            IBlob root = row(data);
            var result = Reconciler.Diff(ref fiber, root);
            foreach (var w in result.Warnings) Log.Warning(w);
            Applier.Apply(host, result.Ops);
        }
        finally
        {
            ctx.ReturnAll();
            BuildContext._current = prev;
            _mounting = prevMounting;
        }
    }
}
