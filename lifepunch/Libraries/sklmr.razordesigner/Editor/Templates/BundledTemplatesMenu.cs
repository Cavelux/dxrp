using System;
using System.IO;
using System.Text;
using Editor;
using Grains.RazorDesigner.Templates;

namespace Grains.RazorDesigner;

public static class BundledTemplatesMenu
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	[Menu( "Editor", "Razor Designer/Regenerate bundled templates", "auto_fix_high" )]
	public static void RegenerateBundledTemplates()
	{
		string dir;
		try
		{
			dir = PaletteTemplateStore.ResolveBundledTemplatesWriteDir();
		}
		catch ( Exception ex )
		{
			Log.Error( $"{LogPrefix} RegenerateBundledTemplates: resolve dir threw {ex.GetType().Name}: {ex.Message}" );
			EditorUtility.DisplayDialog( "Regenerate Bundled Templates — Error",
				$"Could not resolve the write directory:\n{ex.Message}", "Okay", "⚠️" );
			return;
		}

		if ( string.IsNullOrEmpty( dir ) )
		{
			EditorUtility.DisplayDialog( "Regenerate Bundled Templates — Error",
				"No loaded project matches a bundled-templates root Ident. Open the Razor Designer addon project and retry.",
				"Okay", "⚠️" );
			return;
		}

		var templates = BundledTemplateCatalog.All();
		var sb = new StringBuilder();
		int written = 0;

		try
		{
			Directory.CreateDirectory( dir );
			foreach ( var t in templates )
			{
				var fileName = SanitiseFilename( t.Name ) + ".json";
				var fullPath = Path.Combine( dir, fileName );
				var stamped = t with { FilePath = fullPath };
				File.WriteAllText( fullPath, PaletteTemplateSerializer.Serialize( stamped ) );
				Log.Info( $"{LogPrefix} bundled template written: {fileName}" );
				sb.AppendLine( fileName );
				written++;
			}
		}
		catch ( IOException ex )
		{
			Log.Error( $"{LogPrefix} RegenerateBundledTemplates: write failed after {written} file(s): {ex.Message}" );
			EditorUtility.DisplayDialog( "Regenerate Bundled Templates — Error",
				$"Wrote {written} file(s), then failed:\n{ex.Message}", "Okay", "⚠️" );
			return;
		}

		Log.Info( $"{LogPrefix} RegenerateBundledTemplates: {written} file(s) -> {dir}" );
		EditorUtility.DisplayDialog( $"Bundled Templates Regenerated — {written}",
			$"Wrote {written} file(s) to:\n{dir}\n\n{sb.ToString().TrimEnd()}\n\n" +
			"Commit the regenerated JSON alongside any BundledTemplateCatalog change. " +
			"Reopen the Razor Designer dock to see the refreshed Templates section.",
			"Okay", "✅" );
	}

	private static string SanitiseFilename( string name )
	{
		var trimmed = (name ?? "").Trim();
		if ( string.IsNullOrEmpty( trimmed ) ) return "Untitled";
		var invalid = Path.GetInvalidFileNameChars();
		var chars = trimmed.ToCharArray();
		for ( int i = 0; i < chars.Length; i++ )
			foreach ( var c in invalid )
				if ( chars[i] == c ) chars[i] = '_';
		var result = new string( chars ).Trim().TrimEnd( '.' );
		return string.IsNullOrEmpty( result ) ? "Untitled" : result;
	}
}
