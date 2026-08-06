using System;
using System.Collections.Generic;

namespace Goo.Animation;

/// <summary>Per-frame animator tick; returns true while the animator is still in motion.</summary>
public delegate bool TickFn(float dt);

/// <summary>Collects per-frame animator tick callbacks; UpdateAll returns true while any still runs.</summary>
public sealed class AnimationSet
{
    readonly List<TickFn> _ticks = new();

    public int Count => _ticks.Count;

    public void Add(TickFn tick)
    {
        if (tick is null) throw new ArgumentNullException(nameof(tick));
        _ticks.Add(tick);
    }

    public void Clear() => _ticks.Clear();

    public bool UpdateAll(float dt)
    {
        bool stillRunning = false;
        for (int i = 0; i < _ticks.Count; i++)
        {
            if (_ticks[i](dt)) stillRunning = true;
        }
        return stillRunning;
    }
}
