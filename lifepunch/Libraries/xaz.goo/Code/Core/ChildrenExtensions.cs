using System;
using System.Collections.Generic;

namespace Goo;

// Auto-keying loop helper: AddRange runs the builder once per item and keys by index when the builder leaves Key == null. User-supplied keys win.
public static class ChildrenExtensions
{
    // TBlob is generic over all blob kinds (Polygon, Text, ... not just Container). A `with`
    // expression cannot key a generic struct, so the key is written into the Frame slot after
    // Add — same assembly, same effect, no boxing.
    public static void AddRange<TItem, TBlob>(
        this Children                children,
        IReadOnlyList<TItem>         items,
        Func<int, TItem, TBlob>      builder ) where TBlob : struct, IBlob
    {
        for ( int i = 0; i < items.Count; i++ )
        {
            var child = builder( i, items[i] );
            children.Add( in child );
            ref Frame slot = ref children[children.Count - 1];
            slot.Key ??= $"_idx:{i}";
        }
    }

    // Stable-identity overload: keys each child by keyOf(item) instead of position, so per-item state
    // (animations, focus, scroll, in-flight transitions) follows the item across reorder/insert/delete.
    // Use when the list can be reordered or items removed from the middle; the index-keyed overload above
    // is fine for append-only/static lists. A user-supplied Key on the built blob still wins; a null
    // keyOf result falls back to the index key so siblings never go unkeyed.
    public static void AddRange<TItem, TBlob>(
        this Children                children,
        IReadOnlyList<TItem>         items,
        Func<TItem, object>          keyOf,
        Func<int, TItem, TBlob>      builder ) where TBlob : struct, IBlob
    {
        for ( int i = 0; i < items.Count; i++ )
        {
            var child = builder( i, items[i] );
            children.Add( in child );
            ref Frame slot = ref children[children.Count - 1];
            slot.Key ??= keyOf( items[i] )?.ToString() ?? $"_idx:{i}";
        }
    }

    /// <summary>Nullable overload for inline conditionals in an initializer: Children = { header, cond ? badge : null }. Null is skipped.</summary>
    public static void Add<T>( this Children children, in T? child ) where T : struct, IBlob
    {
        if ( child is { } value ) children.Add( in value );
    }

    /// <summary>Loop element for initializers: adds one child per item, auto-keyed like AddRange. Children = { Header(), Each.Of(items, (i, x) => Card(x)) }.</summary>
    public static void Add<TItem, TBlob>( this Children children, in EachOf<TItem, TBlob> each ) where TBlob : struct, IBlob
    {
        if ( each.KeyOf is null ) children.AddRange( each.Items, each.Builder );
        else children.AddRange( each.Items, each.KeyOf, each.Builder );
    }
}

/// <summary>Factory for inline loops inside a Children initializer. The index-keyed form suits static/append-only lists; pass keyOf for lists that reorder or delete from the middle.</summary>
public static class Each
{
    public static EachOf<TItem, TBlob> Of<TItem, TBlob>( IReadOnlyList<TItem> items, Func<int, TItem, TBlob> builder )
        where TBlob : struct, IBlob => new( items, null, builder );

    public static EachOf<TItem, TBlob> Of<TItem, TBlob>( IReadOnlyList<TItem> items, Func<TItem, object> keyOf, Func<int, TItem, TBlob> builder )
        where TBlob : struct, IBlob => new( items, keyOf, builder );
}

/// <summary>The value Each.Of produces; consumed by the Children.Add overload above. Users never name it.</summary>
public readonly struct EachOf<TItem, TBlob> where TBlob : struct, IBlob
{
    internal readonly IReadOnlyList<TItem> Items;
    internal readonly Func<TItem, object>? KeyOf;
    internal readonly Func<int, TItem, TBlob> Builder;

    internal EachOf( IReadOnlyList<TItem> items, Func<TItem, object>? keyOf, Func<int, TItem, TBlob> builder )
    {
        Items = items;
        KeyOf = keyOf;
        Builder = builder;
    }
}
