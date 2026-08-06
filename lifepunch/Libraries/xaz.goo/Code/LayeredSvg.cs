using System;
using Sandbox.UI;

namespace Goo;

public readonly record struct SvgLayer
{
    public string?         Path      { get; init; }
    public string?         Color     { get; init; }
    public string?         Key       { get; init; }
    public PanelTransform? Transform { get; init; }
    public int?            ZIndex    { get; init; }

    public Action<MousePanelEvent>? OnClick      { get; init; }
    public Action<MousePanelEvent>? OnMouseEnter { get; init; }
    public Action<MousePanelEvent>? OnMouseLeave { get; init; }
    public Action<MousePanelEvent>? OnMouseDown  { get; init; }
    public Action<MousePanelEvent>? OnMouseUp    { get; init; }
    public Action<MousePanelEvent>? OnMouseMove  { get; init; }
}

public static class LayeredSvg
{
    public static Container Of(float width, float height, params SvgLayer[] layers)
    {
        var root = new Container
        {
            Width = width,
            Height = height,
            Position = PositionMode.Relative,
        };
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            root.Children.Add(new Container
            {
                Width = width,
                Height = height,
                Position = PositionMode.Absolute,
                ZIndex = layer.ZIndex ?? i,
                Transform = layer.Transform,
                Children =
                {
                    new SvgPanel
                    {
                        Path         = layer.Path,
                        Color        = layer.Color,
                        Key          = layer.Key,
                        Width        = width,
                        Height       = height,
                        OnClick      = layer.OnClick,
                        OnMouseEnter = layer.OnMouseEnter,
                        OnMouseLeave = layer.OnMouseLeave,
                        OnMouseDown  = layer.OnMouseDown,
                        OnMouseUp    = layer.OnMouseUp,
                        OnMouseMove  = layer.OnMouseMove,
                    },
                },
            });
        }
        return root;
    }
}
