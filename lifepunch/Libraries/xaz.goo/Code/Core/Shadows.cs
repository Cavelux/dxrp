using System;
using Sandbox.UI;

namespace Goo;

/// <summary>An ordered stack of shadows for BoxShadow and TextShadow. Converts implicitly from a single Shadow or a Shadow[]. Assign <see cref="None"/> to declare "no shadows", which cancels an inherited TextShadow.</summary>
public readonly struct Shadows : IEquatable<Shadows>
{
    readonly Shadow _single;
    readonly Shadow[]? _items;
    readonly int _count;

    public Shadows(Shadow item)
    {
        _single = item;
        _items = null;
        _count = 1;
    }

    public Shadows(params Shadow[]? items)
    {
        if (items is null || items.Length == 0)
        {
            _single = default;
            _items = null;
            _count = 0;
        }
        else if (items.Length == 1)
        {
            _single = items[0];
            _items = null;
            _count = 1;
        }
        else
        {
            _single = default;
            _items = new Shadow[items.Length];
            for (int i = 0; i < items.Length; i++)
                _items[i] = items[i];
            _count = items.Length;
        }
    }

    /// <summary>Declares "no shadows", cancelling a TextShadow inherited from a parent.</summary>
    public static readonly Shadows None = new();

    public int Count => _count;
    public Shadow this[int i]
    {
        get
        {
            if ((uint)i >= (uint)_count) throw new IndexOutOfRangeException();
            return _items is null ? _single : _items[i];
        }
    }

    public static implicit operator Shadows(Shadow s) => new(s);
    public static implicit operator Shadows(Shadow[] s) => new(s);

    // Shadow has no Equals override, so the default one boxes and walks fields by reflection.
    internal static bool Same(in Shadow a, in Shadow b)
        => a.OffsetX == b.OffsetX && a.OffsetY == b.OffsetY && a.Blur == b.Blur
        && a.Spread == b.Spread && a.Inset == b.Inset && a.Color == b.Color;

    public bool Equals(Shadows other)
    {
        if (Count != other.Count) return false;
        for (int i = 0; i < Count; i++)
            if (!Same(this[i], other[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is Shadows s && Equals(s);

    public static bool operator ==(Shadows a, Shadows b) => a.Equals(b);
    public static bool operator !=(Shadows a, Shadows b) => !a.Equals(b);

    public override int GetHashCode()
    {
        var hash = Count;
        for (int i = 0; i < Count; i++)
        {
            var shadow = this[i];
            hash = HashCode.Combine(
                hash,
                shadow.OffsetX,
                shadow.OffsetY,
                shadow.Blur,
                shadow.Spread,
                shadow.Inset,
                shadow.Color);
        }
        return hash;
    }
}
