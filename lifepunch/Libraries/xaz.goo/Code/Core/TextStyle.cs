using Sandbox;
using Sandbox.UI;

namespace Goo;

/// <summary>A bundle of font properties applied together via Text.Style; null fields are no-ops.</summary>
public readonly struct TextStyle
{
    public string? FontFamily { get; init; }
    public Length? FontSize   { get; init; }
    public int?    FontWeight { get; init; }
    public Color?  FontColor  { get; init; }

    internal StyleList ApplyTo(StyleList current)
    {
        var s = current;
        s = StyleAccumulator.Add(s, StyleField.FontFamily, FontFamily);
        s = StyleAccumulator.Add(s, StyleField.FontSize,   FontSize);
        s = StyleAccumulator.Add(s, StyleField.FontWeight, FontWeight);
        s = StyleAccumulator.Add(s, StyleField.FontColor,  FontColor, StyleValue.FromColor);
        return s;
    }
}
