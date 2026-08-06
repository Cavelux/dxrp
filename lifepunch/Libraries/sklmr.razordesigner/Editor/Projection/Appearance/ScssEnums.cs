using System.Globalization;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Projection.Appearance;

public static class ScssEnums
{
    public static string Css( FlexDirection d ) => d switch
    {
        FlexDirection.Row    => "row",
        FlexDirection.Column => "column",
        _                    => "row",
    };

    public static string Css( JustifyContent j ) => j switch
    {
        JustifyContent.Start        => "flex-start",
        JustifyContent.Center       => "center",
        JustifyContent.End          => "flex-end",
        JustifyContent.SpaceBetween => "space-between",
        JustifyContent.SpaceAround  => "space-around",
        _                           => "flex-start",
    };

    public static string Css( AlignItems a ) => a switch
    {
        AlignItems.Start   => "flex-start",
        AlignItems.Center  => "center",
        AlignItems.End     => "flex-end",
        AlignItems.Stretch => "stretch",
        _                  => "stretch",
    };

    // Auto is never emitted (AppearanceScss gates on != Auto) — the fallback keeps the switch total.
    public static string Css( AlignSelfKind a ) => a switch
    {
        AlignSelfKind.Start    => "flex-start",
        AlignSelfKind.Center   => "center",
        AlignSelfKind.End      => "flex-end",
        AlignSelfKind.Stretch  => "stretch",
        AlignSelfKind.Baseline => "baseline",
        _                      => "auto",
    };

    // Only Absolute is emitted (AppearanceScss gates on == Absolute) — the fallback keeps the switch total.
    public static string Css( PositionKind p ) => p switch
    {
        PositionKind.Absolute => "absolute",
        _                     => "relative",
    };

    // None is never emitted (AppearanceScss gates on != None) — the fallback keeps the switch total.
    public static string Css( TextTransformKind t ) => t switch
    {
        TextTransformKind.Uppercase  => "uppercase",
        TextTransformKind.Lowercase  => "lowercase",
        TextTransformKind.Capitalize => "capitalize",
        _                            => "none",
    };

    public static string Css( FlexWrap w ) => w switch
    {
        FlexWrap.NoWrap      => "nowrap",
        FlexWrap.Wrap        => "wrap",
        FlexWrap.WrapReverse => "wrap-reverse",
        _                    => "nowrap",
    };

    public static string Css( TextAlignment t ) => t switch
    {
        TextAlignment.Left   => "left",
        TextAlignment.Center => "center",
        TextAlignment.Right  => "right",
        _                    => "left",
    };

    public static string Css( CursorKind c ) => c switch
    {
        CursorKind.Auto       => "auto",
        CursorKind.Default    => "default",
        CursorKind.Pointer    => "pointer",
        CursorKind.Text       => "text",
        CursorKind.Grab       => "grab",
        CursorKind.Grabbing   => "grabbing",
        CursorKind.Wait       => "wait",
        CursorKind.Crosshair  => "crosshair",
        CursorKind.Move       => "move",
        CursorKind.NotAllowed => "not-allowed",
        CursorKind.None       => "none",
        _                     => "auto",
    };

    public static string Css( OverflowKind o ) => o switch
    {
        OverflowKind.Visible   => "visible",
        OverflowKind.Hidden    => "hidden",
        OverflowKind.Scroll    => "scroll",
        OverflowKind.Clip      => "clip",
        OverflowKind.ClipWhole => "clip-whole",
        _                      => "visible",
    };

    public static string Px( float v ) => v.ToString( "F0", CultureInfo.InvariantCulture ) + "px";

    public static string PseudoSelector( PseudoKind kind, NthChildMode nthMode, int nthArg )
    {
        return kind switch
        {
            PseudoKind.Hover      => "hover",
            PseudoKind.Active     => "active",
            PseudoKind.Focus      => "focus",
            PseudoKind.Empty      => "empty",
            PseudoKind.FirstChild => "first-child",
            PseudoKind.LastChild  => "last-child",
            PseudoKind.OnlyChild  => "only-child",
            PseudoKind.NthChild   => nthMode switch
            {
                NthChildMode.Odd  => "nth-child(odd)",
                NthChildMode.Even => "nth-child(even)",
                _                 => $"nth-child({(nthArg < 1 ? 1 : nthArg)})",
            },
            _ => throw new System.ArgumentOutOfRangeException( nameof( kind ), kind, "unsupported PseudoKind" ),
        };
    }
}
