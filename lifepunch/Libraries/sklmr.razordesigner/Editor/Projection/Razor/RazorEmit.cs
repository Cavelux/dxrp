namespace Grains.RazorDesigner.Projection.Razor;

public static class RazorEmit
{
    public static string Attr( string name, string value ) => $"{name}=\"{Escape.Html( value )}\"";
}
