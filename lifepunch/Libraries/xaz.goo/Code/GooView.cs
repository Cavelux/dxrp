using System;
using System.Diagnostics;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// A Razor markup element that hands its children to Goo. Drop <c>&lt;GooView Build=@MyBuild/&gt;</c>
/// into any .razor panel; every frame it runs the Goo reconciler pump (<see cref="Reconciler"/> to
/// <see cref="Applier"/>) against itself. Razor owns everything outside the tag; Goo owns everything
/// inside. GooView imposes no intrinsic sizing: size it like any panel (a zero-content-size view can
/// collapse, same as a bare &lt;div&gt;).
/// </summary>
public class GooView : Panel
{
    Fiber? _fiber;
    readonly BuildContext _ctx = new();

    /// <summary>Returns the single Goo root. Invoked every frame; the build closure may freely read
    /// external (Razor-owned) state because the view re-diffs each frame, so changes to that state appear
    /// with no explicit dirty signal. A dirty-on-reassign scheme cannot work here: the engine de-dupes a
    /// re-assigned Build delegate by hashcode, so re-running the reconciler each frame is what reflects
    /// outside state.</summary>
    public BlobBuilder? Build { get; set; }

    /// <summary>Optional per-frame driver for animation or polled state the build closure reads. Runs every
    /// frame before the diff. Its return value is ignored (the view always re-diffs).</summary>
    public Func<float, bool>? OnTick { get; set; }

    /// <summary>Optional tag-to-effect manifest. A Blob with a matching <see cref="Container.Tag"/> and no
    /// inline <c>Effect</c> resolves its effect from here.</summary>
    public EffectMap? Effects { get; set; }

    public override void Tick()
    {
        base.Tick();

        // Advance any per-frame state the build closure reads.
        bool perf = Perf.Enabled;
        long phaseStart = perf ? Stopwatch.GetTimestamp() : 0;
        OnTick?.Invoke(Time.Delta);
        if (perf)
            Perf.RecordPhase(PerfPhase.ViewTick, Stopwatch.GetTimestamp() - phaseStart);

        if (Build is null) return;

        // Re-diff every frame. A GooView's build closure reads state owned by the surrounding Razor
        // component, and the engine de-dupes the Build delegate by hashcode, so nothing signals GooView
        // when that state changes. Re-running the reconciler each frame is the only way to reflect it.
        // Reconciler.Diff emits no ops when nothing changed, so an idle view costs a build + tree-walk
        // but no Apply. Save/set/restore _current so a GooView can nest inside a GooPanel or another GooView.
        var prev = BuildContext._current;
        BuildContext._current = _ctx;
        _ctx.RootRebuild = null; // no dirty flag; the per-frame diff also reflects in-subtree events
        _ctx.EffectManifest = Effects;
        try
        {
            phaseStart = perf ? Stopwatch.GetTimestamp() : 0;
            IBlob next = Build();
            if (perf)
                Perf.RecordPhase(PerfPhase.ViewBuild, Stopwatch.GetTimestamp() - phaseStart);

            phaseStart = perf ? Stopwatch.GetTimestamp() : 0;
            var result = Reconciler.Diff(ref _fiber, next);
            if (perf)
                Perf.RecordPhase(PerfPhase.ViewDiff, Stopwatch.GetTimestamp() - phaseStart);

            foreach (var w in result.Warnings) Log.Warning(w);
            phaseStart = perf ? Stopwatch.GetTimestamp() : 0;
            Applier.Apply(this, result.Ops);
            if (perf)
                Perf.RecordPhase(PerfPhase.ViewApply, Stopwatch.GetTimestamp() - phaseStart);
        }
        finally
        {
            phaseStart = perf ? Stopwatch.GetTimestamp() : 0;
            _ctx.ReturnAll();
            if (perf)
                Perf.RecordPhase(PerfPhase.ViewReturnAll, Stopwatch.GetTimestamp() - phaseStart);
            BuildContext._current = prev;
        }
    }

    public override void OnDeleted()
    {
        base.OnDeleted();
        bool perf = Perf.Enabled;
        long start = perf ? Stopwatch.GetTimestamp() : 0;
        try
        {
            // Dispose any IDisposable Cell instances in the subtree before the panel goes away.
            Reconciler.TeardownFiberTree(_fiber);
        }
        finally
        {
            _fiber = null;
            if (perf)
                Perf.RecordPhase(PerfPhase.ViewDeleteTeardown, Stopwatch.GetTimestamp() - start);
        }
    }
}
