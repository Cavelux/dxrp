using System;
using System.Collections.Generic;
using System.Globalization;
using Grains.RazorDesigner.Document;
using static Grains.RazorDesigner.Projection.Appearance.ScssEnums;

namespace Grains.RazorDesigner.Projection.Appearance;

public static class AppearanceScss
{
    public static IReadOnlyList<string> Emit(
        IAppearance a,
        bool isRoot,
        bool isContainer,
        int childCount,
        bool isLabel = false,
        bool isCheckbox = false,
        Length checkboxSize = default,
        IAppearance baseForDiff = null )    // NEW: when non-null, only emit props where a.X != baseForDiff.X
    {
        var lines = new List<string>();

        // --- Width / Height ---
        if ( a.Width.Unit  != LengthUnit.Auto
             && ( baseForDiff is null || !a.Width.Equals( baseForDiff.Width ) ) )
            lines.Add( $"width: {a.Width.ToCss()};" );
        if ( a.Height.Unit != LengthUnit.Auto
             && ( baseForDiff is null || !a.Height.Equals( baseForDiff.Height ) ) )
            lines.Add( $"height: {a.Height.ToCss()};" );

        if ( a.Position == PositionKind.Absolute
             && ( baseForDiff is null || a.Position != baseForDiff.Position ) )
            lines.Add( $"position: {Css( a.Position )};" );
        if ( a.Top.Unit    != LengthUnit.Auto
             && ( baseForDiff is null || !a.Top.Equals( baseForDiff.Top ) ) )
            lines.Add( $"top: {a.Top.ToCss()};" );
        if ( a.Left.Unit   != LengthUnit.Auto
             && ( baseForDiff is null || !a.Left.Equals( baseForDiff.Left ) ) )
            lines.Add( $"left: {a.Left.ToCss()};" );
        if ( a.Right.Unit  != LengthUnit.Auto
             && ( baseForDiff is null || !a.Right.Equals( baseForDiff.Right ) ) )
            lines.Add( $"right: {a.Right.ToCss()};" );
        if ( a.Bottom.Unit != LengthUnit.Auto
             && ( baseForDiff is null || !a.Bottom.Equals( baseForDiff.Bottom ) ) )
            lines.Add( $"bottom: {a.Bottom.ToCss()};" );

        // --- Flex-self (skip on root — no parent flex container) ---
        if ( !isRoot )
        {
            // Guards: engine defaults (grow=0, shrink=1), not creation hints.
            if ( a.FlexGrow   != 0f
                 && ( baseForDiff is null || a.FlexGrow != baseForDiff.FlexGrow ) )
                lines.Add( $"flex-grow: {a.FlexGrow.ToString( "0.##", CultureInfo.InvariantCulture )};" );
            if ( a.FlexShrink != 1f
                 && ( baseForDiff is null || a.FlexShrink != baseForDiff.FlexShrink ) )
                lines.Add( $"flex-shrink: {a.FlexShrink.ToString( "0.##", CultureInfo.InvariantCulture )};" );
            if ( a.FlexBasis.Unit != LengthUnit.Auto
                 && ( baseForDiff is null || !a.FlexBasis.Equals( baseForDiff.FlexBasis ) ) )
                lines.Add( $"flex-basis: {a.FlexBasis.ToCss()};" );
            // align-self: per-child cross-axis override; Auto = inherit parent's align-items, emits nothing (grd-7s3).
            if ( a.AlignSelf != AlignSelfKind.Auto
                 && ( baseForDiff is null || a.AlignSelf != baseForDiff.AlignSelf ) )
                lines.Add( $"align-self: {Css( a.AlignSelf )};" );
        }

        // --- Flex-container props (containers only) ---
        if ( isContainer )
        {
            // YogaWrapper engine defaults: direction=Row, justify=Start, align=Stretch.
            if ( a.Direction != FlexDirection.Row
                 && ( baseForDiff is null || a.Direction != baseForDiff.Direction ) )
                lines.Add( $"flex-direction: {Css( a.Direction )};" );
            if ( a.Justify   != JustifyContent.Start
                 && ( baseForDiff is null || a.Justify != baseForDiff.Justify ) )
                lines.Add( $"justify-content: {Css( a.Justify )};" );
            if ( a.Align     != AlignItems.Stretch
                 && ( baseForDiff is null || a.Align != baseForDiff.Align ) )
                lines.Add( $"align-items: {Css( a.Align )};" );
            // Gap only emits when there are >= 2 siblings to space (phantom gap guard).
            if ( a.Gap > 0f && childCount >= 2
                 && ( baseForDiff is null || a.Gap != baseForDiff.Gap ) )
                lines.Add( $"gap: {Px( a.Gap )};" );
            if ( a.Wrap      != FlexWrap.NoWrap
                 && ( baseForDiff is null || a.Wrap != baseForDiff.Wrap ) )
                lines.Add( $"flex-wrap: {Css( a.Wrap )};" );
        }

        if ( !a.Padding.IsAllAuto && !a.Padding.IsDefaultZero
             && ( baseForDiff is null || a.Padding != baseForDiff.Padding ) )
            lines.Add( $"padding: {a.Padding.ToCss()};" );


        if ( a.OverrideTypography )
        {
            if ( !string.IsNullOrEmpty( a.FontFamily )
                 && ( baseForDiff is null || !string.Equals( a.FontFamily, baseForDiff.FontFamily, StringComparison.Ordinal ) ) )
                lines.Add( $"font-family: {a.FontFamily};" );
            if ( a.FontSize.Unit != LengthUnit.Auto
                 && ( baseForDiff is null || !a.FontSize.Equals( baseForDiff.FontSize ) ) )
                lines.Add( $"font-size: {a.FontSize.ToCss()};" );
            if ( baseForDiff is null || a.FontWeight != baseForDiff.FontWeight )
                lines.Add( $"font-weight: {a.FontWeight.ToString( CultureInfo.InvariantCulture )};" );
            if ( baseForDiff is null || !ColorsEqual( a.Color, baseForDiff.Color ) )
                lines.Add( $"color: {a.Color.Hex};" );
            if ( isLabel
                 && ( baseForDiff is null || a.TextAlign != baseForDiff.TextAlign ) )
                lines.Add( $"text-align: {Css( a.TextAlign )};" );
            if ( a.FontStyleItalic
                 && ( baseForDiff is null || a.FontStyleItalic != baseForDiff.FontStyleItalic ) )
                lines.Add( "font-style: italic;" );
            if ( a.TextTransform != TextTransformKind.None
                 && ( baseForDiff is null || a.TextTransform != baseForDiff.TextTransform ) )
                lines.Add( $"text-transform: {Css( a.TextTransform )};" );
            if ( a.LetterSpacing.Unit != LengthUnit.Auto
                 && ( baseForDiff is null || !a.LetterSpacing.Equals( baseForDiff.LetterSpacing ) ) )
                lines.Add( $"letter-spacing: {a.LetterSpacing.ToCss()};" );
            if ( a.LineHeight.Unit    != LengthUnit.Auto
                 && ( baseForDiff is null || !a.LineHeight.Equals( baseForDiff.LineHeight ) ) )
                lines.Add( $"line-height: {a.LineHeight.ToCss()};" );
        }

        if ( a.OverrideBackground )
        {
            if ( baseForDiff is null || !ColorsEqual( a.BackgroundColor, baseForDiff.BackgroundColor ) )
                lines.Add( $"background-color: {a.BackgroundColor.Hex};" );
            var imageEmpty = string.IsNullOrWhiteSpace( a.BackgroundImage );
            if ( imageEmpty )
            {
                var baseAlsoEmptyOverride = baseForDiff is not null
                    && baseForDiff.OverrideBackground
                    && string.IsNullOrWhiteSpace( baseForDiff.BackgroundImage );
                if ( !baseAlsoEmptyOverride )
                    lines.Add( "background-image: none;" );
            }
            else if ( baseForDiff is null || !string.Equals( a.BackgroundImage.Trim(), baseForDiff.BackgroundImage?.Trim(), StringComparison.Ordinal ) )
                lines.Add( $"background-image: {a.BackgroundImage.Trim()};" );
            if ( !string.IsNullOrWhiteSpace( a.BackgroundSize )
                 && ( baseForDiff is null || !string.Equals( a.BackgroundSize.Trim(), baseForDiff.BackgroundSize.Trim(), StringComparison.Ordinal ) ) )
                lines.Add( $"background-size: {a.BackgroundSize.Trim()};" );
            if ( !string.IsNullOrWhiteSpace( a.BackgroundPosition )
                 && ( baseForDiff is null || !string.Equals( a.BackgroundPosition.Trim(), baseForDiff.BackgroundPosition.Trim(), StringComparison.Ordinal ) ) )
                lines.Add( $"background-position: {a.BackgroundPosition.Trim()};" );
            if ( !string.IsNullOrWhiteSpace( a.BackgroundRepeat )
                 && ( baseForDiff is null || !string.Equals( a.BackgroundRepeat.Trim(), baseForDiff.BackgroundRepeat.Trim(), StringComparison.Ordinal ) ) )
                lines.Add( $"background-repeat: {a.BackgroundRepeat.Trim()};" );
        }

        if ( a.OverrideBorder )
        {
            if ( a.BorderRadius.Unit != LengthUnit.Auto
                 && ( baseForDiff is null || !a.BorderRadius.Equals( baseForDiff.BorderRadius ) ) )
                lines.Add( $"border-radius: {a.BorderRadius.ToCss()};" );
            var hasVisibleBorder = a.BorderWidth.Unit != LengthUnit.Auto && a.BorderWidth.Value != 0f;
            if ( hasVisibleBorder
                 && ( baseForDiff is null || !a.BorderWidth.Equals( baseForDiff.BorderWidth ) || !ColorsEqual( a.BorderColor, baseForDiff.BorderColor ) ) )
            {
                if ( baseForDiff is null || !a.BorderWidth.Equals( baseForDiff.BorderWidth ) )
                    lines.Add( $"border-width: {a.BorderWidth.ToCss()};" );
                if ( baseForDiff is null || !ColorsEqual( a.BorderColor, baseForDiff.BorderColor ) )
                    lines.Add( $"border-color: {a.BorderColor.Hex};" );
            }
        }

        if ( a.OverrideEffects )
        {
            var inset = a.BoxShadowInset ? " inset" : "";
            if ( baseForDiff is null
                 || !a.BoxShadowX.Equals( baseForDiff.BoxShadowX )
                 || !a.BoxShadowY.Equals( baseForDiff.BoxShadowY )
                 || !a.BoxShadowBlur.Equals( baseForDiff.BoxShadowBlur )
                 || !ColorsEqual( a.BoxShadowColor, baseForDiff.BoxShadowColor )
                 || a.BoxShadowInset != baseForDiff.BoxShadowInset )
                lines.Add( $"box-shadow: {a.BoxShadowX.ToCss()} {a.BoxShadowY.ToCss()} {a.BoxShadowBlur.ToCss()} {a.BoxShadowColor.Hex}{inset};" );
            if ( baseForDiff is null || a.Opacity != baseForDiff.Opacity )
                lines.Add( $"opacity: {a.Opacity.ToString( "0.##", CultureInfo.InvariantCulture )};" );
        }

        if ( a.OverrideConstraints )
        {
            if ( !isRoot && !a.Margin.IsAllAuto && !a.Margin.IsDefaultZero
                 && ( baseForDiff is null || a.Margin != baseForDiff.Margin ) )
                lines.Add( $"margin: {a.Margin.ToCss()};" );
            if ( a.MinWidth.Unit  != LengthUnit.Auto
                 && ( baseForDiff is null || !a.MinWidth.Equals( baseForDiff.MinWidth ) ) )
                lines.Add( $"min-width: {a.MinWidth.ToCss()};" );
            if ( a.MaxWidth.Unit  != LengthUnit.Auto
                 && ( baseForDiff is null || !a.MaxWidth.Equals( baseForDiff.MaxWidth ) ) )
                lines.Add( $"max-width: {a.MaxWidth.ToCss()};" );
            if ( a.MinHeight.Unit != LengthUnit.Auto
                 && ( baseForDiff is null || !a.MinHeight.Equals( baseForDiff.MinHeight ) ) )
                lines.Add( $"min-height: {a.MinHeight.ToCss()};" );
            if ( a.MaxHeight.Unit != LengthUnit.Auto
                 && ( baseForDiff is null || !a.MaxHeight.Equals( baseForDiff.MaxHeight ) ) )
                lines.Add( $"max-height: {a.MaxHeight.ToCss()};" );
        }

        if ( isCheckbox && checkboxSize.Unit != LengthUnit.Auto )
        {
            var glyphFontSize = new Length( checkboxSize.Value * 0.75f, checkboxSize.Unit );
            lines.Add( "> .checkmark {" );
            lines.Add( $"    width: {checkboxSize.ToCss()};" );
            lines.Add( $"    height: {checkboxSize.ToCss()};" );
            lines.Add( $"    font-size: {glyphFontSize.ToCss()};" );
            lines.Add( "}" );
        }

        // --- Interaction (gated by OverrideInteraction) ---
        if ( a.OverrideInteraction )
        {
            if ( baseForDiff is null || a.Cursor != baseForDiff.Cursor )
                lines.Add( $"cursor: {Css( a.Cursor )};" );
            if ( baseForDiff is null || a.Overflow != baseForDiff.Overflow )
                lines.Add( $"overflow: {Css( a.Overflow )};" );
            // Interaction extras (Tier 3 — grd-7t2z). Gated on non-default for byte-stability.
            if ( a.ZIndex != 0
                 && ( baseForDiff is null || a.ZIndex != baseForDiff.ZIndex ) )
                lines.Add( $"z-index: {a.ZIndex.ToString( CultureInfo.InvariantCulture )};" );
            if ( !a.PointerEvents
                 && ( baseForDiff is null || a.PointerEvents != baseForDiff.PointerEvents ) )
                lines.Add( "pointer-events: none;" );
        }

        return lines;
    }

    private static bool ColorsEqual( Color x, Color y ) =>
        string.Equals( x.Hex, y.Hex, StringComparison.Ordinal );
}
