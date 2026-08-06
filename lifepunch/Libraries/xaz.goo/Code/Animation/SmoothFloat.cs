using System;
using Sandbox;

namespace Goo.Animation;

public record struct SmoothFloat
{
    public float Current;
    public float Target;
    public float Velocity;
    public float SmoothTime;

    public SmoothFloat(float initial, float smoothTime)
    {
        Current = initial;
        Target = initial;
        Velocity = 0f;
        SmoothTime = smoothTime;
    }

    public void Update(float dt) =>
        Current = MathX.SmoothDamp(Current, Target, ref Velocity, SmoothTime, dt);

    public bool IsSettled =>
        MathF.Abs(Target - Current) < 0.0001f && MathF.Abs(Velocity) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
