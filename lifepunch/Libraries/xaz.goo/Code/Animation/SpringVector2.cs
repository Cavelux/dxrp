using System;
using Sandbox;

namespace Goo.Animation;

public record struct SpringVector2
{
    public Vector2 Current;
    public Vector2 Target;
    public Vector2 Velocity;
    public float Frequency;
    public float Damping;

    public SpringVector2(Vector2 initial, float frequency, float damping)
    {
        Current = initial;
        Target = initial;
        Velocity = default;
        Frequency = frequency;
        Damping = damping;
    }

    public void Update(float dt)
    {
        float vx = Velocity.x, vy = Velocity.y;
        Current = new Vector2(
            MathX.SpringDamp(Current.x, Target.x, ref vx, dt, Frequency, Damping),
            MathX.SpringDamp(Current.y, Target.y, ref vy, dt, Frequency, Damping));
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
