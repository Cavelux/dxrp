using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// The Tips Studio's state and every action its panel takes. The panel
/// (<see cref="TipsStudioPanel"/>) is markup over this; everything that decides something lives here, so the
/// razor stays readable and this stays testable by eye.
///
/// WHAT IT IS. An authoring surface for tips that runs inside your game: list the merged catalog, open any
/// tip in an editor, watch the real card change as you type, fire the tip and its completion for real, and
/// bake the result out as a <c>.tip</c> file. Nothing here is part of a shipped game's runtime: the panel
/// only exists if you add it, it starts closed, and <c>fg_tips_studio</c> is off by default.
///
/// TWO WAYS A DRAFT REACHES THE COACH, and they are deliberately different:
/// <list type="bullet">
/// <item>PREVIEW registers the draft under <see cref="PreviewId"/>, an id nothing else uses, and force-shows
/// it. It is a picture of the card. It cannot be shadowed by a real tip with the same id, which is what would
/// happen if it registered under the draft's own id (drafts are the lowest-precedence source), and it cannot
/// mark anything complete.</item>
/// <item>TEST FIRE registers the draft under its OWN id and shows it, so completing it retires the real tip
/// and the chain advances the way it will in the game. If a code or asset tip already owns that id, that one
/// wins, which is correct: you are testing the chain, not the draft.</item>
/// </list>
///
/// CLEAN-UP. Everything it touches is static and would otherwise outlive the scene: the preview draft, the
/// test-fire draft, and the pinned preview device. <see cref="Shutdown"/> hands all of it back, and the panel
/// calls it from OnDestroy.
/// </summary>
public static class TipsStudio
{
	/// <summary>The id the live preview registers under. Long and namespaced on purpose: it must never
	/// collide with a real tip, because a collision would silently show the real tip instead of the draft.</summary>
	public const string PreviewId = "fg_tips_studio_preview";

	/// <summary>Folder under <c>FileSystem.Data</c> the Studio stages baked tips in, for the editor menu
	/// action to pick up. Staging through a file rather than a live static is what lets you bake in play and
	/// write the asset after you have stopped playing.</summary>
	public const string StageFolder = "fieldguide_tips_studio";

	// ------------------------------------------------------------------
	// Open / close
	// ------------------------------------------------------------------

	private static bool _open;

	/// <summary>Open or close the Tips Studio. Off by default, and the panel forces it off at boot: s&amp;box
	/// persists convars between sessions, so without that a value set weeks ago would open an authoring panel
	/// over someone's game.</summary>
	[ConVar( "fg_tips_studio", Help = "Open or close the Tips Studio authoring panel (dev tool, off by default)" )]
	public static bool Open
	{
		get => _open;
		set => _open = value;
	}

	/// <summary>Which tab is showing.</summary>
	public static StudioTab Tab { get; set; } = StudioTab.Tips;

	/// <summary>The Studio's three tabs.</summary>
	public enum StudioTab
	{
		/// <summary>The merged catalog, with source labels.</summary>
		Tips,

		/// <summary>The draft editor: wording, order, triggers, preview and test fire.</summary>
		Draft,

		/// <summary>The bake-out surface: the .tip JSON, Copy, and staging for the editor.</summary>
		Bake,
	}

	// ------------------------------------------------------------------
	// The draft
	// ------------------------------------------------------------------

	private static TipStudioDraft _draft = new();

	/// <summary>The tip being authored. Never null.</summary>
	public static TipStudioDraft Draft
	{
		get => _draft ??= new TipStudioDraft();
		set => _draft = value ?? new TipStudioDraft();
	}

	/// <summary>The catalog id the draft was opened from, or null for a new tip. Shown so it is obvious
	/// whether you are editing something that already exists.</summary>
	public static string OpenedFrom { get; private set; }

	/// <summary>Start a new, empty tip.</summary>
	public static void NewDraft()
	{
		Draft = new TipStudioDraft { Priority = 100 };
		OpenedFrom = null;
	}

	/// <summary>Open a catalog tip in the editor. The two code-only predicates have no authored form and are
	/// dropped; <see cref="DroppedPredicates"/> says so on screen.</summary>
	public static void OpenTip( string id )
	{
		var def = TipsCatalog.Active.FirstOrDefault( t => t.Id == id );
		if ( def is null )
			return;

		Draft = TipStudioDraft.FromDefinition( def );
		OpenedFrom = id;
		DroppedPredicates = HasCodePredicates( def );
		Tab = StudioTab.Draft;
	}

	/// <summary>True when the tip currently open was carrying a <c>Trigger</c> or <c>CompleteWhen</c>
	/// predicate, which a <c>.tip</c> file cannot hold. Baking it out keeps the declarative triggers and
	/// loses the predicate, so the panel warns before you do.</summary>
	public static bool DroppedPredicates { get; private set; }

	private static bool HasCodePredicates( TipDefinition def )
	{
		// A tip that never set them carries the record's defaults. Comparing against a fresh default is the
		// only way to tell "the author wrote a predicate" from "the record filled one in".
		var plain = new TipDefinition { Id = "probe", Text = "" };
		return def.Trigger != plain.Trigger || def.CompleteWhen != plain.CompleteWhen;
	}

	/// <summary>The authoring notes for the current draft (grey-block run lengths, triggers that can never
	/// fire). Recomputed on read; the panel refreshes them when you press Enter in a box or click anything,
	/// because rebuilding the panel while you type would take the cursor out of the box.</summary>
	public static IReadOnlyList<string> Notes => TipStudioText.Warnings( Draft );

	/// <summary>The draft as <c>.tip</c> JSON: what Copy puts on the clipboard and what a bake writes.</summary>
	public static string Json => TipStudioJson.Write( Draft );

	/// <summary>True when the draft's id already names a tip from a HIGHER-precedence source, so a bake would
	/// be shadowed until that source lets go. Worth saying out loud before someone wonders why their new file
	/// does nothing.</summary>
	public static string ShadowedBy
	{
		get
		{
			if ( string.IsNullOrWhiteSpace( Draft.Id ) )
				return null;

			var source = TipsCatalog.SourceOf( Draft.Id );
			return source == "code" ? "code" : null;
		}
	}

	// ------------------------------------------------------------------
	// Live preview
	// ------------------------------------------------------------------

	/// <summary>Whether the real card is mirroring the draft right now.</summary>
	public static bool PreviewOn { get; private set; }

	/// <summary>Push the draft onto the real card, or refresh what is already there. Registers under
	/// <see cref="PreviewId"/> so a draft of an existing tip is not shadowed by the tip it copies.</summary>
	public static void PushPreview( Scene scene )
	{
		var def = Draft.ToDefinition();

		// The preview stands in for the draft even before it has an id, so an author sees the card from the
		// first character typed rather than after they remember to name it.
		var preview = new TipDefinition
		{
			Id = PreviewId,
			Text = Draft.Text ?? "",
			TextPad = string.IsNullOrEmpty( Draft.TextPad ) ? null : Draft.TextPad,
			Icon = Draft.Icon ?? "",
			Priority = def?.Priority ?? 0,
		};

		TipsCatalog.RegisterRuntime( preview );
		PreviewOn = true;

		var coach = LiveCoach( scene );
		coach?.ForceShow( PreviewId );
	}

	/// <summary>Take the preview off the card and out of the catalog.</summary>
	public static void StopPreview( Scene scene )
	{
		PreviewOn = false;
		TipsCatalog.UnregisterRuntime( PreviewId );
		TipsCoach.PreviewDevice = null;

		// The card may still be showing a tip that no longer exists. Drop it rather than dismiss it: dismissing
		// would write the fake preview id into the player's saved progress and leave it there for good.
		if ( TipsCoach.ActiveTip?.Id == PreviewId )
			LiveCoach( scene )?.DropActive();
	}

	/// <summary>Which device the preview card is pinned to, or null for whatever the player last used.</summary>
	public static TipDevice? PinnedDevice
	{
		get => TipsCoach.PreviewDevice;
		set => TipsCoach.PreviewDevice = value;
	}

	// ------------------------------------------------------------------
	// Test fire
	// ------------------------------------------------------------------

	/// <summary>
	/// Make the draft the live tip UNDER ITS OWN ID and show it now. From here its completion is the real
	/// thing: fire the trigger in the game, or press Complete, and the tip retires and the chain moves on.
	/// Returns false when the draft has no id yet.
	/// </summary>
	public static bool TestFire( Scene scene )
	{
		var def = Draft.ToDefinition();
		if ( def is null )
			return false;

		// A test fire of a tip already marked complete would retire the moment it appeared.
		TipsCoach.Uncomplete( def.Id );
		TipsCatalog.UnregisterRuntime( PreviewId );
		PreviewOn = false;
		TipsCatalog.RegisterRuntime( def );

		var coach = LiveCoach( scene );
		if ( coach is null )
		{
			Log.Warning( "fg_tips: no TipsCoach in the scene, so there is nothing to show the tip on." );
			return false;
		}

		return coach.ForceShow( def.Id );
	}

	/// <summary>Fire the draft's completion by hand, the same path a world trigger uses. The tip retires and
	/// whatever waits on it becomes eligible.</summary>
	public static void CompleteNow()
	{
		if ( string.IsNullOrWhiteSpace( Draft.Id ) )
			return;

		TipsCoach.Complete( Draft.Id );
	}

	private static TipsCoach LiveCoach( Scene scene )
		=> scene?.GetAllComponents<TipsCoach>().FirstOrDefault();

	// ------------------------------------------------------------------
	// Input actions (the action picker)
	// ------------------------------------------------------------------

	/// <summary>
	/// The project's real input actions, for the InputAction picker: <c>Input.ActionNames</c>, which is the
	/// engine's list from the current game's input settings, the same list the <c>[InputAction]</c> inspector
	/// dropdown draws from. Sorted, and empty rather than throwing outside a running game.
	/// </summary>
	public static IReadOnlyList<string> ActionNames
	{
		get
		{
			try
			{
				var names = Input.ActionNames?.Where( n => !string.IsNullOrWhiteSpace( n ) ).ToList();
				if ( names is null || names.Count == 0 )
					return Array.Empty<string>();

				names.Sort( StringComparer.OrdinalIgnoreCase );
				return names;
			}
			catch ( Exception )
			{
				return Array.Empty<string>();
			}
		}
	}

	// ------------------------------------------------------------------
	// Bake out
	// ------------------------------------------------------------------

	/// <summary>Copy the draft's <c>.tip</c> JSON to the system clipboard, from in game. Paste it into a new
	/// file under your project's <c>Assets/</c> and the editor picks it up as a tip.</summary>
	public static void CopyJson()
	{
		Sandbox.UI.Clipboard.SetText( Json );
	}

	/// <summary>
	/// Write the draft into <see cref="StageFolder"/> under <c>FileSystem.Data</c>, where the editor menu
	/// action "Field Guide / Write staged tips" picks it up and writes the real asset. Two steps because game
	/// code cannot write into a project's <c>Assets/</c> folder, and because staging survives the end of the
	/// play session, so you can author in play and land the file afterwards.
	/// </summary>
	/// <returns>A line for the panel saying what happened.</returns>
	public static string Stage()
	{
		var file = TipStudioJson.FileNameFor( Draft.Id );
		if ( file is null )
			return "Give the tip an id first.";

		try
		{
			FileSystem.Data.CreateDirectory( StageFolder );
			var path = $"{StageFolder}/{file}";
			FileSystem.Data.WriteAllText( path, Json );
			Log.Info( $"fg_tips: staged {file}. In the editor, run Field Guide / Write staged tips to Assets/tips." );

			// Short on purpose: this lands in a one-line status slot in the panel, and the console line
			// above already carries the full instruction.
			return $"staged {file}";
		}
		catch ( Exception e )
		{
			Log.Warning( $"fg_tips: could not stage {file} ({e.Message})." );
			return $"Could not stage {file}: {e.Message}";
		}
	}

	/// <summary>How many tips are waiting in the staging folder, so the panel can say whether there is
	/// anything for the editor action to do.</summary>
	public static int StagedCount
	{
		get
		{
			try
			{
				return FileSystem.Data.DirectoryExists( StageFolder )
					? FileSystem.Data.FindFile( StageFolder, "*.tip", false ).Count()
					: 0;
			}
			catch ( Exception )
			{
				return 0;
			}
		}
	}

	/// <summary>Empty the staging folder, for when a bake was a mistake or the files have landed.</summary>
	public static string ClearStaged()
	{
		try
		{
			if ( !FileSystem.Data.DirectoryExists( StageFolder ) )
				return "Nothing staged.";

			var cleared = 0;
			foreach ( var file in FileSystem.Data.FindFile( StageFolder, "*.tip", false ).ToList() )
			{
				FileSystem.Data.DeleteFile( $"{StageFolder}/{file}" );
				cleared++;
			}

			return cleared == 0 ? "Nothing staged." : $"Cleared {cleared} staged tip(s).";
		}
		catch ( Exception e )
		{
			return $"Could not clear the staging folder: {e.Message}";
		}
	}

	// ------------------------------------------------------------------
	// Shutdown
	// ------------------------------------------------------------------

	/// <summary>
	/// Hand back everything the Studio pinned: the preview and test-fire drafts, and the pinned preview
	/// device. Called from the panel's OnDestroy, because all of it is static and would otherwise follow the
	/// developer into the next scene, exactly the trap a scene-registered code catalog falls into.
	/// </summary>
	public static void Shutdown()
	{
		PreviewOn = false;
		TipsCoach.PreviewDevice = null;
		TipsCatalog.ClearRuntime();
		Open = false;
	}
}
