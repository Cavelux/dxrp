using Sandbox;
using Editor;

namespace FieldGuide.Placement;

/// <summary>
/// Editor-assembly companion to <see cref="PlacementExport"/>, for the BULK export. The game side writes
/// both export files into FileSystem.Data during play (the panel's "Export all to Data" button, or
/// <see cref="PlacementExport.WriteAll"/>); these menu actions read the latest file back and copy the whole
/// thing to the system clipboard through <see cref="EditorUtility.Clipboard"/>.
///
/// Copying ONE target's bake line does not need any of this: the tweak panel's Copy button calls
/// <c>Sandbox.UI.Clipboard.SetText</c> from game code, in play. Use these menu actions when you want the
/// entire JSON array or the entire C# snippet rather than a single line.
/// </summary>
public static class PlacementExportTool
{
	[Menu( "Editor", "Field Guide/Copy Placement Export (JSON)", "content_copy" )]
	public static void CopyJson() => CopyToClipboard( PlacementExport.JsonFileName );

	[Menu( "Editor", "Field Guide/Copy Placement Export (C# snippet)", "content_copy" )]
	public static void CopyCSharp() => CopyToClipboard( PlacementExport.CSharpFileName );

	static void CopyToClipboard( string fileName )
	{
		if ( !Sandbox.FileSystem.Data.FileExists( fileName ) )
		{
			Log.Warning( $"[placement] no export at {Sandbox.FileSystem.Data.GetFullPath( fileName )}, run Export in play mode first." );
			return;
		}

		var text = Sandbox.FileSystem.Data.ReadAllText( fileName );
		EditorUtility.Clipboard.Copy( text );
		Log.Info( $"[placement] copied {fileName} ({text.Length} chars) to the clipboard." );
	}
}
