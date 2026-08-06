using System;
using Sandbox;

namespace Goo.Animation;

public record struct DecayColor
{
    public Color Current;
    public Color Target;
    public float Halflife;

    public DecayColor(Color initial, float halflife)
    {
        Current = initial;
        Target = initial;
        Halflife = halflife;
    }

    public void Update(float dt)
    {
        Current = new Color(
            MathX.ExponentialDecay(Current.r, Target.r, Halflife, dt),
            MathX.ExponentialDecay(Current.g, Target.g, Halflife, dt),
            MathX.ExponentialDecay(Current.b, Target.b, Halflife, dt),
            MathX.ExponentialDecay(Current.a, Target.a, Halflife, dt));
    }

    public bool IsSettled =>
        MathF.Abs(Target.r - Current.r) < 0.0001f &&
        MathF.Abs(Target.g - Current.g) < 0.0001f &&
        MathF.Abs(Target.b - Current.b) < 0.0001f &&
        MathF.Abs(Target.a - Current.a) < 0.0001f;

    /// <summary>Advances by dt and returns true while still moving; chain calls with | (not ||) so every damper advances each frame.</summary>
    public bool Tick(float dt) { Update(dt); return !IsSettled; }
}
