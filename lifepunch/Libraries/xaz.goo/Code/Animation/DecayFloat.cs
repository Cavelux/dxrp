using System;
using Sandbox;

namespace Goo.Animation;

public record struct DecayFloat
{
    public float Current;
    public float Target;
    public float Halflife;

    public DecayFloat(float initial, float halflife)
    {
        Current = initial;
        Target = initial;
        Halflife = halflife;
    }

    public void Update(float dt) =>
        Current = MathX.ExponentialDecay(Current, Target, Halflife, dt);

    public bool IsSettled => MathF.Abs(Target - Current) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
