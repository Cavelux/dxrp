using System;
using Sandbox;

namespace Goo.Animation;

public record struct DecayVector2
{
    public Vector2 Current;
    public Vector2 Target;
    public float Halflife;

    public DecayVector2(Vector2 initial, float halflife)
    {
        Current = initial;
        Target = initial;
        Halflife = halflife;
    }

    public void Update(float dt) =>
        Current = new Vector2(
            MathX.ExponentialDecay(Current.x, Target.x, Halflife, dt),
            MathX.ExponentialDecay(Current.y, Target.y, Halflife, dt));

    public bool IsSettled =>
        MathF.Abs(Target.x - Current.x) < 0.0001f &&
        MathF.Abs(Target.y - Current.y) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
