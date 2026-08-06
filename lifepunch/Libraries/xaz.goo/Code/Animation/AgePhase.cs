using Sandbox;

namespace Goo.Animation;

/// <summary>Projects an entry's age into normalized slide-in / hold / fade-out values (0..1); pure value-type sibling of <see cref="Tween"/>.</summary>
public readonly record struct AgePhase
{
    public float SlideInDuration { get; init; }
    public float HoldTime        { get; init; }
    public float FadeOutDuration { get; init; }
    public Sandbox.Utility.Easing.Function SlideEasing { get; init; }
    public Sandbox.Utility.Easing.Function FadeEasing  { get; init; }

    public AgePhase(
        float slideInDuration, float holdTime, float fadeOutDuration,
        Sandbox.Utility.Easing.Function? slideEasing = null,
        Sandbox.Utility.Easing.Function? fadeEasing  = null )
    {
        SlideInDuration = slideInDuration;
        HoldTime        = holdTime;
        FadeOutDuration = fadeOutDuration;
        SlideEasing     = slideEasing ?? Easing.EaseOut;
        FadeEasing      = fadeEasing  ?? Easing.EaseIn;
    }

    /// <param name="age">Seconds since spawn (drives slide-in).</param>
    /// <param name="idleAge">Seconds since last input (drives hold + fade-out).</param>
    public AgeProjection Project( float age, float idleAge )
    {
        float slideT = SlideInDuration > 0f ? MathX.Clamp( age / SlideInDuration, 0f, 1f ) : 1f;
        float slide  = (SlideEasing ?? Easing.EaseOut)( slideT );

        float fadeT = FadeOutDuration > 0f
            ? MathX.Clamp( (idleAge - HoldTime) / FadeOutDuration, 0f, 1f )
            : (idleAge >= HoldTime ? 1f : 0f);
        float fade = (FadeEasing ?? Easing.EaseIn)( fadeT );

        return new AgeProjection( slide, fade, (1f - fade) * slide );
    }
}

/// <summary>Normalized 0..1 projection from <see cref="AgePhase.Project"/>; <c>Opacity</c> is the composite <c>(1 - Fade) * Slide</c>.</summary>
public readonly record struct AgeProjection( float Slide, float Fade, float Opacity );
