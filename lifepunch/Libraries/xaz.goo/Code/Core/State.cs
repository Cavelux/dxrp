using System;
using System.Collections.Generic;

namespace Goo;

/// <summary>Auto-dirtying state for mutation outside event handlers (timers, network, polls): writing a new Value marks the owner dirty, so no manual Rebuild() call. Create via Track() on GooPanel or Cell.</summary>
public sealed class State<T>
{
    readonly Action _invalidate;
    T _value;

    public State(T initial, Action invalidate)
    {
        _value = initial;
        _invalidate = invalidate;
    }

    /// <summary>Current value. Setting a different value invalidates the owner; setting an equal value is a no-op.</summary>
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            _invalidate();
        }
    }

    public override string ToString() => _value?.ToString() ?? "";
}
