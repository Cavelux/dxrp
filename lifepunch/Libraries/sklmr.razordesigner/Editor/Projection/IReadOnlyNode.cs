using System;
using System.Collections.Generic;
using Sandbox; // Color

namespace Grains.RazorDesigner.Projection;

public interface IReadOnlyStateRule
{
    Document.PseudoKind State { get; }
    Document.NthChildMode NthChildMode { get; }
    int NthChildArg { get; }
    IAppearance Delta { get; }

    public static int CompareCanonical( IReadOnlyStateRule a, IReadOnlyStateRule b )
    {
        int c = ((int)a.State).CompareTo( (int)b.State );
        if ( c != 0 ) return c;
        c = ((int)a.NthChildMode).CompareTo( (int)b.NthChildMode );
        if ( c != 0 ) return c;
        return a.NthChildArg.CompareTo( b.NthChildArg );
    }
}

public interface IReadOnlyNode
{
    Guid Id { get; }
    string Kind { get; }         // == ControlType.ToString()
    string ClassName { get; }
    IAppearance Appearance { get; }
    IPayload Payload { get; }
    IReadOnlyList<IReadOnlyNode> Children { get; }                              // non-slot children
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyNode>> Slots { get; }    // slot-name -> slot children (only SplitContainer populates)
    IReadOnlyList<IReadOnlyStateRule> StateRules { get; }                       // per-state style deltas; canonical order not guaranteed here (the Applier sorts)
}

public interface IAppearance
{
    // Layout
    Document.Length Width { get; }
    Document.Length Height { get; }

    // Flex container
    Document.FlexDirection Direction { get; }
    Document.JustifyContent Justify { get; }
    Document.AlignItems Align { get; }
    float Gap { get; }
    Document.Edges Padding { get; }
    Document.FlexWrap Wrap { get; }

    // Positioning (grd-7t2z)
    Document.PositionKind Position { get; }
    Document.Length Top { get; }
    Document.Length Left { get; }
    Document.Length Right { get; }
    Document.Length Bottom { get; }

    // Flex self
    float FlexGrow { get; }
    float FlexShrink { get; }
    Document.Length FlexBasis { get; }
    Document.AlignSelfKind AlignSelf { get; }

    // Typography + OverrideTypography
    bool OverrideTypography { get; }
    string FontFamily { get; }
    Document.Length FontSize { get; }
    int FontWeight { get; }
    Color Color { get; }
    Document.TextAlignment TextAlign { get; }
    bool FontStyleItalic { get; }
    Document.TextTransformKind TextTransform { get; }
    Document.Length LetterSpacing { get; }
    Document.Length LineHeight { get; }

    // Background + OverrideBackground
    bool OverrideBackground { get; }
    Color BackgroundColor { get; }
    string BackgroundImage { get; }
    string BackgroundSize { get; }
    string BackgroundPosition { get; }
    string BackgroundRepeat { get; }

    // Border + OverrideBorder
    bool OverrideBorder { get; }
    Document.Length BorderRadius { get; }
    Color BorderColor { get; }
    Document.Length BorderWidth { get; }

    // Effects + OverrideEffects
    bool OverrideEffects { get; }
    Document.Length BoxShadowX { get; }
    Document.Length BoxShadowY { get; }
    Document.Length BoxShadowBlur { get; }
    Color BoxShadowColor { get; }
    bool BoxShadowInset { get; }
    float Opacity { get; }

    // Constraints + OverrideConstraints
    bool OverrideConstraints { get; }
    Document.Edges Margin { get; }
    Document.Length MinWidth { get; }
    Document.Length MaxWidth { get; }
    Document.Length MinHeight { get; }
    Document.Length MaxHeight { get; }

    // Interaction + OverrideInteraction
    bool OverrideInteraction { get; }
    Document.CursorKind Cursor { get; }
    Document.OverflowKind Overflow { get; }
    int ZIndex { get; }
    bool PointerEvents { get; }
}

public interface IPayload
{
    string Content { get; }       // Label/Button text; Checkbox label
    string Placeholder { get; }   // TextEntry
    string Source { get; }        // Image src
    string IconName { get; }      // IconPanel glyph
    Document.Length CheckboxSize { get; }  // Checkbox box size
}
