using System;

namespace Goo;

internal sealed class FrameList
{
    Frame[] _items = new Frame[4];
    int _count;
    public int Count => _count;
    public ref Frame this[int i] => ref _items[i];

    public ref Frame Reserve()
    {
        if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        return ref _items[_count++];
    }

    internal void Reset()
    {
        Array.Clear(_items, 0, _count);
        _count = 0;
    }
}
