using System;
using System.Collections.Generic;
using Sandbox;

namespace Goo.Internal;

// Variable-length cache key for Polygon. Holds the same Vector2[] reference the user
// passed to the Blob (no defensive copy); structural equality + content hash let two
// independently-constructed arrays with identical contents share a cache slot.
internal readonly struct PolygonKey : IEquatable<PolygonKey>
{
    public readonly Vector2[] Points;
    public PolygonKey(Vector2[] points) { Points = points; }

    public bool Equals(PolygonKey other)
    {
        if (ReferenceEquals(Points, other.Points)) return true;
        if (Points is null || other.Points is null) return false;
        if (Points.Length != other.Points.Length) return false;
        for (int i = 0; i < Points.Length; i++)
            if (Points[i] != other.Points[i]) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is PolygonKey k && Equals(k);

    public override int GetHashCode()
    {
        if (Points is null) return 0;
        var h = new HashCode();
        h.Add(Points.Length);
        for (int i = 0; i < Points.Length; i++)
        {
            h.Add(Points[i].x);
            h.Add(Points[i].y);
        }
        return h.ToHashCode();
    }
}

// Every shape renders on the GPU via ui_shape.shader. Polygon vertices ride a baked (N x 1) points-
// texture (16-bit per coord), cached by structural content so identical polygons share one texture.
internal static class ShapeTextureCache
{
    // Pack vertices into RGBA8, one texel per point: x -> (R hi, G lo), y -> (B hi, A lo), 16-bit each.
    public static byte[] PackPoints(Vector2[] points)
    {
        var b = new byte[points.Length * 4];
        for (int i = 0; i < points.Length; i++)
        {
            int x = (int)MathF.Round(Math.Clamp(points[i].x, 0f, 1f) * 65535f);
            int y = (int)MathF.Round(Math.Clamp(points[i].y, 0f, 1f) * 65535f);
            int o = i * 4;
            b[o + 0] = (byte)(x >> 8); b[o + 1] = (byte)(x & 0xFF);
            b[o + 2] = (byte)(y >> 8); b[o + 3] = (byte)(y & 0xFF);
        }
        return b;
    }

    static readonly Dictionary<PolygonKey, Texture?> _pointsCache = new();

    public static Texture? GetOrBakePointsTexture(Vector2[] points)
    {
        if (points is null || points.Length < 3) return null;
        var key = new PolygonKey(points);
        if (_pointsCache.TryGetValue(key, out var cached)) return cached;
        byte[] rgba = PackPoints(points);
        var tex = Texture.Create(points.Length, 1).WithName($"goo-shape-points-{key.GetHashCode():x}")
                         .WithData(rgba).Finish();
        _pointsCache[key] = tex;
        return tex;
    }

    // Test seam: clears the cache between tests.
    internal static void Reset()
    {
        _pointsCache.Clear();
    }
}
