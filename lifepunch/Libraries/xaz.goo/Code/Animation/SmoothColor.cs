using System;
using Sandbox;

namespace Goo.Animation;

public record struct SmoothColor
{
    public Color Current;
    public Color Target;
    public Color Velocity;
    public float SmoothTime;

    public SmoothColor(Color initial, float smoothTime)
    {
        Current = initial;
        Target = initial;
        Velocity = default;
        SmoothTime = smoothTime;
    }

    public void Update(float dt)
    {
        float vr = Velocity.r, vg = Velocity.g, vb = Velocity.b, va = Velocity.a;
        Current = new Color(
            MathX.SmoothDamp(Current.r, Target.r, ref vr, SmoothTime, dt),
            MathX.SmoothDamp(Current.g, Target.g, ref vg, SmoothTime, dt),
            MathX.SmoothDamp(Current.b, Target.b, ref vb, SmoothTime, dt),
            MathX.SmoothDamp(Current.a, Target.a, ref va, SmoothTime, dt));
        Velocity = new Color(vr, vg, vb, va);
    }

    public bool IsSettled =>
        MathF.Abs(Target.r - Current.r) < 0.0001f &&
        MathF.Abs(Target.g - Current.g) < 0.0001f &&
        MathF.Abs(Target.b - Current.b) < 0.0001f &&
        MathF.Abs(Target.a - Current.a) < 0.0001f &&
        MathF.Abs(Velocity.r) < 0.0001f &&
        MathF.Abs(Velocity.g) < 0.0001f &&
        MathF.Abs(Velocity.b) < 0.0001f &&
        MathF.Abs(Velocity.a) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
