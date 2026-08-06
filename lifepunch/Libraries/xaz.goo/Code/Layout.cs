using Sandbox.UI;

namespace Goo;

// Maps an anchor corner to the shared JustifyContent + AlignItems + FlexDirection tuple.
public static class Layout
{
    public enum Anchor { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight, Center }

    public readonly record struct AnchorResolution(
        Justify       JustifyContent,
        Align         AlignItems,
        FlexDirection FlexDirection )
    {
        // True when content stacks bottom-up (FlexDirection.Column). Useful for slide-in
        // animations whose entries originate from below the anchor.
        public bool StacksUp => FlexDirection == FlexDirection.Column;
    }

    public static AnchorResolution ResolveAnchor( Anchor a ) => a switch
    {
        Anchor.TopLeft      => new( Justify.FlexStart, Align.FlexStart, FlexDirection.ColumnReverse ),
        Anchor.TopCenter    => new( Justify.FlexStart, Align.Center,    FlexDirection.ColumnReverse ),
        Anchor.TopRight     => new( Justify.FlexStart, Align.FlexEnd,   FlexDirection.ColumnReverse ),
        Anchor.BottomLeft   => new( Justify.FlexEnd,   Align.FlexStart, FlexDirection.Column        ),
        Anchor.BottomCenter => new( Justify.FlexEnd,   Align.Center,    FlexDirection.Column        ),
        Anchor.BottomRight  => new( Justify.FlexEnd,   Align.FlexEnd,   FlexDirection.Column        ),
        Anchor.Center       => new( Justify.Center,    Align.Center,    FlexDirection.Column        ),
        _                   => throw new System.ArgumentOutOfRangeException( nameof( a ) ),
    };

    /// <summary>Overlap container: Position = Relative so Layer children resolve against it instead of escaping to a further ancestor.</summary>
    public static Container Stack() => new() { Position = PositionMode.Relative };

    /// <summary>Pins a child to overlap its Stack siblings (Absolute, Top 0, Left 0). Appended last, so it wins over the child's own Position fields; without it flex lays overlapping siblings side by side.</summary>
    public static Container Layer( Container child ) => child with { Position = PositionMode.Absolute, Top = 0, Left = 0 };

    /// <summary>Layer for a Sector shape; see Layer(Container).</summary>
    public static Sector Layer( Sector child ) => child with { Position = PositionMode.Absolute, Top = 0, Left = 0 };

    /// <summary>Layer for an Arc shape; see Layer(Container).</summary>
    public static Arc Layer( Arc child ) => child with { Position = PositionMode.Absolute, Top = 0, Left = 0 };

    /// <summary>Layer for a Polygon shape; see Layer(Container).</summary>
    public static Polygon Layer( Polygon child ) => child with { Position = PositionMode.Absolute, Top = 0, Left = 0 };
}
