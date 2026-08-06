using System;
using System.Collections.Generic;
using Sandbox.UI;

namespace Goo.Input;

// Lazily caches one stable Action<MousePanelEvent> per key so successive Builds
// compare equal (Delegate.Equals) and emit no SetEvents op. 0 alloc on cache hit.
public sealed class HandlerTable<TKey> where TKey : notnull
{
    readonly Action<TKey, MousePanelEvent> _action;
    readonly Dictionary<TKey, Action<MousePanelEvent>> _cache = new();

    public HandlerTable(Action<TKey> action) : this((k, _) => action(k)) { }

    public HandlerTable(Action<TKey, MousePanelEvent> action) => _action = action;

    public Action<MousePanelEvent> this[TKey key]
    {
        get
        {
            if (!_cache.TryGetValue(key, out var h))
            {
                var k = key;
                h = e => _action(k, e);
                _cache[key] = h;
            }
            return h;
        }
    }
}
