using System;
using Sandbox;

namespace Goo;

// Resolves which wedge of a radial menu a point falls in. One atan2 + a radius-band test, in the
// same frame as ui_shape.shader (see ShapeMath). Built to ride a native circular host panel
// (BorderRadius 50%) for the "inside the ring" test, doing only the "which wedge" math here.
public sealed class RadialDispatcher
{
    const float Tau = MathF.PI * 2f;
    readonly int _segments;
    readonly float _inner, _outer, _offsetRad;

    public RadialDispatcher(int segments, float innerRadius, float outerRadius = 1f, float angleOffsetDeg = 0f)
    {
        _segments = Math.Max(segments, 1);
        _inner = innerRadius;
        _outer = outerRadius;
        _offsetRad = angleOffsetDeg * (MathF.PI / 180f);
    }

    // Pairs with Shapes.Ring, which centers wedge 0 on 12 o'clock; offset slot 0 back by half a wedge to match.
    public static RadialDispatcher Centered(int segments, float innerRadius, float outerRadius = 1f)
        => new(segments, innerRadius, outerRadius, angleOffsetDeg: -180f / Math.Max(segments, 1));

    // uv in [0,1]. Returns 0.._segments-1, or null outside the [inner,outer] band.
    public int? SlotAt(Vector2 uv)
    {
        float dx = uv.x - 0.5f, dy = uv.y - 0.5f;
        float dist = MathF.Sqrt(dx * dx + dy * dy) * 2f; // 0..1 over the half-extent
        if (dist < _inner || dist > _outer) return null;

        float angle = MathF.Atan2(dx, -dy) - _offsetRad; // 0 at 12 o'clock, cw
        angle -= Tau * MathF.Floor(angle / Tau);          // wrap into [0, Tau)
        int slot = (int)(angle / (Tau / _segments));
        return Math.Clamp(slot, 0, _segments - 1);
    }
}
