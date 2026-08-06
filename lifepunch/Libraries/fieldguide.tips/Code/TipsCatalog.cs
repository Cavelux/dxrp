using System;
using System.Collections.Generic;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// The registry the coach reads. Three sources merge into <see cref="Active"/>, keyed by
/// <see cref="TipDefinition.Id"/>, highest precedence first:
/// <list type="number">
/// <item>CODE: whatever the game passed to <see cref="Register(IReadOnlyList{TipDefinition})"/>.</item>
/// <item>ASSETS: every <c>.tip</c> <see cref="TipResource"/> found via
/// <c>ResourceLibrary.GetAll&lt;TipResource&gt;()</c>, mapped through <see cref="TipResource.ToDefinition"/>.</item>
/// <item>DRAFTS: runtime tips injected via <see cref="RegisterRuntime(TipDefinition)"/> (a test-fire / dev
/// tool seam), lowest precedence.</item>
/// </list>
/// On an id collision CODE wins, then the asset, then the draft: a game's own code catalog is authored
/// intent, and assets are usually additive or mod content. This is the inverse of the RPG kit, where
/// authored assets override code demos. When all three sources are empty the shipped <see cref="Example"/>
/// surfaces so the panel still does something before any content is wired in.
///
/// Back-compat: <see cref="Register(IReadOnlyList{TipDefinition})"/> and <see cref="Active"/> keep their
/// v0.2 signatures. A game that only calls Register with no assets and no drafts reads exactly its own
/// list, in its own order, from <see cref="Active"/> just as before.
///
/// LIFETIME. Every source here is STATIC, so a catalog registered from a scene component outlives that
/// scene and that play session: load another scene in the same editor process and the old tips are still
/// the highest-priority thing the coach can pick, with nothing over there able to retire them. Register
/// from a component and you want <see cref="RegisterScoped(IReadOnlyList{TipDefinition})"/>, which hands the
/// previous catalog back when you dispose it in OnDestroy. Runtime drafts have the same rule and the same
/// answer, <see cref="ClearRuntime"/>.
///
/// FRESHNESS. The merged view is DERIVED state: one <see cref="TipCatalogView"/> holding the ordered list
/// and the id-to-source map together, rebuilt whenever a source moves and swapped in whole, so the two can
/// never disagree with each other. Sources announce their own moves: <see cref="Register"/> and the draft calls
/// invalidate directly, and <see cref="TipResource"/> calls <see cref="NoteAssetsChanged"/> from its PostLoad
/// / PostReload, which is what makes an edited <c>.tip</c> reach a running session. <c>fg_tips_rebuild</c> is
/// the manual reset for anything that slips past (a deleted asset, a code hotload).
///
/// Game-specific tip text (which keys open which panels, what the dev overlay does, spell and
/// potion lines) belongs in YOUR catalog, not in the kit. The kit owns the mechanic (priority,
/// prerequisites, trigger, complete-when, timeout); you own the words.
/// </summary>
public static class TipsCatalog
{
	// CODE source: the game's own registered catalog (highest precedence).
	private static IReadOnlyList<TipDefinition> _code = Array.Empty<TipDefinition>();

	// DRAFT source: runtime-authored tips (lowest precedence), kept in their own store so a draft never
	// shadows a code or asset tip and a Rebuild can rescan assets without dropping live drafts.
	private static readonly Dictionary<string, TipDefinition> _drafts = new( StringComparer.Ordinal );

	// Source revisions. Bumped whenever that source moves; the snapshot records the three it was built from
	// and a read that finds any of them changed rebuilds. Comparing them is three field reads, no allocation,
	// which is what lets the coach ask for Active several times a frame.
	private static int _draftRevision;
	private static int _assetRevision;

	// The derived view (list + labels together, from the pure TipCatalogMerge) and the stamp saying what it
	// was built from. One reference, swapped in whole: there is no second static that could disagree with it.
	private static TipCatalogView _view;
	private static TipCatalogStamp _stamp;

	/// <summary>
	/// The merged, deduped walkthrough the coach reads (code + assets + drafts by the precedence above),
	/// or <see cref="Example"/> when every source is empty. Priority breaks ties; PrerequisiteTipIds
	/// sequences the spine. Derived and cached against the source revisions, so an edited or newly created
	/// <c>.tip</c> shows up on the next read; <c>fg_tips_rebuild</c> forces a rescan by hand.
	/// </summary>
	public static IReadOnlyList<TipDefinition> Active => Current().Tips;

	/// <summary>The merged catalog and its source labels together, for a reader that needs both and must not
	/// see them from two different builds (the Tips Studio's tip list).</summary>
	public static TipCatalogView View => Current();

	/// <summary>Install your own tutorial line (the CODE source). Call once at bootstrap; a null or empty
	/// list is ignored. Additive to any <c>.tip</c> assets and drafts; code wins id collisions.
	///
	/// The catalog is static and outlives the scene that registered it. Registering from a component that
	/// can be destroyed (a scene bootstrap, a dev harness) wants
	/// <see cref="RegisterScoped(IReadOnlyList{TipDefinition})"/> instead.</summary>
	public static void Register( IReadOnlyList<TipDefinition> tips )
	{
		if ( tips is null || tips.Count == 0 )
			return;
		_code = tips;
		Invalidate();
	}

	/// <summary>
	/// Register a code catalog that HANDS ITSELF BACK. Dispose the returned token (from your component's
	/// OnDestroy, or with a <c>using</c>) and whatever was registered before is restored, so a scene's tips
	/// cannot follow the player into the next scene. Disposing twice is a no-op, and disposing out of order
	/// still restores what this call displaced rather than clearing the catalog outright.
	///
	/// This is the seam the kit's own demo bootstrap discipline generalizes: statics outlive scenes, so
	/// whoever set one puts it back.
	/// </summary>
	public static IDisposable RegisterScoped( IReadOnlyList<TipDefinition> tips )
	{
		var previous = _code;
		Register( tips );
		return new ScopedCode( previous, tips );
	}

	private sealed class ScopedCode : IDisposable
	{
		private readonly IReadOnlyList<TipDefinition> _previous;
		private IReadOnlyList<TipDefinition> _mine;

		public ScopedCode( IReadOnlyList<TipDefinition> previous, IReadOnlyList<TipDefinition> mine )
		{
			_previous = previous ?? Array.Empty<TipDefinition>();
			_mine = mine;
		}

		public void Dispose()
		{
			if ( _mine is null )
				return; // already handed back

			// Only restore if OUR list is still the installed one. A later Register replaced us, and stomping
			// that would be worse than leaving it: the newer registration is the live intent.
			if ( ReferenceEquals( _code, _mine ) )
			{
				_code = _previous;
				Invalidate();
			}

			_mine = null;
		}
	}

	/// <summary>
	/// Inject (or replace) a live runtime draft tip (the lowest-precedence DRAFT source). An id that also
	/// names a code tip or an authored <c>.tip</c> asset resolves to that real tip, never the draft. The
	/// seam a test-fire / dev tool uses so an in-progress tip is previewable without an editor compile.
	/// </summary>
	public static void RegisterRuntime( TipDefinition def )
	{
		if ( def is null || string.IsNullOrEmpty( def.Id ) )
			return;
		_drafts[def.Id] = def;
		_draftRevision++;
		Invalidate();
	}

	/// <summary>Remove a draft tip previously injected via <see cref="RegisterRuntime(TipDefinition)"/>.</summary>
	public static void UnregisterRuntime( string id )
	{
		if ( string.IsNullOrEmpty( id ) || !_drafts.Remove( id ) )
			return;
		_draftRevision++;
		Invalidate();
	}

	/// <summary>Drop every runtime draft, leaving the code catalog and the <c>.tip</c> assets alone. The
	/// Tips Studio calls this when it shuts down: an authoring draft must not follow the developer into
	/// another scene or another session.</summary>
	public static void ClearRuntime()
	{
		if ( _drafts.Count == 0 )
			return;
		_drafts.Clear();
		_draftRevision++;
		Invalidate();
	}

	/// <summary>The ids of the live runtime drafts, so a dev tool can list or clean up exactly what it put
	/// in. Ordered for a stable readout.</summary>
	public static IReadOnlyList<string> RuntimeIds
	{
		get
		{
			var ids = new List<string>( _drafts.Keys );
			ids.Sort( StringComparer.Ordinal );
			return ids;
		}
	}

	/// <summary>Force a fresh merge (rescans <c>.tip</c> assets). Call after hot-loading new tip assets;
	/// otherwise the lazy build is enough. Code catalog and live drafts are preserved.</summary>
	public static void Rebuild() => Invalidate();

	/// <summary>
	/// The <c>.tip</c> asset source moved: a tip was loaded for the first time, or recompiled from disk after
	/// an edit. <see cref="TipResource"/> calls this from its PostLoad / PostReload, which is the engine hook
	/// a kit can reach (<c>ResourceLibrary.IEventListener</c> is internal), and it is what makes an edited tip
	/// appear in a running session. Cheap and idempotent: it bumps a counter and drops the derived snapshot.
	/// </summary>
	public static void NoteAssetsChanged()
	{
		_assetRevision++;
		Invalidate();
	}

	/// <summary>Rescan <c>.tip</c> assets and rebuild the merged catalog by hand, then print what came back.
	/// The fallback for anything the automatic hooks cannot see: a deleted asset, or a code hotload that left
	/// the derived view holding pre-hotload tips.</summary>
	[ConCmd( "fg_tips_rebuild" )]
	public static void RebuildCommand()
	{
		NoteAssetsChanged();
		var view = Current();
		var counts = new Dictionary<string, int>( StringComparer.Ordinal );
		foreach ( var kv in view.SourceById )
			counts[kv.Value] = counts.TryGetValue( kv.Value, out var n ) ? n + 1 : 1;

		var parts = new List<string>();
		foreach ( var kv in counts )
			parts.Add( $"{kv.Key}={kv.Value}" );
		parts.Sort( StringComparer.Ordinal );

		Log.Info( $"fg_tips: catalog rebuilt, {view.Tips.Count} tip(s) [{( parts.Count > 0 ? string.Join( " ", parts ) : "empty" )}]." );
	}

	/// <summary>Clear the code catalog and drafts so <see cref="Active"/> falls back to any authored assets,
	/// or to the shipped <see cref="Example"/> when there are none (mostly for tests / demos).</summary>
	public static void Reset()
	{
		_code = Array.Empty<TipDefinition>();
		_drafts.Clear();
		_draftRevision++;
		Invalidate();
	}

	private static void Invalidate() => _view = null;

	/// <summary>The current derived view, rebuilt when any source has moved since it was made.</summary>
	private static TipCatalogView Current()
	{
		var view = _view;
		if ( view is not null && _stamp.Matches( _code, _draftRevision, _assetRevision ) )
			return view;

		_stamp = new TipCatalogStamp( _code, _draftRevision, _assetRevision );
		return _view = TipCatalogMerge.Merge( _code, AssetDefinitions(), _drafts.Values, BuildExample() );
	}

	private static IEnumerable<TipDefinition> AssetDefinitions()
	{
		foreach ( var res in LoadAssets() )
		{
			var def = res?.ToDefinition();
			if ( def is not null )
				yield return def;
		}
	}

	/// <summary>Which source a tip id resolved from ("code" / "asset" / "draft" / "example"), or "unknown".
	/// Used by the <c>fg_tips_list</c> console command and the Tips Studio's tip list, where the label is how
	/// a tip left over from another scene gives itself away.</summary>
	public static string SourceOf( string id )
	{
		var view = Current();
		return !string.IsNullOrEmpty( id ) && view.SourceById.TryGetValue( id, out var src ) ? src : "unknown";
	}

	private static IEnumerable<TipResource> LoadAssets()
	{
		// ResourceLibrary is only meaningful inside a running game/editor; guard so a headless or unit
		// context (no resource system) merges cleanly instead of throwing.
		try
		{
			return ResourceLibrary.GetAll<TipResource>();
		}
		catch
		{
			return Array.Empty<TipResource>();
		}
	}

	/// <summary>
	/// A minimal, GENERIC illustration, enough to show every mechanic (a completed-by-behaviour spine,
	/// a prerequisite chain, a contextual interrupt via <see cref="TipDefinition.Trigger"/>, and a
	/// timed "glance" beat via <see cref="TipDefinition.MaxShowSeconds"/>). It is meant to be REPLACED:
	/// register your own game's tips with <see cref="Register(IReadOnlyList{TipDefinition})"/>.
	///
	/// A computed property, not a <c>static readonly</c> field: a field's initializer runs once, so after a
	/// code hotload the old list survives and an edit to these tips is invisible until the editor restarts.
	/// A property is a method, and methods come back fresh from a hotload.
	/// </summary>
	public static IReadOnlyList<TipDefinition> Example => BuildExample();

	private static IReadOnlyList<TipDefinition> BuildExample() => new List<TipDefinition>
	{
		// A calm spine beat that retires when the player performs its action. Note the mixed input
		// markup: *asterisks* render a keyboard keycap, `backticks` render a gamepad button chip, so one
		// line can prompt both control schemes.
		new()
		{
			Id = "move", Icon = "🧭", Priority = 100,
			Text = "Move with *W* *A* *S* *D* or the `Left Stick`. Hold *Shift* / `LB` to sprint.",
			CompleteWhen = static c => c.EverMoved,
		},
		// A second beat gated behind the first (prerequisite chain).
		// The same beat with pad-specific wording: on a controller the display shows TextPad instead of
		// Text (build plan point 2), so the prompt reads the button the player actually has.
		new()
		{
			Id = "interact", Icon = "💬", Priority = 90, PrerequisiteTipIds = new[] { "move" },
			Text = "Walk up to someone and press *E* to interact.",
			TextPad = "Walk up to someone and press `X` to interact.",
			CompleteWhen = static c => c.EverTalked,
		},
		// A "just glance" beat with no behavioural signal, it times out on its own.
		new()
		{
			Id = "look", Icon = "🗺️", Priority = 80, PrerequisiteTipIds = new[] { "interact" },
			Text = "Look around with the mouse to get your bearings.",
			MaxShowSeconds = 6f,
		},
		// A contextual interrupt: it outranks the calm spine while a fight is live.
		new()
		{
			Id = "combat", Icon = "❗", Priority = 130,
			Text = "Something's hostile! Attack with *LMB*, and hold *B* to block.",
			Trigger = static c => c.EverAggroed && c.InCombat,
			CompleteWhen = static c => !c.InCombat,
		},
	};
}
