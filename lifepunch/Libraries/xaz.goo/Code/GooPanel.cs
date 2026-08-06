using System.Collections.Generic;
using System.Diagnostics;
using Sandbox;
using Sandbox.UI;

namespace Goo;

public abstract class GooPanel<TRoot> : PanelComponent, IHotloadManaged where TRoot : struct, IBlob
{
    Fiber? _fiber;
    readonly BuildContext _ctx = new();
    bool _needsBuild = true;
    bool _wasMoving;
    bool _hasMounted;

    protected abstract TRoot Build();

    public void Rebuild() => _needsBuild = true;

    void EventRebuild()
    {
        if (Perf.Enabled) Perf.RecordEvent(PerfEvent.PanelEventRebuild);
        Rebuild();
    }

    /// <summary>Creates a State bound to this panel: writes trigger a rebuild automatically.</summary>
    protected State<T> Track<T>(T initial) => new(initial, Rebuild);

    /// <summary>Override to drive per-frame state (animations, polled input).
    /// Return true while a rebuild is still needed next frame (e.g. an animation is in motion).
    /// Runs every frame, before the build gate. On the frame motion stops (this returns false
    /// after the previous frame returned true) one extra rebuild fires so the final settled value paints.</summary>
    protected virtual bool Tick(float dt) => false;

    /// <summary>When true (default), a rebuild is requested automatically after any event
    /// handler on this panel's tree fires. Set false to restore fully manual Rebuild() control.</summary>
    protected virtual bool AutoRebuildOnEvents => true;

    /// <summary>Override to map tags to effects. A Blob with a matching <see cref="Container.Tag"/> and no
    /// inline <c>Effect</c> resolves its effect from here. Default targets nothing.</summary>
    protected virtual EffectMap Effects => EffectMap.Empty;

    // Hotload hook. Engine creates a fresh instance per Component on hotload, then
    // Cecil-migrates field values from the prior instance. We need to trigger a diff
    // against the new Build() output so the panel reflects edited source.
    void IHotloadManaged.Created(IReadOnlyDictionary<string, object> state) => _needsBuild = true;

    protected override void OnEnabled()
    {
        bool perf = Perf.Enabled;
        long start = perf ? Stopwatch.GetTimestamp() : 0;
        // Engine destroys Panel on disable and creates a fresh empty one on enable.
        // Drop our fiber so the next diff is a full mount against the new Panel.
        _fiber = null;
        _needsBuild = true;
        _wasMoving = false;
        if (perf)
        {
            Perf.RecordPhase(PerfPhase.PanelEnable, Stopwatch.GetTimestamp() - start);
            if (_hasMounted) Perf.RecordEvent(PerfEvent.PanelRemount);
        }
    }

    protected override void OnDisabled()
    {
        bool perf = Perf.Enabled;
        long start = perf ? Stopwatch.GetTimestamp() : 0;
        try
        {
            Reconciler.TeardownFiberTree(_fiber);
        }
        finally
        {
            _fiber = null;
            if (perf)
                Perf.RecordPhase(PerfPhase.PanelDisableTeardown, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override void OnUpdate()
    {
        if (Panel == null) return;

        // Per-frame state advances every frame, independent of the build gate.
        // A motion->settled transition rebuilds once more so the final resting value paints.
        bool perf = Perf.Enabled;
        long tickStart = perf ? Stopwatch.GetTimestamp() : 0;
        bool moving = Tick(Time.Delta);
        if (perf)
        {
            Perf.RecordPhase(PerfPhase.PanelTick, Stopwatch.GetTimestamp() - tickStart);
            if (moving) Perf.RecordEvent(PerfEvent.PanelMovingFrame);
        }
        if (moving || _wasMoving) _needsBuild = true;
        _wasMoving = moving;
        if (!_needsBuild)
        {
            if (perf) Perf.RecordEvent(PerfEvent.PanelIdleFrame);
            return;
        }

        var prev = BuildContext._current;
        BuildContext._current = _ctx;
        _ctx.RootRebuild = EventRebuild;
        _ctx.AutoRebuildOnEvents = AutoRebuildOnEvents;
        _ctx.EffectManifest = Effects;
        try
        {
            bool mount = _fiber is null;
            long t0 = perf ? Stopwatch.GetTimestamp() : 0;

            TRoot next = Build();
            long t1 = perf ? Stopwatch.GetTimestamp() : 0;

            var result = Reconciler.Diff(ref _fiber, in next);
            long t2 = perf ? Stopwatch.GetTimestamp() : 0;

            foreach (var w in result.Warnings) Log.Warning(w);
            Applier.Apply(Panel, result.Ops);

            if (perf)
                Perf.RecordRebuild(mount, t1 - t0, t2 - t1, Stopwatch.GetTimestamp() - t2);
            _hasMounted = true;
        }
        finally
        {
            long returnStart = perf ? Stopwatch.GetTimestamp() : 0;
            _ctx.ReturnAll();
            if (perf)
                Perf.RecordPhase(PerfPhase.PanelReturnAll, Stopwatch.GetTimestamp() - returnStart);
            BuildContext._current = prev;
            _needsBuild = false;
        }
    }

    // Goo manages Panel.Children directly via Diff/Apply; skip Razor's render-tree build path.
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
    }
}
