using System;
using System.Collections.Generic;

namespace Goo;

// Lexically-scoped dynamic token lookup. Push a dict via Scope, read via Get inside the body.
public static class Tokens
{
    [ThreadStatic] private static Stack<IReadOnlyDictionary<string, object>>? _stack;

    public static T Scope<T>(IReadOnlyDictionary<string, object> tokens, Func<T> body)
    {
        var stack = _stack ??= new Stack<IReadOnlyDictionary<string, object>>();
        stack.Push(tokens);
        try { return body(); }
        finally { stack.Pop(); }
    }

    public static T Get<T>(string key)
    {
        if (_stack != null)
        {
            foreach (var dict in _stack)
            {
                if (dict.TryGetValue(key, out var value))
                    return (T)value;
            }
        }
        throw new KeyNotFoundException($"Token '{key}' not found in any active Scope.");
    }

    public static bool TryGet<T>(string key, out T value)
    {
        if (_stack != null)
        {
            foreach (var dict in _stack)
            {
                if (dict.TryGetValue(key, out var obj) && obj is T typed)
                {
                    value = typed;
                    return true;
                }
            }
        }
        value = default!;
        return false;
    }
}
