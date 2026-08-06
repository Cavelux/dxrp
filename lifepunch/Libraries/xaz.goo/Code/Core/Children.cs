using System;
using System.Collections;

namespace Goo;

public sealed class Children : IEnumerable
{
    internal FrameList? _list;
    internal int _buildId;
    internal Children() { }
    internal int Count { get { EnsureValid(); return _list!.Count; } }
    internal ref Frame this[int i] { get { EnsureValid(); return ref _list![i]; } }

    public void Add<T>(in T child) where T : struct, IBlob
    {
        EnsureValid();
        ref Frame slot = ref _list!.Reserve();
        child.WriteTo(ref slot);
    }

    void EnsureValid()
    {
        var ctx = BuildContext._current;
        if (ctx == null || _list == null || _buildId != ctx._currentBuildId)
            throw new InvalidOperationException(
                "Container reused across rebuilds. The same Container instance cannot survive " +
                "past the Build() it was created in. Build a new one inside Build(), or extract " +
                "a helper function that returns a fresh Container each call.");
    }

    // IEnumerable is required by C# collection-initializer syntax; iteration is not a
    // use case for Children, so this returns an empty enumerator.
    IEnumerator IEnumerable.GetEnumerator() => Array.Empty<object>().GetEnumerator();
}
