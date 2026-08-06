using Sandbox.UI;

namespace Goo;

// Non-nullable Length factory. Sandbox.UI.Length.Pixels returns Length? (CS0266 for 'static readonly Length x = Length.Pixels(16)'); Px.Of returns non-nullable so the explicit-unit factory works anywhere.
public static class Px
{
    public static Length Of(int pixels) => Length.Pixels(pixels) ?? default;
    public static Length Of(float pixels) => Length.Pixels(pixels) ?? default;
}
