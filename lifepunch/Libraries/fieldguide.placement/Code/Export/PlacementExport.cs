using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldGuide.Placement;

/// <summary>
/// Bakes what you tuned in-game into text you can paste into your own project. Two populations are
/// collected, because the kit serves two jobs and both end at "get these numbers into my code":
///
///  1. TWEAK TARGETS (the hero case). Every <see cref="ITweakTarget"/> registered with the scene's
///     <see cref="TweakSession"/>, exported as a LOCAL offset tagged with the frame it hangs off
///     ("citizen/hold_R", "citizen/spine_2", ...). This is the accessory-fitting path: parent a custom
///     model to a character, drag it into place, bake the offset.
///  2. PLACED INSTANCES (scene authoring). Every object carrying a <see cref="PlacedInstance"/>, exported
///     in WORLD space. This is the ghost-placer path: lay a scene out, bake the coordinates.
///
/// <see cref="WriteAll(Scene)"/> writes a JSON file and a C# file into FileSystem.Data (per-project,
/// writable) and logs their full paths. The panel's Copy button does the single-target version:
/// <see cref="BakeLine(ITweakTarget)"/> straight onto the clipboard.
/// </summary>
public static class PlacementExport
{
	public const string JsonFileName = "placement_export.json";
	public const string CSharpFileName = "placement_export.cs";

	// ---- collection ----

	/// <summary>Both populations in one list: tweak-session offsets first (ordered by frame then id), then
	/// world-space placed instances. This is what the JSON and C# exports write.</summary>
	public static List<PlacedItem> Collect( Scene scene )
	{
		var list = new List<PlacedItem>();
		if ( scene is null ) return list;

		list.AddRange( CollectOffsets( scene ) );
		list.AddRange( CollectPlaced( scene ) );
		return list;
	}

	/// <summary>The tweak session's targets as LOCAL offsets, each tagged with its frame. Empty when the
	/// scene has no <see cref="TweakSession"/> or the session holds no valid targets.</summary>
	public static List<PlacedItem> CollectOffsets( Scene scene )
	{
		var list = new List<PlacedItem>();
		if ( scene is null ) return list;

		foreach ( var session in scene.GetAllComponents<TweakSession>() )
		{
			if ( session is null || !session.IsValid() ) continue;
			foreach ( var target in session.Targets )
			{
				var item = ToItem( target );
				if ( item is not null ) list.Add( item );
			}
		}

		return list
			.OrderBy( i => i.Frame )
			.ThenBy( i => i.Id )
			.ToList();
	}

	/// <summary>Every object tagged with a <see cref="PlacedInstance"/>, in WORLD space, ordered by catalog
	/// id then x for stable output.</summary>
	public static List<PlacedItem> CollectPlaced( Scene scene )
	{
		var list = new List<PlacedItem>();
		if ( scene is null ) return list;

		foreach ( var inst in scene.GetAllComponents<PlacedInstance>() )
		{
			if ( inst is null || !inst.IsValid() ) continue;
			var go = inst.GameObject;
			list.Add( new PlacedItem(
				string.IsNullOrEmpty( inst.CatalogId ) ? "item" : inst.CatalogId,
				go.WorldPosition,
				go.WorldRotation.Angles(),
				go.WorldScale.x,
				PlacedItem.WorldFrame ) );
		}

		return list
			.OrderBy( i => i.Id )
			.ThenBy( i => i.Position.x )
			.ToList();
	}

	/// <summary>One tweak target's live LOCAL transform as a record: its bake id, its local position /
	/// rotation / uniform scale, and the frame it hangs off. Null for an invalid target.</summary>
	public static PlacedItem ToItem( ITweakTarget target )
	{
		var go = target?.Target;
		if ( go is null || !go.IsValid() ) return null;

		return new PlacedItem(
			target.BakeSymbol,
			go.LocalPosition,
			go.LocalRotation.Angles(),
			go.LocalScale.x,
			target.FrameName );
	}

	// ---- the paste-ready line (what Copy puts on the clipboard) ----

	/// <summary>
	/// The paste-ready C# for ONE tweak target. A target that implements
	/// <see cref="ITweakTarget.FormatBakeLine"/> shapes its own line (bake straight into your real data
	/// shape); otherwise the kit's default form is used. Returns an empty string for an invalid target.
	/// </summary>
	public static string BakeLine( ITweakTarget target )
	{
		var item = ToItem( target );
		if ( item is null ) return "";

		var custom = target.FormatBakeLine( item );
		return string.IsNullOrEmpty( custom ) ? DefaultBakeLine( item ) : custom;
	}

	/// <summary>
	/// The kit's default bake line: a comment naming the id and the frame the offset is measured from,
	/// then a <see cref="PlacedOffset"/> constructor with the live numbers. Two lines, because the comment
	/// is what makes the numbers mean something once they are sitting in a file three weeks later.
	/// <code>
	/// // hat: offset from citizen/head
	/// new PlacedOffset( "hat", new Vector3( 0.5f, 0f, 3.25f ), new Angles( 0f, 90f, 0f ), 1f ),
	/// </code>
	/// </summary>
	public static string DefaultBakeLine( PlacedItem it )
	{
		if ( it is null ) return "";
		string what = it.IsLocalOffset ? $"offset from {it.Frame}" : "world placement";
		return $"// {it.Id}: {what}\n{OffsetCtor( it )},";
	}

	/// <summary>Just the constructor expression, no comment and no trailing comma. The building block the
	/// default line and the C# file share.</summary>
	public static string OffsetCtor( PlacedItem it )
		=> $"new PlacedOffset( \"{BakeFormat.Escape( it.Id )}\", "
		 + $"new Vector3( {BakeFormat.F( it.Position.x )}f, {BakeFormat.F( it.Position.y )}f, {BakeFormat.F( it.Position.z )}f ), "
		 + $"new Angles( {BakeFormat.F( it.Rotation.pitch )}f, {BakeFormat.F( it.Rotation.yaw )}f, {BakeFormat.F( it.Rotation.roll )}f ), "
		 + $"{BakeFormat.F( it.Scale )}f )";

	// ---- JSON ----

	/// <summary>An indented JSON array of { id, frame, position {x,y,z}, rotation {pitch,yaw,roll}, scale }.
	/// "frame" is "world" for a placed instance and the target's frame name for a tuned offset.</summary>
	public static string ToJson( IEnumerable<PlacedItem> items )
	{
		var sb = new StringBuilder();
		sb.Append( "[\n" );
		var arr = items.ToList();
		for ( int i = 0; i < arr.Count; i++ )
		{
			var it = arr[i];
			sb.Append( "  {\n" );
			sb.Append( $"    \"id\": \"{BakeFormat.Escape( it.Id )}\",\n" );
			sb.Append( $"    \"frame\": \"{BakeFormat.Escape( it.Frame )}\",\n" );
			sb.Append( $"    \"position\": {{ \"x\": {BakeFormat.F( it.Position.x )}, \"y\": {BakeFormat.F( it.Position.y )}, \"z\": {BakeFormat.F( it.Position.z )} }},\n" );
			sb.Append( $"    \"rotation\": {{ \"pitch\": {BakeFormat.F( it.Rotation.pitch )}, \"yaw\": {BakeFormat.F( it.Rotation.yaw )}, \"roll\": {BakeFormat.F( it.Rotation.roll )} }},\n" );
			sb.Append( $"    \"scale\": {BakeFormat.F( it.Scale )}\n" );
			sb.Append( i < arr.Count - 1 ? "  },\n" : "  }\n" );
		}
		sb.Append( "]\n" );
		return sb.ToString();
	}

	// ---- C# snippet ----

	/// <summary>
	/// A paste-ready C# file with up to two blocks, each written only when it has entries:
	/// a <c>PlacedOffset[]</c> of character-relative offsets (one commented line per accessory, naming the
	/// frame it hangs off), and a <c>PlacedItem[]</c> of world-space placements. Both record types ship
	/// with the kit, so the paste compiles as-is against <c>FieldGuide.Placement</c>.
	/// </summary>
	public static string ToCSharp( IEnumerable<PlacedItem> items )
	{
		var arr = items.ToList();
		var offsets = arr.Where( i => i.IsLocalOffset ).ToList();
		var placed = arr.Where( i => !i.IsLocalOffset ).ToList();

		var sb = new StringBuilder();
		sb.Append( "// Generated by FieldGuide.Placement. Paste into your project.\n" );

		if ( offsets.Count > 0 )
		{
			sb.Append( "\n// Character-relative offsets. Each is LOCAL to the frame named above it: parent your\n" );
			sb.Append( "// object to that mount, then call offset.ApplyTo( go ).\n" );
			sb.Append( "static readonly PlacedOffset[] Offsets =\n{\n" );
			foreach ( var it in offsets )
			{
				sb.Append( $"\t// {it.Id}: offset from {it.Frame}\n" );
				sb.Append( $"\t{OffsetCtor( it )},\n" );
			}
			sb.Append( "};\n" );
		}

		if ( placed.Count > 0 )
		{
			sb.Append( "\n// World-space placements from the ghost placer.\n" );
			sb.Append( "// PlacedItem( string Id, Vector3 Position, Angles Rotation, float Scale, string Frame )\n" );
			sb.Append( "static readonly PlacedItem[] Placed =\n{\n" );
			foreach ( var it in placed )
			{
				sb.Append( $"\tnew PlacedItem( \"{BakeFormat.Escape( it.Id )}\", " );
				sb.Append( $"new Vector3( {BakeFormat.F( it.Position.x )}f, {BakeFormat.F( it.Position.y )}f, {BakeFormat.F( it.Position.z )}f ), " );
				sb.Append( $"new Angles( {BakeFormat.F( it.Rotation.pitch )}f, {BakeFormat.F( it.Rotation.yaw )}f, {BakeFormat.F( it.Rotation.roll )}f ), " );
				sb.Append( $"{BakeFormat.F( it.Scale )}f, \"{BakeFormat.Escape( it.Frame )}\" ),\n" );
			}
			sb.Append( "};\n" );
		}

		if ( offsets.Count == 0 && placed.Count == 0 )
			sb.Append( "\n// Nothing to export: no TweakSession targets and no PlacedInstance objects in the scene.\n" );

		return sb.ToString();
	}

	// ---- write both to FileSystem.Data ----

	/// <summary>Collect both populations and write the JSON and C# exports to FileSystem.Data, logging the
	/// paths and the per-population counts. Returns the total number of records written.</summary>
	public static int WriteAll( Scene scene )
	{
		var items = Collect( scene );
		int offsets = items.Count( i => i.IsLocalOffset );
		int placed = items.Count - offsets;

		FileSystem.Data.WriteAllText( JsonFileName, ToJson( items ) );
		FileSystem.Data.WriteAllText( CSharpFileName, ToCSharp( items ) );

		Log.Info( $"[placement] exported {offsets} tuned offset(s) + {placed} world placement(s):" );
		Log.Info( $"[placement]   JSON to {FileSystem.Data.GetFullPath( JsonFileName )}" );
		Log.Info( $"[placement]   C# to {FileSystem.Data.GetFullPath( CSharpFileName )}" );
		return items.Count;
	}
}
