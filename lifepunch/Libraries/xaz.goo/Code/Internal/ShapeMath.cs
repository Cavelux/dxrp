using System;
using Sandbox;

namespace Goo.Internal;

// Analytic inside-tests mirroring ui_shape.shader. Single source of truth for
// shape hit-testing; the shader is the rendering mirror. Frame: uv in [0,1], d = uv-0.5,
// angleFromTop = atan2(d.x,-d.y) (0 at 12 o'clock, cw); radii are fractions of the 0.5 half-extent.
internal static class ShapeMath
{
    const float Tau = MathF.PI * 2f;
    const float Deg2Rad = MathF.PI / 180f;

    public static bool Contains(BlobKind kind, in ShapeParams p, Vector2 uv, float margin = 0f) => kind switch
    {
        BlobKind.Sector => Sector(p.A, p.B, p.C, p.D, uv, margin),
        // Arc: (Start, End, centre Radius, Stroke) -> inner/outer, exactly like StatefulShapePanel.ApplyShape.
        BlobKind.Arc    => Sector(p.A, p.B, MathF.Max(0f, p.C - p.D * 0.5f), MathF.Min(1f, p.C + p.D * 0.5f), uv, margin),
        _ => false,
    };

    public static bool Sector(float startDeg, float endDeg, float inner, float outer, Vector2 uv, float margin)
    {
        float dx = uv.x - 0.5f, dy = uv.y - 0.5f;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float outerR = 0.5f * outer, innerR = 0.5f * inner;
        if (dist < innerR - margin || dist > outerR + margin) return false;

        float startRad = startDeg * Deg2Rad, endRad = endDeg * Deg2Rad;
        float arcRad = endRad - startRad;
        if (arcRad < 0f) arcRad += Tau;
        arcRad = MathF.Min(arcRad, Tau);
        if (arcRad >= Tau - 1e-4f) return true; // full sweep, band only

        float angle = MathF.Atan2(dx, -dy);
        if (angle < 0f) angle += Tau;
        float rel = angle - startRad;
        rel -= Tau * MathF.Floor(rel / Tau); // wrap into [0, Tau)
        float angMargin = margin / MathF.Max(dist, 1e-3f); // matches shader angAa = aa/dist
        return rel >= -angMargin && rel <= arcRad + angMargin;
    }

    // Even-odd point-in-polygon. Points and uv are both in unit [0,1] space (the panel's full box),
    // so uv maps directly with no centre offset. Mirrors the kind-4 SDF in ui_shape.shader.
    public static bool Polygon(Vector2[] points, Vector2 uv)
    {
        if (points is null || points.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Vector2 pi = points[i], pj = points[j];
            if (((pi.y > uv.y) != (pj.y > uv.y)) &&
                (uv.x < (pj.x - pi.x) * (uv.y - pi.y) / (pj.y - pi.y) + pi.x))
                inside = !inside;
        }
        return inside;
    }
}
