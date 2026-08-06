using System;
using Sandbox;
using Sandbox.Rendering;

namespace Goo.Internal;

// ui_shape.shader effect that recolors its fill on hover and lerps ShapeColor via FadeStep.
internal sealed record ShapeEffect : ShaderEffect
{
    readonly float _kind, _inner, _outer, _start, _end;
    readonly Vector2[]? _points;
    readonly Color _baseColor, _hoverColor;
    readonly float _transitionMs;
    float _hover; // 0..1 animated fade, mutated in Apply only

    public ShapeEffect(float kind, float inner, float outer, float start, float end, float corner,
        Color baseColor, Color hoverColor, float transitionMs)
        : base("shaders/ui_shape.shader")
    {
        _kind = kind; _inner = inner; _outer = outer; _start = start; _end = end;
        _baseColor = baseColor; _hoverColor = hoverColor; _transitionMs = transitionMs;

        this["ShapeKind"]   = kind;
        this["ShapeInner"]  = inner;
        this["ShapeOuter"]  = outer;
        this["ShapeStart"]  = start;
        this["ShapeEnd"]    = end;
        this["ShapeCorner"] = corner;
        this["ShapeColor"]  = baseColor;
    }

    // Polygon mode: geometry rides a points-texture uniform; Contains uses ShapeMath.Polygon.
    public ShapeEffect(Vector2[] points, Texture pointsTex, Color baseColor, Color hoverColor, float transitionMs)
        : base("shaders/ui_shape.shader")
    {
        _kind = 4f; _points = points;
        _baseColor = baseColor; _hoverColor = hoverColor; _transitionMs = transitionMs;

        this["ShapeKind"]  = 4f;
        this["PointsTex"]  = pointsTex;
        this["PointCount"] = (float)(points?.Length ?? 0);
        this["ShapeColor"] = baseColor;
    }

    // Inside the drawn outline. Only Sector (kind 0) and Polygon (kind 4) effects are ever built.
    public bool Contains(Vector2 uv, float margin) =>
        _kind > 3.5f ? (_points is null || ShapeMath.Polygon(_points, uv))
                     : ShapeMath.Sector(_start, _end, _inner, _outer, uv, margin);

    // Advance current toward target by dt over durationMs. 0 duration snaps (CSS default). Pure.
    public static float FadeStep(float current, float target, float dt, float durationMs)
    {
        if (durationMs <= 0f) return target;
        float delta = dt * 1000f / durationMs;
        if (current < target) return MathF.Min(target, current + delta);
        if (current > target) return MathF.Max(target, current - delta);
        return current;
    }

    protected internal override void Apply(CommandList cl, Rect rect)
    {
        var size = rect.Size;
        bool inside = size.x > 0f && size.y > 0f
            && Contains(new Vector2((Mouse.Position.x - rect.Left) / size.x,
                                    (Mouse.Position.y - rect.Top) / size.y), 0f);
        _hover = FadeStep(_hover, inside ? 1f : 0f, Time.Delta, _transitionMs);

        // base.Apply pushes the bag then override ShapeColor with the lerped value
        base.Apply(cl, rect);
        cl.Attributes.Set("ShapeColor", Color.Lerp(_baseColor, _hoverColor, _hover));
    }

    public bool Equals(ShapeEffect? other)
        => base.Equals(other) && _hoverColor == other._hoverColor && _transitionMs == other._transitionMs;

    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), _hoverColor, _transitionMs);
}
