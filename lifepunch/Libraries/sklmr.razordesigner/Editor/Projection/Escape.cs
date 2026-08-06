namespace Grains.RazorDesigner.Projection;

public static class Escape
{
    public static string Html( string s )
    {
        if ( string.IsNullOrEmpty( s ) ) return "";
        return s
            .Replace( "&", "&amp;" )
            .Replace( "<", "&lt;" )
            .Replace( ">", "&gt;" )
            .Replace( "\"", "&quot;" );
    }
}
