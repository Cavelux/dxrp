using System;
using System.Collections.Generic;

namespace Goo;

internal sealed class BuildContext
{
    [ThreadStatic] internal static BuildContext? _current;

    public static BuildContext Current => _current
        ?? throw new InvalidOperationException("No active BuildContext; struct intent constructed outside GooPanel.Tick?");

    // Published by GooPanel.OnUpdate before Build(), read by the static Applier during Apply
    // (BuildContext._current is this instance for the whole OnUpdate body). RootRebuild is the
    // panel's Rebuild(); AutoRebuildOnEvents gates whether event handlers auto-request a rebuild.
    internal Action? RootRebuild;
    internal bool AutoRebuildOnEvents = true;

    // Host-supplied { tag -> effect } manifest, set before each diff. Read by the reconciler to
    // resolve a tagged Blob's effect when it sets none inline.
    internal EffectMap? EffectManifest;

    internal ShaderEffect? ResolveTagEffect(string? tag)
        => tag is not null && EffectManifest is not null && EffectManifest.TryGetValue(tag, out var fx)
            ? fx
            : null;

    readonly Stack<FrameList> _frameListPool = new();
    readonly Stack<StyleList> _styleListPool = new();
    readonly Stack<List<int>> _pathPool = new();
    readonly Stack<Children>  _childrenPool  = new();
    readonly Stack<Dictionary<string, Fiber>> _keyedDictPool = new();
    readonly Stack<HashSet<string>>           _keySetPool    = new();
    readonly Stack<List<Op>>     _opListPool   = new();
    readonly Stack<List<string>> _warnListPool = new();
    // HostPath int[] pool keyed by length. Index 0 unused (length-0 paths use Array.Empty<int>()).
    // _hostPathPool[n] holds reusable int[n] slabs.
    readonly List<Stack<int[]>>  _hostPathPool = new();
    readonly List<FrameList> _rentedFrameLists = new();
    readonly List<StyleList> _rentedStyleLists = new();
    readonly List<Children>  _rentedChildren  = new();
    // Diff result lists outlive ReturnAll; the previous Diff's lists are returned to the pool
    // at the start of the NEXT Diff. This keeps DiffResult valid for callers that read it
    // after a try/finally ReturnAll block.
    List<Op>?     _pendingOpReturn;
    List<string>? _pendingWarnReturn;
    // HostPath snapshots live inside emitted Ops and are read by callers after ReturnAll, so
    // they obey the same delayed-return scheme: the previous Diff's snapshots get released to
    // the pool at the START of the next Diff (via BeginDiff), not on ReturnAll.
    List<int[]> _pendingHostPathReturn = new();
    List<int[]> _activeHostPathRented  = new();

    // Bumped on every ReturnAll. Stamped onto each rented Children so user code that
    // holds a Container reference past its build can be detected (see Children.EnsureValid).
    internal int _currentBuildId;

    public FrameList RentFrameList()
    {
        var list = _frameListPool.Count > 0 ? _frameListPool.Pop() : new FrameList();
        _rentedFrameLists.Add(list);
        return list;
    }

    public Children RentChildren()
    {
        var c = _childrenPool.Count > 0 ? _childrenPool.Pop() : new Children();
        c._list = RentFrameList();
        c._buildId = _currentBuildId;
        _rentedChildren.Add(c);
        return c;
    }

    public StyleList RentStyleList()
    {
        var list = _styleListPool.Count > 0 ? _styleListPool.Pop() : new StyleList();
        _rentedStyleLists.Add(list);
        return list;
    }

    // Path pool returned per-Diff (lifetime is one Reconciler.Diff call, not the whole frame).
    public List<int> RentPath() => _pathPool.Count > 0 ? _pathPool.Pop() : new List<int>();

    public void ReturnPath(List<int> p) { p.Clear(); _pathPool.Push(p); }

    // Scratch dict/set rented and returned synchronously inside Reconciler.DiffKeyed.
    public Dictionary<string, Fiber> RentKeyedDict() =>
        _keyedDictPool.Count > 0 ? _keyedDictPool.Pop() : new Dictionary<string, Fiber>();
    public void ReturnKeyedDict(Dictionary<string, Fiber> d) { d.Clear(); _keyedDictPool.Push(d); }

    public HashSet<string> RentKeySet() =>
        _keySetPool.Count > 0 ? _keySetPool.Pop() : new HashSet<string>();
    public void ReturnKeySet(HashSet<string> s) { s.Clear(); _keySetPool.Push(s); }

    // Recycles the previous Diff's list, then rents the next. Caller of the previous Diff
    // must not read the result after the next Diff begins.
    internal List<Op> RentOpList()
    {
        if (_pendingOpReturn != null) { _pendingOpReturn.Clear(); _opListPool.Push(_pendingOpReturn); }
        var l = _opListPool.Count > 0 ? _opListPool.Pop() : new List<Op>();
        _pendingOpReturn = l;
        return l;
    }

    internal List<string> RentWarningList()
    {
        if (_pendingWarnReturn != null) { _pendingWarnReturn.Clear(); _warnListPool.Push(_pendingWarnReturn); }
        var l = _warnListPool.Count > 0 ? _warnListPool.Pop() : new List<string>();
        _pendingWarnReturn = l;
        return l;
    }

    // Drains the previous Diff's HostPath snapshots back to the pool, then swaps the active
    // and pending tracking lists. Called from Reconciler.Diff before any RentHostPath calls.
    internal void BeginDiff()
    {
        for (int i = 0; i < _pendingHostPathReturn.Count; i++)
        {
            var arr = _pendingHostPathReturn[i];
            _hostPathPool[arr.Length].Push(arr);
        }
        _pendingHostPathReturn.Clear();
        (_pendingHostPathReturn, _activeHostPathRented) = (_activeHostPathRented, _pendingHostPathReturn);
    }

    internal int[] RentHostPath(List<int> path)
    {
        int len = path.Count;
        if (len == 0) return Array.Empty<int>();
        while (_hostPathPool.Count <= len) _hostPathPool.Add(new Stack<int[]>());
        var stack = _hostPathPool[len];
        var arr = stack.Count > 0 ? stack.Pop() : new int[len];
        for (int i = 0; i < len; i++) arr[i] = path[i];
        _activeHostPathRented.Add(arr);
        return arr;
    }

    internal void ReturnAll()
    {
        foreach (var l in _rentedFrameLists) { l.Reset(); _frameListPool.Push(l); }
        foreach (var l in _rentedStyleLists) { l.Reset(); _styleListPool.Push(l); }
        foreach (var c in _rentedChildren)   { c._list = null; _childrenPool.Push(c); }
        _rentedFrameLists.Clear();
        _rentedStyleLists.Clear();
        _rentedChildren.Clear();
        _currentBuildId++;
    }
}
