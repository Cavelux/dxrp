using System;
using Sandbox;
using Sandbox.UI;

namespace Goo;

// Factories for full-bleed HUD/overlay roots (absolute, full-screen, Column, pointer-events off).
public static class Hud
{
    /// <summary>Full-screen HUD root: Column layout, pointer-events off. For shader overlays use Hud.Fill().</summary>
    public static Container Overlay() => new()
    {
        Position      = PositionMode.Absolute,
        Top           = 0,
        Left          = 0,
        Width         = Length.Percent( 100 ),
        Height        = Length.Percent( 100 ),
        FlexDirection = FlexDirection.Column,
        PointerEvents = PointerEvents.None,
    };

    /// <summary>Absolute, 100% x 100%, pointer-through fill layer. Resolves inside the nearest positioned ancestor; add Position=Relative to that ancestor if needed. Override any field it sets, including PointerEvents, with a <c>with</c> expression (last-declared wins).</summary>
    public static Container Fill() => new()
    {
        Position      = PositionMode.Absolute,
        Top           = 0,
        Left          = 0,
        Width         = Length.Percent( 100 ),
        Height        = Length.Percent( 100 ),
        PointerEvents = PointerEvents.None,
    };

    /// <summary>Absolute, all four edges pinned to zero. Assign PointerEvents explicitly - scrims typically toggle between None and All on open/close. The factory does not set PointerEvents.</summary>
    public static Container Scrim( Color? color = null ) => new()
    {
        Position        = PositionMode.Absolute,
        Top             = 0,
        Left            = 0,
        Right           = 0,
        Bottom          = 0,
        BackgroundColor = color,
    };

    /// <summary>Full-bleed absolute image layer. Pass texture=null to load by path; path is ignored when texture is non-null. Override any field it sets with a <c>with</c> expression (last-declared wins). Must be called from within Build().</summary>
    public static Container Wallpaper( Texture? texture, string? path )
    {
        var c = Fill();
        c.Children.Add( new Image
        {
            Width      = Length.Percent( 100 ),
            Height     = Length.Percent( 100 ),
            FlexShrink = 0,
            Texture    = texture,
            Path       = texture is null ? path : null,
            ObjectFit  = ObjectFit.Fill,
        } );
        return c;
    }

    /// <summary>Full-bleed pointer-through cell that pins content to an anchor corner. Wrapper is always FlexDirection.Column; ResolveAnchor's ColumnReverse belongs on an inner stacking column, never here (top anchors would render at the bottom).</summary>
    public static Container Anchored( Layout.Anchor anchor, Container content, Length? padding = default, string? key = null )
    {
        var r = Layout.ResolveAnchor( anchor );
        return new Container
        {
            Key            = key,
            Position       = PositionMode.Absolute,
            Top            = 0,
            Left           = 0,
            Width          = Length.Percent( 100 ),
            Height         = Length.Percent( 100 ),
            Padding        = padding,
            PointerEvents  = PointerEvents.None,
            FlexDirection  = FlexDirection.Column,
            JustifyContent = r.JustifyContent,
            AlignItems     = r.AlignItems,
            Children       = { content },
        };
    }

    /// <summary>Scroll viewport: Column layout, OverflowY = Scroll, pointer events on. The engine needs all three of a bounded size on the scroll axis, OverflowY = Scroll, and PointerEvents = All or the viewport looks inert; pass <paramref name="height"/> to bound the axis here, or omit it and bound the panel from its parent.</summary>
    public static Container ScrollArea( Length? height = null ) => new()
    {
        Height        = height,
        FlexDirection = FlexDirection.Column,
        OverflowY     = OverflowMode.Scroll,
        PointerEvents = PointerEvents.All,
    };

    /// <summary>Invisible full-bleed click-catcher: covers the nearest positioned ancestor, swallows the click, and invokes <paramref name="onDismiss"/>. Draw dismissable content after it in the sibling order (or give it SwallowClick, rule of thumb for anything clickable above a scrim).</summary>
    public static Container DismissScrim( Action onDismiss, Color? color = null ) => Scrim( color ) with
    {
        PointerEvents = PointerEvents.All,
        SwallowClick  = true,
        OnClick       = _ => onDismiss(),
    };

    /// <summary>Absolute left-anchored fill bar: 100% height, width = <paramref name="frac"/> (clamped 0..1) of the parent. The parent supplies the track: give it Position = Relative, a bounded size, and the track background.</summary>
    public static Container FillRect( float frac, Color color ) => new()
    {
        Position        = PositionMode.Absolute,
        Top             = 0,
        Left            = 0,
        Height          = Length.Percent( 100 ),
        Width           = Length.Percent( frac.Clamp( 0f, 1f ) * 100f ),
        BackgroundColor = color,
    };

    /// <summary>A flex-grow spacer that pushes siblings apart. Sets only FlexGrow = 1; all other fields are caller-controlled.</summary>
    public static Container Spacer() => new() { FlexGrow = 1 };

    /// <summary>A 1px full-width horizontal rule. Defaults to a low-alpha white line; pass a color to override. Sets Height, Width, and BackgroundColor.</summary>
    public static Container Divider( Color? color = null ) => new()
    {
        Height          = Px.Of( 1 ),
        Width           = Length.Percent( 100 ),
        // 8 percent white: legible on dark glass and solid dark backgrounds, tuned visually.
        BackgroundColor = color ?? new Color( 1f, 1f, 1f, 0.08f ),
    };

    /// <summary>Self-driving particle overlay: absolute, full-screen, pointer-through. The <paramref name="field"/>
    /// steps itself in the paint callback (a Draw-carrying panel self-dirties every frame, so no Tick is needed);
    /// spawn into it from any handler. Pass <paramref name="draw"/> for a one-off look, or omit it to use the field's own <see cref="ParticleField.Draw"/>.</summary>
    public static Container Particles( ParticleField field, ParticleDraw? draw = null ) => new()
    {
        Position      = PositionMode.Absolute,
        Top           = 0,
        Left          = 0,
        Width         = Length.Percent( 100 ),
        Height        = Length.Percent( 100 ),
        PointerEvents = PointerEvents.None,
        Draw          = c =>
        {
            field.Step( Time.Delta );
            var look = draw ?? field.Draw;
            foreach ( var p in field.Live ) look( c, p );
        },
    };
}
