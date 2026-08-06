namespace Goo;

// Hand-written partial: the Style bundle property is not part of the generated facade.
public readonly partial record struct Text
{
    /// <summary>Applies a font preset (FontFamily, FontSize, FontWeight, FontColor) in one property. Last-declared wins: a per-field override placed after Style overrides the preset; a field set before Style is clobbered by it.</summary>
    public TextStyle Style { init => _style = value.ApplyTo(_style); }
}
