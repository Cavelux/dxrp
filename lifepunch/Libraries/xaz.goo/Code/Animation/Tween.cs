using System;

namespace Goo.Animation;

public readonly record struct Tween
{
    public Sandbox.Utility.Easing.Function Easing { get; init; }
    public float Duration   { get; init; }
    public float Delay      { get; init; }
    public float SpeedScale { get; init; }
    public int   Iterations { get; init; }
    public bool  PingPong   { get; init; }
    public bool  Reversed   { get; init; }

    public Tween(Sandbox.Utility.Easing.Function easing, float duration, float delay = 0f)
    {
        Easing     = easing;
        Duration   = duration;
        Delay      = delay;
        SpeedScale = 1f;
        Iterations = 1;
        PingPong   = false;
        Reversed   = false;
    }

    /// <summary>
    /// Bridge a designer-authored Sandbox.Curve (authored over [0, 1]) into a Tween.
    /// Allocates one delegate per call; cache the result in a static readonly field.
    /// </summary>
    public static Tween FromCurve(Sandbox.Curve curve, float duration, float delay = 0f)
        => new Tween(curve.Evaluate, duration, delay);

    public float Eval(float elapsedSec)
    {
        if (Duration <= 0f) return Reversed ? 0f : 1f;

        float t = (elapsedSec - Delay) * SpeedScale;
        if (t <= 0f) return Reversed ? 1f : 0f;

        float cycleDuration = PingPong ? 2f * Duration : Duration;

        if (Iterations > 0 && t >= cycleDuration * Iterations)
        {
            float endLocal = PingPong ? 0f : 1f;
            if (Reversed) endLocal = 1f - endLocal;
            return Easing(endLocal);
        }

        float cycleT = t % cycleDuration;
        float local = PingPong
            ? (cycleT < Duration ? cycleT / Duration : 1f - (cycleT - Duration) / Duration)
            : cycleT / Duration;

        if (Reversed) local = 1f - local;
        return Easing(local);
    }
}

public static class TweenExtensions
{
    public static Tween Loop(this Tween t)               => t with { Iterations = -1 };
    public static Tween Times(this Tween t, int n)       => t with { Iterations = n };
    public static Tween PingPong(this Tween t)           => t with { PingPong = true };
    public static Tween Scale(this Tween t, float speed) => t with { SpeedScale = speed };
    public static Tween WithDelay(this Tween t, float s) => t with { Delay = s };
    public static Tween Reverse(this Tween t)            => t with { Reversed = true };
}
