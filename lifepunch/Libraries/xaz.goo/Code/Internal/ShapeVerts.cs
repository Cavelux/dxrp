using System;
using Sandbox;

namespace Goo.Internal;

// Vertex generators for the parametric shape factories. Unit [0,1] frame, origin top-left, y down,
// vertex 0 at top, clockwise - matches ShapeMath.Polygon and ui_shape.shader.
internal static class ShapeVerts
{
    static Vector2 At(float angleRad, float r) => new(0.5f + r * MathF.Sin(angleRad), 0.5f - r * MathF.Cos(angleRad));

    public static Vector2[] Ngon(int sides, float rotationDeg = 0f)
    {
        int n = Math.Max(sides, 3);
        var pts = new Vector2[n];
        float rot = rotationDeg * (MathF.PI / 180f);
        float step = MathF.Tau / n;
        for (int i = 0; i < n; i++)
            pts[i] = At(rot + i * step, 0.5f);
        return pts;
    }

    public static Vector2[] Star(int points, float innerRatio = 0.45f, float rotationDeg = 0f)
    {
        int n = Math.Max(points, 3);
        float q = Math.Clamp(innerRatio, 0.05f, 0.95f);
        var pts = new Vector2[n * 2];
        float rot = rotationDeg * (MathF.PI / 180f);
        float step = MathF.PI / n; // half a wedge between alternating outer/inner
        for (int i = 0; i < n * 2; i++)
            pts[i] = At(rot + i * step, (i & 1) == 0 ? 0.5f : 0.5f * q);
        return pts;
    }
}
