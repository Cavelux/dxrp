using System;
using Sandbox;

namespace Goo.Animation;

public record struct SmoothVector2
{
    public Vector2 Current;
    public Vector2 Target;
    public Vector2 Velocity;
    public float SmoothTime;

    public SmoothVector2(Vector2 initial, float smoothTime)
    {
        Current = initial;
        Target = initial;
        Velocity = default;
        SmoothTime = smoothTime;
    }

    public void Update(float dt)
    {
        float vx = Velocity.x, vy = Velocity.y;
        Current = new Vector2(
            MathX.SmoothDamp(Current.x, Target.x, ref vx, SmoothTime, dt),
            MathX.SmoothDamp(Current.y, Target.y, ref vy, SmoothTime, dt));
        Velocity = new Vector2(vx, vy);
    }

    public bool IsSettled =>
        MathF.Abs(Target.x - Current.x) < 0.0001f &&
        MathF.Abs(Target.y - Current.y) < 0.0001f &&
        MathF.Abs(Velocity.x) < 0.0001f &&
        MathF.Abs(Velocity.y) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
