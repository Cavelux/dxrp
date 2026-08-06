// Handwritten pair shorthands; not part of the emitted facade or the style manifest.
using Sandbox;
using Sandbox.UI;

namespace Goo;

public readonly partial record struct Container
{
    /// <summary>Sets PaddingLeft and PaddingRight to the same value. The style list is append-only; a PaddingLeft declared after this shorthand overrides it for that edge.</summary>
    public Length? PaddingX
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.PaddingLeft,  value );
            _style = StyleAccumulator.Add( _style, StyleField.PaddingRight, value );
        }
    }

    /// <summary>Sets PaddingTop and PaddingBottom to the same value. The style list is append-only; a PaddingTop or PaddingBottom declared after this shorthand overrides it for that edge.</summary>
    public Length? PaddingY
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.PaddingTop,    value );
            _style = StyleAccumulator.Add( _style, StyleField.PaddingBottom, value );
        }
    }

    /// <summary>Sets MarginLeft and MarginRight to the same value. The style list is append-only; a MarginLeft declared after this shorthand overrides it for that edge.</summary>
    public Length? MarginX
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.MarginLeft,  value );
            _style = StyleAccumulator.Add( _style, StyleField.MarginRight, value );
        }
    }

    /// <summary>Sets MarginTop and MarginBottom to the same value. The style list is append-only; a MarginTop or MarginBottom declared after this shorthand overrides it for that edge.</summary>
    public Length? MarginY
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.MarginTop,    value );
            _style = StyleAccumulator.Add( _style, StyleField.MarginBottom, value );
        }
    }

    /// <summary>Sets BorderTopLeftRadius and BorderTopRightRadius to the same value. The style list is append-only; a per-corner property declared after this shorthand overrides it for that corner.</summary>
    public Length? BorderTopRadius
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.BorderTopLeftRadius,  value );
            _style = StyleAccumulator.Add( _style, StyleField.BorderTopRightRadius, value );
        }
    }

    /// <summary>Sets BorderBottomLeftRadius and BorderBottomRightRadius to the same value. The style list is append-only; a per-corner property declared after this shorthand overrides it for that corner.</summary>
    public Length? BorderBottomRadius
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.BorderBottomLeftRadius,  value );
            _style = StyleAccumulator.Add( _style, StyleField.BorderBottomRightRadius, value );
        }
    }

    /// <summary>Sets BorderTopLeftRadius and BorderBottomLeftRadius to the same value. The style list is append-only; a per-corner property declared after this shorthand overrides it for that corner.</summary>
    public Length? BorderLeftRadius
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.BorderTopLeftRadius,    value );
            _style = StyleAccumulator.Add( _style, StyleField.BorderBottomLeftRadius, value );
        }
    }

    /// <summary>Sets BorderTopRightRadius and BorderBottomRightRadius to the same value. The style list is append-only; a per-corner property declared after this shorthand overrides it for that corner.</summary>
    public Length? BorderRightRadius
    {
        init
        {
            _style = StyleAccumulator.Add( _style, StyleField.BorderTopRightRadius,    value );
            _style = StyleAccumulator.Add( _style, StyleField.BorderBottomRightRadius, value );
        }
    }

    // (Precisely: multiplies resolved opacity by DisabledOpacity; the Applier applies
    // the overrides after resolving all other declared fields. Single-line so BlobSource parses it.)
    /// <summary>Forces PointerEvents to None and dims the resolved opacity while true. Applied after all other declared fields, so per-property PointerEvents and Opacity declarations cannot override it.</summary>
    public bool? Disabled { init => _style = StyleAccumulator.Add( _style, StyleField.Disabled, value ); }

    // Opacity multiplier applied to a Container when Disabled = true.
    public const float DisabledOpacity = 0.45f;

    // Canonical disabled dimming; multiplies any declared opacity. Applied by the Applier.
    internal static float ResolveDisabledOpacity( float? declaredOpacity )
        => (declaredOpacity ?? 1f) * DisabledOpacity;
}
