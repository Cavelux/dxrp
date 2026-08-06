using System;
using System.Collections.Generic;
using Goo.Internal;
using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>Factories for composite shape subtrees. Rotation-applying helpers default <c>PointerEvents.None</c> so rotated descendants do not drift parent hit dispatch.</summary>
public static class Shapes
{
    /// <summary>N <see cref="Sector"/> wedges clockwise from 12 o'clock; the subtree is <c>PointerEvents.None</c> so parent dispatchers see the unrotated frame.</summary>
    public static Container Ring(int segments, float innerRadius, ReadOnlySpan<Color> colors,
        float outerRadius = 1.0f, string? key = null)
    {
        if (segments <= 0)
            throw new ArgumentException("segments must be > 0", nameof(segments));
        if (colors.Length != segments)
            throw new ArgumentException($"colors.Length ({colors.Length}) must equal segments ({segments})", nameof(colors));

        var outer = new Container
        {
            Key           = key,
            Position      = PositionMode.Relative,
            Width         = Length.Percent(100),
            Height        = Length.Percent(100),
            FlexShrink    = 0, // a flex-squeezed non-square box renders the rotated wedges as a broken spiral
            PointerEvents = PointerEvents.None,
        };

        float wedge = 360f / segments;
        float halfWedge = 0.5f * wedge;
        for (int i = 0; i < segments; i++)
        {
            float rot = i * wedge - halfWedge;
            var wrapper = new Container
            {
                Key           = $"ring-slot-{i}",
                Position      = PositionMode.Absolute,
                Top           = 0,
                Left          = 0,
                Width         = Length.Percent(100),
                Height        = Length.Percent(100),
                PointerEvents = PointerEvents.None,
                Transform     = Goo.PanelTransform.Rotate(rot),
            };
            wrapper.Children.Add(new Sector
            {
                Width           = Length.Percent(100),
                Height          = Length.Percent(100),
                StartAngle      = 0f,
                EndAngle        = wedge,
                InnerRadius     = innerRadius,
                OuterRadius     = outerRadius,
                BackgroundColor = colors[i],
            });
            outer.Children.Add(wrapper);
        }
        return outer;
    }

    /// <summary>Color-array overload; forwards to the <see cref="ReadOnlySpan{T}"/> form. Prefer the span overload from animated <c>Build()</c> bodies to avoid per-frame allocation.</summary>
    public static Container Ring(int segments, float innerRadius, Color[] colors,
        float outerRadius = 1.0f, string? key = null)
    {
        if (colors is null)
            throw new ArgumentException("colors must not be null", nameof(colors));
        return Ring(segments, innerRadius, (ReadOnlySpan<Color>)colors, outerRadius, key);
    }

    /// <summary>A pre-rounded circular <see cref="Container"/> (50% radius, 100% size) that drops into a flex slot.</summary>
    public static Container Disc(string? key = null)
        => new Container
        {
            Key          = key,
            Width        = Length.Percent(100),
            Height       = Length.Percent(100),
            BorderRadius = Length.Percent(50),
        };

    /// <summary>A filled pill/stadium (50% radius, 100% size). Native <c>BorderRadius</c>, so the engine
    /// hit-tests the same rounded outline it draws: interactive for free, no guard needed.</summary>
    public static Container Pill(Color color, string? key = null) => new Container
    {
        Key = key, BackgroundColor = color, BorderRadius = Length.Percent(50),
        Width = Length.Percent(100), Height = Length.Percent(100),
    };

    /// <summary>A filled rounded rectangle (100% size). Native <c>BorderRadius</c>, hit-test matches the
    /// drawn outline: interactive for free, no guard needed.</summary>
    public static Container RoundedRect(Color color, Length radius, string? key = null) => new Container
    {
        Key = key, BackgroundColor = color, BorderRadius = radius,
        Width = Length.Percent(100), Height = Length.Percent(100),
    };

    /// <summary>A filled regular polygon (a vertex points up) at 100% size, drawn by ui_shape.shader.
    /// Decorate the returned blob with BackgroundColor / HoverBackgroundColor / On* like any shape.</summary>
    public static Polygon Ngon(int sides, float rotation = 0f, string? key = null)
        => new Polygon { Points = ShapeVerts.Ngon(sides, rotation), Key = key };

    /// <summary>A filled star with <paramref name="points"/> points (one up). <paramref name="innerRadius"/> (0..1) is the valley radius.</summary>
    public static Polygon Star(int points, float innerRadius = 0.45f, float rotation = 0f, string? key = null)
        => new Polygon { Points = ShapeVerts.Star(points, innerRadius, rotation), Key = key };

    /// <summary>A wedge menu: <see cref="Ring"/> visuals on one circular host whose <see cref="RadialDispatcher"/>
    /// resolves the hovered/clicked slot. The center hole and outside-the-ring are inert. Keep it in a square slot.</summary>
    public static CellElement RadialMenu(int segments, float innerRadius, IReadOnlyList<Color> colors,
        Color hoverColor, Action<int> onSlot, float outerRadius = 1f, string? key = null)
        => Cell.Mount<RadialMenuCell>(
            key: key ?? "radial-menu",
            seed: c => { c.Segments = segments; c.InnerRadius = innerRadius; c.OuterRadius = outerRadius; },
            configure: c => { c.Colors = colors; c.HoverColor = hoverColor; c.OnSlot = onSlot; });
}

internal sealed class RadialMenuCell : Cell<Container>
{
    public int Segments;
    public float InnerRadius = 0.4f, OuterRadius = 1f;
    public IReadOnlyList<Color> Colors = Array.Empty<Color>();
    public Color HoverColor = Color.White;
    public Action<int>? OnSlot;

    int _hovered = -1;
    RadialDispatcher? _dispatcher;

    protected override Container Build()
    {
        _dispatcher ??= RadialDispatcher.Centered(Segments, InnerRadius, OuterRadius);

        Span<Color> wedge = stackalloc Color[Segments];
        for (int i = 0; i < Segments; i++)
            wedge[i] = i == _hovered ? HoverColor : (i < Colors.Count ? Colors[i] : Color.Gray);

        return new Container
        {
            Position = PositionMode.Relative,
            Width = Length.Percent(100), Height = Length.Percent(100),
            Children =
            {
                // visuals (pointer-through)
                Shapes.Ring(Segments, InnerRadius, wedge, OuterRadius, key: "wedges"),
                // one circular host: native circle hit-test = "on the ring", dispatcher = "which wedge"
                new Container
                {
                    Key = "hit",
                    Position = PositionMode.Absolute, Top = 0, Left = 0,
                    Width = Length.Percent(100), Height = Length.Percent(100),
                    BorderRadius = Length.Percent(50),
                    PointerEvents = PointerEvents.All,
                    OnMouseMove = e => UpdateHover(e),
                    OnMouseLeave = _ => { if (_hovered != -1) { _hovered = -1; Rebuild(); } },
                    OnClick = e =>
                    {
                        var slot = SlotAt(e);
                        if (slot is int s) OnSlot?.Invoke(s);
                    },
                },
            },
        };
    }

    int? SlotAt(MousePanelEvent e)
    {
        var size = e.Target.Box.Rect.Size;
        if (size.x <= 0f || size.y <= 0f) return null;
        var uv = new Vector2(e.LocalPosition.x / size.x, e.LocalPosition.y / size.y);
        return _dispatcher!.SlotAt(uv);
    }

    void UpdateHover(MousePanelEvent e)
    {
        int next = SlotAt(e) ?? -1;
        if (next != _hovered) { _hovered = next; Rebuild(); } // rebuild only on slot change, not per move
    }
}
