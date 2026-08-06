using Sandbox.UI;

namespace Goo;

/// <summary>Length helpers for typography properties whose engine unit semantics are non-obvious.</summary>
public static class Typography
{
    /// <summary>CSS-style unitless LineHeight multiplier (e.g. 1.5 = 1.5x of FontSize).</summary>
    public static Length LineHeightMultiplier(float multiplier)
        => Length.Percent(multiplier * 100f).Value;

    /// <summary>CSS-style em-relative LetterSpacing (e.g. -0.02 for tight headings).</summary>
    public static Length LetterSpacingEm(float em)
        => Length.Em(em);
}
