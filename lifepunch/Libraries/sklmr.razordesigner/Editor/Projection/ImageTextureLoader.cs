
using Editor; // EditorUtility.Projects.GetAll() — matches LiveTreeMirror.cs source
using Sandbox;

namespace Grains.RazorDesigner.Projection;

public static class ImageTextureLoader
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    public static Texture Load( string source )
    {
        if ( string.IsNullOrEmpty( source ) ) return null;
        try
        {
            var abs = ResolveImagePath( source );
            if ( string.IsNullOrEmpty( abs ) )
            {
                Log.Warning( $"{LogPrefix} Image source not found in Project.Current or any loaded library Assets/: \"{source}\"" );
                return null;
            }

            using var bm = Bitmap.CreateFromBytes( System.IO.File.ReadAllBytes( abs ) );
            return bm?.ToTexture();
        }
        catch ( System.Exception e )
        {
            Log.Warning( e, $"{LogPrefix} LoadImageTexture failed for \"{source}\": {e.Message}" );
            return null;
        }
    }

    public static string ResolveImagePath( string source )
    {
        var primary = Project.Current?.GetAssetsPath();
        if ( !string.IsNullOrEmpty( primary ) )
        {
            var abs = System.IO.Path.GetFullPath( source, primary );
            if ( System.IO.File.Exists( abs ) ) return abs;
        }

        foreach ( var p in EditorUtility.Projects.GetAll() )
        {
            if ( p == Project.Current ) continue;
            var assets = p.GetAssetsPath();
            if ( string.IsNullOrEmpty( assets ) || !System.IO.Directory.Exists( assets ) ) continue;
            var abs = System.IO.Path.GetFullPath( source, assets );
            if ( System.IO.File.Exists( abs ) ) return abs;
        }

        return null;
    }
}
