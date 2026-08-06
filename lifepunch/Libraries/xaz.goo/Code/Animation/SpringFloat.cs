using System;
using Sandbox;

namespace Goo.Animation;

public record struct SpringFloat
{
    public float Current;
    public float Target;
    public float Velocity;
    public float Frequency;
    public float Damping;

    public SpringFloat(float initial, float frequency, float damping)
    {
        Current = initial;
        Target = initial;
        Velocity = 0f;
        Frequency = frequency;
        Damping = damping;
    }

    public void Update(float dt) =>
        Current = MathX.SpringDamp(Current, Target, ref Velocity, dt, Frequency, Damping);

    public bool IsSettled =>
        MathF.Abs(Target - Current) < 0.0001f && MathF.Abs(Velocity) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
