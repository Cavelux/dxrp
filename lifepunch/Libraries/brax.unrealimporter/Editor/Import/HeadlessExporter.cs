using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;

namespace Editor.UnrealImporter;

public class ExportResult
{
	public bool Success;
	public string StagingDir;
	public string ManifestPath;
	public string Error;
}

/// <summary>A progress signal parsed out of the live Unreal log stream.</summary>
/// <param name="Done">Meshes exported so far, when the line carried a count.</param>
/// <param name="Total">Total meshes to export, when known.</param>
/// <param name="Message">Human-readable phase/state line.</param>
public record ExportEvent( int? Done, int? Total, string Message );

/// <summary>
/// Drives Tools/ue_export.py inside headless Unreal (UnrealEditor-Cmd) to turn selected
/// .uasset StaticMeshes into FBX + PNG + manifest.json in a staging folder.
/// </summary>
public static class HeadlessExporter
{
	/// <summary>Find ue_export.py shipped in this library's Tools folder.</summary>
	public static string FindExportScript()
	{
		var root = Sandbox.Project.Current?.GetRootPath();
		if ( !string.IsNullOrEmpty( root ) )
		{
			var direct = Path.Combine( root, "Libraries", "unrealimporter", "Tools", "ue_export.py" );
			if ( File.Exists( direct ) )
				return direct;

			var hit = Directory.EnumerateFiles( root, "ue_export.py", SearchOption.AllDirectories ).FirstOrDefault();
			if ( hit != null )
				return hit;
		}

		return null;
	}

	/// <summary>Convert a Content-relative .uasset file path to a /Game object path.</summary>
	/// <example>.../Content/Construction_VOL1/Meshes/SM_Boxes_01a.uasset -> /Game/Construction_VOL1/Meshes/SM_Boxes_01a</example>
	public static string ToGamePath( string uprojectFolder, string uassetAbsPath )
	{
		var content = Path.Combine( uprojectFolder, "Content" );
		var rel = Path.GetRelativePath( content, uassetAbsPath ).Replace( '\\', '/' );
		if ( rel.EndsWith( ".uasset", StringComparison.OrdinalIgnoreCase ) )
			rel = rel[..^".uasset".Length];

		return "/Game/" + rel;
	}

	/// <param name="mapGamePath">When set, scene mode: export this .umap's placements plus every mesh it uses (gameAssetPaths is ignored by the script).</param>
	/// <param name="onProgress">
	/// Live progress parsed by tailing the -abslog file. Unreal's stdout only carries
	/// Display+ severity (verified: our script's Log-verbosity lines never appear there,
	/// with or without -stdout), but the log FILE gets every line. Invoked on the calling
	/// thread's context.
	/// </param>
	public static async Task<ExportResult> Run( string editorCmd, string uprojectPath, IEnumerable<string> gameAssetPaths, string scriptPath, CancellationToken progressToken, string mapGamePath = null, Action<ExportEvent> onProgress = null )
	{
		var result = new ExportResult();

		// Marketplace packs often force-enable plugins that no longer ship with the engine
		// (NVIDIA Ansel is the classic) - Unreal hard-fatals on those at boot. Launch a
		// sanitized temp .uproject with the missing ones marked Optional instead.
		//
		// This walks the whole engine + project plugin trees looking for .uplugin files, which
		// is seconds of disk work on a big install - off the UI thread, and announced, or it
		// reads as the editor hanging before anything has even started.
		string tempUproject = null;
		onProgress?.Invoke( new ExportEvent( null, null, "Checking project plugins..." ) );
		try
		{
			var sanitized = await Task.Run( () =>
			{
				var path = SanitizeUproject( editorCmd, uprojectPath, out var temp );
				return (path, temp);
			}, progressToken );

			uprojectPath = sanitized.path;
			tempUproject = sanitized.temp;
		}
		catch ( Exception e )
		{
			Log.Warning( $"uproject plugin check failed, launching unmodified: {e.Message}" );
		}

		try
		{

		var stagingDir = Path.Combine( Path.GetTempPath(), "unrealimporter", Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( stagingDir );
		result.StagingDir = stagingDir;

		// Pass the selection via a file (env-var/command-line length is limited).
		var assetsFile = Path.Combine( stagingDir, "_assets.txt" );
		await File.WriteAllLinesAsync( assetsFile, gameAssetPaths, progressToken );

		var logPath = Path.Combine( stagingDir, "ue_export.log" );

		// NOTE: -script must use forward slashes; a backslash before u/r/etc. is eaten as a python escape.
		// PCG ships with the engine since 5.2 - without it, PCG-scattered actors in World Partition
		// maps fail to deserialize ("Invalid actor native class") and their geometry is lost.
		var script = scriptPath.Replace( '\\', '/' );
		var args =
			$"\"{uprojectPath}\" -run=pythonscript -script=\"{script}\" " +
			$"-EnablePlugins=PythonScriptPlugin,PCG -unattended -nosplash -nullrhi -abslog=\"{logPath}\"";

		var psi = new ProcessStartInfo
		{
			FileName = editorCmd,
			Arguments = args,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		psi.EnvironmentVariables["UE_EXPORT_OUT"] = stagingDir;
		psi.EnvironmentVariables["UE_EXPORT_ASSETS_FILE"] = assetsFile;
		if ( !string.IsNullOrEmpty( mapGamePath ) )
			psi.EnvironmentVariables["UE_EXPORT_MAP"] = mapGamePath;

		try
		{
			using var proc = Process.Start( psi );

			// Cancelling the progress section actually stops Unreal rather than orphaning it.
			using var killOnCancel = progressToken.Register( () =>
			{
				try { proc.Kill( entireProcessTree: true ); }
				catch { }
			} );

			// TailLog owns the status line from here - it emits immediately and then keeps a
			// heartbeat going, so there's no silent gap to fill in.
			var tail = TailLog( proc, logPath, onProgress );

			try
			{
				await proc.WaitForExitAsync( progressToken );
			}
			catch ( OperationCanceledException )
			{
				// killOnCancel is stopping Unreal; fall through so the tail loop winds down.
			}

			await tail;

			result.ManifestPath = Path.Combine( stagingDir, "manifest.json" );
			if ( proc.ExitCode != 0 )
			{
				result.Error = progressToken.IsCancellationRequested
					? "Export cancelled."
					: $"UnrealEditor-Cmd exited with code {proc.ExitCode}. See log:\n{logPath}";
				return result;
			}

			if ( !File.Exists( result.ManifestPath ) )
			{
				result.Error = $"Export finished but no manifest.json was produced. See log:\n{logPath}";
				return result;
			}

			result.Success = true;
			return result;
		}
		catch ( Exception e )
		{
			result.Error = e.Message;
			return result;
		}

		}
		finally
		{
			if ( tempUproject is not null )
			{
				try { File.Delete( tempUproject ); }
				catch { }
			}
		}
	}

	static readonly Dictionary<string, HashSet<string>> pluginScanCache = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Names of every .uplugin discoverable under a directory (cached - engine trees are big).</summary>
	static HashSet<string> AvailablePlugins( string dir )
	{
		if ( pluginScanCache.TryGetValue( dir, out var cached ) )
			return cached;

		var set = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		if ( Directory.Exists( dir ) )
		{
			foreach ( var f in Directory.EnumerateFiles( dir, "*.uplugin", SearchOption.AllDirectories ) )
				set.Add( Path.GetFileNameWithoutExtension( f ) );
		}

		pluginScanCache[dir] = set;
		return set;
	}

	/// <summary>
	/// If the .uproject enables plugins that exist neither in the engine nor the project,
	/// write a sibling temp .uproject with those entries marked Optional (Unreal skips
	/// missing optional plugins instead of aborting) and return its path. Returns the
	/// original path untouched when everything resolves. Caller deletes the temp file.
	/// </summary>
	static string SanitizeUproject( string editorCmd, string uprojectPath, out string tempUproject )
	{
		tempUproject = null;

		var root = System.Text.Json.Nodes.JsonNode.Parse( File.ReadAllText( uprojectPath ) );
		if ( root?["Plugins"] is not System.Text.Json.Nodes.JsonArray plugins || plugins.Count == 0 )
			return uprojectPath;

		var enabled = plugins
			.Where( p => p?["Enabled"]?.GetValue<bool>() == true )
			.Select( p => p?["Name"]?.GetValue<string>() )
			.Where( n => !string.IsNullOrEmpty( n ) )
			.ToList();
		if ( enabled.Count == 0 )
			return uprojectPath;

		// editorCmd = <root>/Engine/Binaries/Win64/UnrealEditor-Cmd.exe
		var enginePlugins = Path.GetFullPath( Path.Combine( Path.GetDirectoryName( editorCmd ), "..", "..", "Plugins" ) );
		var projFolder = Path.GetDirectoryName( uprojectPath );

		var missing = enabled
			.Where( n => !AvailablePlugins( enginePlugins ).Contains( n )
				&& !AvailablePlugins( Path.Combine( projFolder, "Plugins" ) ).Contains( n )
				&& !AvailablePlugins( Path.Combine( projFolder, "Mods" ) ).Contains( n ) )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );
		if ( missing.Count == 0 )
			return uprojectPath;

		Log.Info( $"uproject enables plugin(s) missing from this engine: {string.Join( ", ", missing )} - marking Optional for the export run." );

		foreach ( var p in plugins )
		{
			if ( p?["Name"]?.GetValue<string>() is string name && missing.Contains( name ) )
				p["Optional"] = true;
		}

		// Same folder, so Content/ and /Game paths resolve identically.
		tempUproject = Path.Combine( projFolder, Path.GetFileNameWithoutExtension( uprojectPath ) + ".sboximport.uproject" );
		File.WriteAllText( tempUproject, root.ToJsonString( new System.Text.Json.JsonSerializerOptions { WriteIndented = true } ) );
		return tempUproject;
	}

	/// <summary>How long a phase may go without an update before its elapsed time is re-emitted.</summary>
	static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds( 2 );

	/// <summary>
	/// Coarse phases recognised in Unreal's OWN boot log. Booting a marketplace project
	/// headlessly is a thousand-odd log lines and a minute-plus of wall clock before our
	/// script gets a word in, and reporting nothing through it is indistinguishable from a
	/// hang. Ordered earliest-to-latest and matched monotonically (a phase never goes
	/// backwards), because the categories interleave freely.
	/// </summary>
	static readonly (string Marker, string Phase)[] BootPhases =
	{
		( "LogInit", "Unreal starting up" ),
		( "LogPluginManager: Mounting", "Unreal: mounting plugins" ),
		( "LogTargetPlatformManager", "Unreal: loading target platforms" ),
		( "LogDerivedDataCache", "Unreal: opening the derived data cache" ),
		( "LogAssetRegistry", "Unreal: reading the asset registry" ),
		( "LogPython", "Unreal: starting Python" ),
	};

	/// <summary>Index into <see cref="BootPhases"/> for a raw log line, or -1 for noise.</summary>
	static int BootPhase( string line )
	{
		for ( int i = BootPhases.Length - 1; i >= 0; i-- )
		{
			if ( line.Contains( BootPhases[i].Marker, StringComparison.Ordinal ) )
				return i;
		}

		return -1;
	}

	/// <summary>
	/// Follow the growing Unreal log file, surfacing progress lines as they land. Unreal
	/// keeps the file open with shared read access and flushes frequently; a short poll
	/// keeps this cheap. Runs on the caller's sync context (awaited reads + delays), so
	/// onProgress can touch UI directly.
	///
	/// Every emitted message carries the elapsed time, and the current one is re-emitted on a
	/// <see cref="Heartbeat"/> whenever the log goes quiet - so even the phases that log
	/// nothing at all (loading a big map, the asset registry scan) visibly tick over.
	/// </summary>
	static async Task TailLog( Process proc, string logPath, Action<ExportEvent> onProgress )
	{
		if ( onProgress is null )
		{
			return;
		}

		var clock = Stopwatch.StartNew();

		// Latest known state, re-emitted by the heartbeat with a fresh elapsed stamp.
		int? done = null, total = null;
		var message = "Starting Unreal";
		var lastEmit = TimeSpan.MinValue;

		void Emit()
		{
			// Total minutes, not the TimeSpan minutes component - an hour-long map export
			// shouldn't look like it restarted its clock.
			var elapsed = $"{(int)clock.Elapsed.TotalMinutes}:{clock.Elapsed.Seconds:00}";
			onProgress( new ExportEvent( done, total, $"{message} ({elapsed})" ) );
			lastEmit = clock.Elapsed;
		}

		async Task Tick()
		{
			if ( clock.Elapsed - lastEmit >= Heartbeat )
				Emit();

			await Task.Delay( 250 );
		}

		Emit();

		while ( !proc.HasExited && !File.Exists( logPath ) )
			await Tick();

		if ( !File.Exists( logPath ) )
			return;

		using var fs = new FileStream( logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete );
		using var reader = new StreamReader( fs );

		// UE's own python startup chatters on LogPython too - hold script messages back until
		// our script announces itself ("=== ue_export: ... ===").
		var sawScript = false;
		var bootPhase = -1;
		var carry = "";
		while ( true )
		{
			var chunk = await reader.ReadToEndAsync();
			if ( chunk.Length > 0 )
			{
				carry += chunk;

				int nl;
				while ( (nl = carry.IndexOf( '\n' )) >= 0 )
				{
					var line = carry[..nl].TrimEnd( '\r' );
					carry = carry[(nl + 1)..];

					var ev = ParseLine( line );

					// Until our script announces itself, Unreal's own boot log is all there
					// is - track a coarse phase from EVERY line, ours or not, so the wait is
					// legible. UE's python startup chatters on LogPython as well, so a
					// LogPython line alone doesn't mean the script is talking yet.
					if ( !sawScript )
					{
						sawScript = ev?.Done is not null
							|| ev?.Message?.StartsWith( "ue_export", StringComparison.OrdinalIgnoreCase ) == true;

						if ( !sawScript )
						{
							var phase = BootPhase( line );
							if ( phase > bootPhase )
							{
								bootPhase = phase;
								message = BootPhases[phase].Phase;
								Emit();
							}

							continue;
						}
					}

					if ( ev is null )
						continue;

					done = ev.Done ?? done;
					total = ev.Total ?? total;
					if ( !string.IsNullOrEmpty( ev.Message ) )
						message = ev.Message;

					Emit();
				}
			}
			else if ( proc.HasExited )
			{
				break;
			}

			await Tick();
		}
	}

	// "...LogPython: [6/98] SM_int_ceiling_300_01" - the per-mesh export progress our script logs.
	static readonly Regex MeshProgressLine = new( @"LogPython:\s*\[(\d+)/(\d+)\]\s*(.+)$", RegexOptions.Compiled );

	/// <summary>
	/// Distil one raw Unreal log line into a progress event, or null for noise. Only our
	/// own script's output (LogPython) is surfaced; indented LogPython lines are per-slot
	/// texture detail and stay hidden.
	/// </summary>
	static ExportEvent ParseLine( string line )
	{
		if ( string.IsNullOrEmpty( line ) )
			return null;

		var match = MeshProgressLine.Match( line );
		if ( match.Success )
		{
			return new ExportEvent(
				int.Parse( match.Groups[1].Value ),
				int.Parse( match.Groups[2].Value ),
				$"Exporting {match.Groups[3].Value.Trim()}" );
		}

		var idx = line.IndexOf( "LogPython: ", StringComparison.Ordinal );
		if ( idx >= 0 )
		{
			var msg = line[(idx + "LogPython: ".Length)..];
			if ( msg.Length > 0 && !char.IsWhiteSpace( msg[0] ) )
				return new ExportEvent( null, null, msg.Trim( '=', ' ' ) );
		}

		return null;
	}
}
