using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox;
using Editor;

namespace FieldGuide.Tips;

/// <summary>
/// The editor half of the Tips Studio's bake: it turns tips STAGED from a play session into real
/// <c>.tip</c> assets in the project.
///
/// Two halves because they live in different places. Game code cannot write into a project's
/// <c>Assets/</c> folder, so the Studio's Stage button writes the finished <c>.tip</c> JSON into
/// <c>FileSystem.Data/fieldguide_tips_studio/</c> instead. These menu actions read that folder and write the
/// files where the editor will compile them. Going through a file rather than a live static also means the
/// play session can be over by the time you land the assets, which is the same shape as the placement kit's
/// export tool.
///
/// WHERE THEY LAND: <c>&lt;the current project&gt;/Assets/tips/</c>. In a game that vendors this kit, that is
/// the game's own Assets folder, which is what you want, the tips belong to the game, not to the library. Open
/// the libraries host project and it lands in the host, not in <c>Libraries/fieldguide.tips/Assets</c>.
///
/// Nothing here overwrites by accident: the plain action skips a tip whose file already exists and says so,
/// and there is a separate action that overwrites on purpose.
/// </summary>
public static class TipsStudioExportTool
{
	/// <summary>The folder inside the project's Assets that baked tips land in.</summary>
	private const string TargetFolder = "tips";

	[Menu( "Editor", "Field Guide/Write staged tips to Assets/tips", "note_add" )]
	public static void WriteStaged() => Write( overwrite: false );

	[Menu( "Editor", "Field Guide/Write staged tips to Assets/tips (overwrite existing)", "edit_note" )]
	public static void WriteStagedOverwriting() => Write( overwrite: true );

	[Menu( "Editor", "Field Guide/Open the staged tips folder", "folder_open" )]
	public static void OpenStagedFolder()
	{
		var staged = StagedPath();
		if ( staged is null )
		{
			Log.Warning( "[tips] nothing staged yet. Bake a tip from the Tips Studio in play mode first." );
			return;
		}

		EditorUtility.OpenFolder( staged );
	}

	private static void Write( bool overwrite )
	{
		var project = Project.Current;
		if ( project is null )
		{
			Log.Warning( "[tips] no project is open, so there is nowhere to write the tips." );
			return;
		}

		var staged = StagedPath();
		if ( staged is null )
		{
			Log.Warning( "[tips] nothing staged. In play mode, open the Tips Studio, author a tip and press " +
				"Stage for the editor." );
			return;
		}

		var files = Directory.GetFiles( staged, "*.tip" )
			.OrderBy( p => p, StringComparer.Ordinal )
			.ToList();

		if ( files.Count == 0 )
		{
			Log.Warning( $"[tips] the staging folder is empty ({staged})." );
			return;
		}

		string assetsRoot;
		try
		{
			assetsRoot = project.GetAssetsPath();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[tips] could not find the project's Assets folder ({e.Message})." );
			return;
		}

		if ( string.IsNullOrEmpty( assetsRoot ) )
		{
			Log.Warning( "[tips] the project reports no Assets folder." );
			return;
		}

		var target = Path.Combine( assetsRoot, TargetFolder );
		Directory.CreateDirectory( target );

		var written = new List<string>();
		var skipped = new List<string>();

		foreach ( var source in files )
		{
			var name = Path.GetFileName( source );
			var destination = Path.Combine( target, name );

			if ( File.Exists( destination ) && !overwrite )
			{
				skipped.Add( name );
				continue;
			}

			try
			{
				File.WriteAllText( destination, File.ReadAllText( source ) );
				written.Add( name );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[tips] could not write {name} ({e.Message})." );
			}
		}

		if ( written.Count > 0 )
			Log.Info( $"[tips] wrote {written.Count} tip(s) to {target}: {string.Join( ", ", written )}" );

		if ( skipped.Count > 0 )
			Log.Warning( $"[tips] skipped {skipped.Count} tip(s) that already exist: {string.Join( ", ", skipped )}. " +
				"Use \"Write staged tips (overwrite existing)\" if you meant to replace them." );

		if ( written.Count == 0 && skipped.Count == 0 )
			Log.Warning( "[tips] nothing was written." );
	}

	/// <summary>The absolute path of the Studio's staging folder, or null when it does not exist yet.</summary>
	private static string StagedPath()
	{
		try
		{
			if ( !Sandbox.FileSystem.Data.DirectoryExists( TipsStudio.StageFolder ) )
				return null;

			var full = Sandbox.FileSystem.Data.GetFullPath( TipsStudio.StageFolder );
			return Directory.Exists( full ) ? full : null;
		}
		catch ( Exception )
		{
			return null;
		}
	}
}
