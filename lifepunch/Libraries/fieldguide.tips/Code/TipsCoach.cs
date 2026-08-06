using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// The contextual onboarding coach: it decides which single tip is relevant right now and shows it in
/// a small calm panel (<see cref="TipsDisplay"/>) until the player performs the action, then advances
/// straight to the next tip. It never fights the player's attention: it hides while a modal,
/// conversation or a dev/menu overlay is open.
///
/// SEAM MODEL. The kit owns the engine, catalog types and display; the game owns the words and the
/// wiring. Because a library may not reach into game state, the coach is DRIVEN by the consumer:
/// <list type="bullet">
/// <item>Each frame the consumer builds a neutral <see cref="TipContext"/> (vitals, quest count,
/// modal/overlay flags, ...) and calls <see cref="Tick(TipContext)"/>.</item>
/// <item>When the consumer observes a one-off behaviour (the player moved, cast a spell, aggroed an
/// NPC) it calls <see cref="Signal(TipSignal)"/> or one of the named wrappers. The coach latches it
/// and the matching <c>Ever*</c> field on the context reads true from then on.</item>
/// </list>
/// A few behaviours are ALSO auto-latched from the neutral context each tick (talking from
/// <see cref="TipContext.DialogueActive"/>, quest-accept from <see cref="TipContext.ActiveQuestCount"/>,
/// aggro from in-combat + enemies-nearby, overlay-opened from <see cref="TipContext.OverlayOpen"/>,
/// panel-closed from a modal/overlay closing), so a game that only pushes context still advances.
///
/// It is a plain <see cref="Component"/> (all logic + state) that lives beside its
/// <see cref="TipsDisplay"/> PanelComponent on one <see cref="ScreenPanel"/>; the display reads the
/// statics this component publishes. Everything is client-local, tips are a personal UX layer and
/// never touch networking or gameplay state.
///
/// Progress (completed tip ids + a "dismissed for good" flag) persists to
/// <c>FileSystem.Data</c> so returning players aren't re-taught.
/// </summary>
[Title( "Tips Coach" )]
[Category( "Field Guide Tips" )]
[Icon( "school" )]
public sealed class TipsCoach : Component
{
	// ------------------------------------------------------------------
	// Config surface (convar to disable, concmd to reset)
	// ------------------------------------------------------------------

	/// <summary>Master switch. Set <c>fg_tips 0</c> in the console to turn the coach off entirely.
	/// Named <c>TipsEnabled</c> (not <c>Enabled</c>) so this static convar doesn't hide the inherited
	/// instance <see cref="Component.Enabled"/>.</summary>
	[ConVar( "fg_tips" )]
	public static bool TipsEnabled { get; set; } = true;

	/// <summary>Forget all tip progress and start the walkthrough over (also re-enables the coach).</summary>
	[ConCmd( "fg_tips_reset" )]
	public static void ResetProgress()
	{
		_completed.Clear();
		_allDismissed = false;
		TipsEnabled = true;
		Save();

		// Clear any live coach's runtime state so it restarts cleanly this session.
		foreach ( var coach in LiveCoaches() )
			coach.ResetRuntime();

		Log.Info( "fg_tips: progress reset, the walkthrough will start over." );
	}

	/// <summary>Print the active device, then every tip in the merged catalog: id, source, completed state,
	/// priority, prerequisites, and a live relevance verdict from the first live coach.</summary>
	[ConCmd( "fg_tips_list" )]
	public static void ListTips()
	{
		Load();
		var coach = LiveCoaches().FirstOrDefault();
		Log.Info( $"fg_tips: device={ActiveDevice}" + ( coach is null ? " (no live coach, relevance verdicts unavailable)" : "" ) );

		var any = false;
		foreach ( var def in TipsCatalog.Active )
		{
			any = true;
			var source = TipsCatalog.SourceOf( def.Id );
			var state = _completed.Contains( def.Id ) ? "done" : "todo";
			var prereqs = def.PrerequisiteTipIds.Length > 0 ? string.Join( ",", def.PrerequisiteTipIds ) : "-";
			var verdict = coach is not null ? coach.RelevanceVerdict( def ) : "n/a";
			Log.Info( $"fg_tips: {def.Id} [{source}] {state} prio={def.Priority} prereqs={prereqs} relevance={verdict}" );
		}

		if ( !any )
			Log.Info( "fg_tips: catalog is empty." );
	}

	/// <summary>Print the active input device and how the on-screen tip resolved for it: which text field
	/// (Text or TextPad) drives the wording, the parsed chip segments, the hide-chip label, and whether a pad
	/// label map / hide seam is wired. Closes the dogfood finding that a device-gated tip was indistinguishable
	/// from a broken input bridge.</summary>
	[ConCmd( "fg_tips_device" )]
	public static void DeviceInfo()
	{
		Load();
		var onPad = ActiveDevice == TipDevice.Gamepad;
		Log.Info( $"fg_tips: device={ActiveDevice} usingController={Input.UsingController} " +
			$"padLabelMap={( PadLabelFor is not null ? "set" : "none" )} " +
			$"hideAll={( HideAll is not null ? "wired" : "unset" )} " +
			$"hideLabel='{( onPad ? HidePadLabel : HideKeyLabel )}'" );

		var def = ActiveTip;
		if ( def is null )
		{
			Log.Info( "fg_tips: no active tip on screen." );
			return;
		}

		var usesPad = onPad && !string.IsNullOrEmpty( def.TextPad );
		var field = !onPad ? "Text" : ( usesPad ? "TextPad" : "Text (TextPad unset, fallback)" );
		var segs = onPad ? ActivePadSegments : ActiveSegments;
		var kinds = string.Join( " | ", segs.Select( s => $"{s.Kind}:'{s.Text}'" ) );
		Log.Info( $"fg_tips: active='{def.Id}' field={field} segments=[{kinds}]" );
	}

	/// <summary>Force the given tip active on every live coach, for preview. It shows until it completes
	/// (or you complete/reset it); a completed tip previews for the readable window then retires.</summary>
	[ConCmd( "fg_tips_show" )]
	public static void ShowTip( string id )
	{
		var found = false;
		foreach ( var coach in LiveCoaches() )
			found |= coach.ForceShow( id );

		if ( !found )
			Log.Warning( $"fg_tips: no live coach could show '{id}' (unknown id, or no coach in the scene)." );
	}

	/// <summary>Fire completion for the given tip id (the same path a world trigger uses). Proves a
	/// trigger end to end from the console.</summary>
	[ConCmd( "fg_tips_complete" )]
	public static void CompleteTip( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
		{
			Log.Warning( "fg_tips: fg_tips_complete needs a tip id." );
			return;
		}

		Complete( id );
		Log.Info( $"fg_tips: completed '{id}'." );
	}

	private static IEnumerable<TipsCoach> LiveCoaches()
		=> Game.ActiveScene?.GetAllComponents<TipsCoach>() ?? Enumerable.Empty<TipsCoach>();

	// ------------------------------------------------------------------
	// Published display state (read by TipsDisplay)
	// ------------------------------------------------------------------

	/// <summary>The tip to render right now, or null when nothing should show.</summary>
	public static TipDefinition ActiveTip { get; private set; }

	/// <summary>True when the panel should be visible (an active tip, and not suppressed).</summary>
	public static bool Visible { get; private set; }

	/// <summary>The active tip's text split into keycap/plain segments (bold-key rendering). Never null. This
	/// is the KEYBOARD/MOUSE list; the display renders it while <see cref="ActiveDevice"/> is
	/// <see cref="TipDevice.KeyboardMouse"/>.</summary>
	public static IReadOnlyList<TipSegment> ActiveSegments { get; private set; } = Array.Empty<TipSegment>();

	/// <summary>The active tip's PAD-MODE variant, parsed the same way as <see cref="ActiveSegments"/> but from
	/// <see cref="TipDefinition.TextPad"/> when set, else the same <see cref="TipDefinition.Text"/> (so a tip
	/// with no pad-specific wording reads identically from either list). Published alongside
	/// <see cref="ActiveSegments"/> everywhere it changes; the display picks between the two off
	/// <see cref="ActiveDevice"/>. Never null.</summary>
	public static IReadOnlyList<TipSegment> ActivePadSegments { get; private set; } = Array.Empty<TipSegment>();

	// ------------------------------------------------------------------
	// Device (build plan point 1): the active input device, backed by Input.UsingController
	// ------------------------------------------------------------------

	/// <summary>
	/// The device the player last used, from <c>Input.UsingController</c> (the engine flaps it to the
	/// last-used device; it is polled, there is no event). The display folds this into its BuildHash so a
	/// device flip re-renders the active tip with the right wording and chips immediately. A library may read
	/// <c>Input</c> directly (the coach already does for its input triggers), so this needs no bridge.
	/// </summary>
	public static TipDevice ActiveDevice => PreviewDevice ?? ( Input.UsingController ? TipDevice.Gamepad : TipDevice.KeyboardMouse );

	/// <summary>
	/// Pin <see cref="ActiveDevice"/> to one device regardless of what the player last touched. An AUTHORING
	/// seam: the Tips Studio uses it so you can read a tip's pad wording, and its pad chips, without owning a
	/// controller. Null (the default) is the live value from <c>Input.UsingController</c>, which is what a
	/// shipped game always runs with. Whoever sets it puts it back; the Studio clears it when it closes.
	/// </summary>
	public static TipDevice? PreviewDevice { get; set; }

	/// <summary>
	/// Optional whole-catalog keycap remap for pad mode (build plan point 4), generalizing World Builder's
	/// <c>Cap()</c>. Null (the default) leaves keycaps untouched. When set, the display passes each keyboard
	/// keycap label through this map while the player is on a pad: return a controller label to swap the chip
	/// (e.g. "RMB" to "LT"), return the label unchanged to pass it through, or return null/empty to skip a
	/// chip that has no pad equivalent. It is the alternative to authoring <see cref="TipDefinition.TextPad"/>
	/// on every tip: map the handful of labels once. Applied at render time via
	/// <see cref="TipDeviceText.PadCap(string, System.Func{string, string})"/>.
	/// </summary>
	public static Func<string, string> PadLabelFor { get; set; }

	/// <summary>
	/// Optional consumer-set labels for a card "hide tips" chip: the keyboard key name (e.g. "Y") and the
	/// gamepad button name (e.g. "B"). The display shows the one that matches <see cref="ActiveDevice"/>, next
	/// to the built-in close affordance; a null/empty label hides that chip variant. The kit itself has no
	/// keybind opinion (SEAM MODEL: the game owns the words), so the chip does not appear until a game sets a
	/// label. Set once at bootstrap. Upstreamed from the World Builder vendored copy under the same names.
	/// </summary>
	public static string HideKeyLabel { get; set; }

	/// <inheritdoc cref="HideKeyLabel"/>
	public static string HidePadLabel { get; set; }

	/// <summary>
	/// What "hide tips" MEANS belongs to the game (SEAM MODEL): a library cannot reference the game's controls
	/// or its persisted preferences. When set, the card's hide affordance (the close chip, and the optional
	/// <see cref="HideKeyLabel"/>/<see cref="HidePadLabel"/> chip) invokes this instead of dismissing only the
	/// current tip, so a game can route the button at a persisted "tips off" preference. Null (the default)
	/// leaves the close affordance dismissing the current tip, the v0.1-v0.3 behaviour. Assign once at init.
	/// Upstreamed from the World Builder vendored copy under the same name and semantics.
	/// </summary>
	public static Action HideAll { get; set; }

	// ------------------------------------------------------------------
	// Persistence
	// ------------------------------------------------------------------

	/// <summary>
	/// The <see cref="FileSystem.Data"/> file tip progress persists to. <see cref="FileSystem.Data"/> is
	/// already sandboxed per game (each published package gets its own data folder), so the default name
	/// never collides across two games on one machine. Override it before <see cref="Ensure(Scene)"/>
	/// only if you want an explicit namespace (e.g. one game hosting several distinct tip tracks). Set it
	/// before the first <see cref="Ensure(Scene)"/> / <see cref="Signal(TipSignal)"/> call so the initial
	/// load reads the right file.
	/// </summary>
	public static string SaveFileName { get; set; } = "fieldguide_tips.json";

	// Static so progress survives the component being recreated across play sessions, and so the
	// concmd can reach it without an instance.
	private static readonly HashSet<string> _completed = new();
	private static bool _allDismissed;
	private static bool _loaded;

	/// <summary>Serializable progress blob.</summary>
	private sealed class TipsSave
	{
		public List<string> Completed { get; set; } = new();
		public bool Dismissed { get; set; }
	}

	private static void Load()
	{
		if ( _loaded )
			return;
		_loaded = true;

		try
		{
			if ( !FileSystem.Data.FileExists( SaveFileName ) )
				return;

			var data = Json.Deserialize<TipsSave>( FileSystem.Data.ReadAllText( SaveFileName ) );
			if ( data is null )
				return;

			_completed.Clear();
			foreach ( var id in data.Completed ?? new List<string>() )
				_completed.Add( id );
			_allDismissed = data.Dismissed;
		}
		catch ( Exception e )
		{
			Log.Warning( $"TipsCoach: could not read tip progress ({e.Message}); starting fresh." );
		}
	}

	private static void Save()
	{
		try
		{
			var data = new TipsSave { Completed = _completed.ToList(), Dismissed = _allDismissed };
			FileSystem.Data.WriteAllText( SaveFileName, Json.Serialize( data ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"TipsCoach: could not save tip progress ({e.Message})." );
		}
	}

	/// <summary>
	/// One-time forward migration for a game that used to persist tip progress under a different file
	/// name (before adopting this kit, or before changing <see cref="SaveFileName"/>). If the current
	/// <see cref="SaveFileName"/> does not exist yet but <paramref name="legacyFileName"/> does, its
	/// contents are copied forward so returning players keep their completed tips. The legacy file is
	/// left untouched. Idempotent and safe to call every bootstrap; call it before
	/// <see cref="Ensure(Scene)"/> so the first load reads the migrated file.
	/// </summary>
	/// <param name="legacyFileName">The old <see cref="FileSystem.Data"/> file to copy forward, e.g.
	/// <c>"my_game_tips.json"</c>.</param>
	/// <returns>True when a migration was performed this call; false when there was nothing to migrate.</returns>
	public static bool MigrateProgressFrom( string legacyFileName )
	{
		if ( string.IsNullOrEmpty( legacyFileName ) || legacyFileName == SaveFileName )
			return false;

		try
		{
			if ( FileSystem.Data.FileExists( SaveFileName ) || !FileSystem.Data.FileExists( legacyFileName ) )
				return false;

			FileSystem.Data.WriteAllText( SaveFileName, FileSystem.Data.ReadAllText( legacyFileName ) );
			Log.Info( $"TipsCoach: migrated tip progress from {legacyFileName} to {SaveFileName}." );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"TipsCoach: could not migrate tip progress from {legacyFileName} ({e.Message}); starting fresh." );
			return false;
		}
	}

	// ------------------------------------------------------------------
	// Runtime state
	// ------------------------------------------------------------------

	private const float MinShowSeconds = 0.75f;  // every tip is readable at least this long before it can complete

	private TipContext _ctx;
	private string _activeId;
	private TimeSince _activeSince;

	// Edge tracker for the "a panel just closed" latch.
	private bool _anyPanelOpenLast;

	// Self-drive guard (v0.3). Tick sets this true every frame it runs; the coach's own OnUpdate reads it
	// and skips its evaluation pass whenever a Tick drove the coach since the last OnUpdate, so a game that
	// calls Tick every frame (the v0.2 pattern) is fully Tick-driven and byte-identical. It starts TRUE so
	// the very first OnUpdate defers to a bootstrap Tick that may run later in the same frame; this makes
	// the guard robust to intra-frame component ordering from frame one, at the cost of a pure self-drive
	// game (one that never calls Tick) beginning on its second frame instead of its first. A tickless game
	// leaves this false after that first frame, so OnUpdate self-drives every frame with a context
	// synthesized from the TipsWorld seams.
	private bool _tickedSinceSelfUpdate = true;

	// Latches a declarative completion trigger that fired for the active tip (v0.3). Input-edge kinds
	// (Input.Pressed / Input.Keyboard.Pressed) are true for a single frame, so a press inside the
	// MinShowSeconds readable window would otherwise be lost; latching lets the tip retire once the
	// window passes. Reset whenever a new tip becomes active. The v0.2 CompleteWhen predicate is NOT
	// latched (it is evaluated live at the completion gate, exactly as before), so existing tips are
	// unaffected.
	private bool _completionSeen;

	// Latched behaviours (mirrored into TipContext each Tick). Once a signal lands it stays for the session.
	private readonly HashSet<TipSignal> _signals = new();

	// Latched custom (string-keyed) behaviours, for games that drive the coach in their own vocabulary
	// rather than the named TipSignal set. Read back through TipContext.Ever(string).
	private readonly HashSet<string> _customSignals = new();

	// ------------------------------------------------------------------
	// Bootstrap
	// ------------------------------------------------------------------

	/// <summary>
	/// Ensure a single coach exists in the scene, on its own <see cref="ScreenPanel"/> alongside the
	/// <see cref="TipsDisplay"/> view. Idempotent, safe to call from a client bootstrap every load.
	/// The consumer still has to drive it every frame with <see cref="Tick(TipContext)"/>.
	/// </summary>
	public static TipsCoach Ensure( Scene scene )
	{
		if ( scene is null )
			return null;

		var existing = scene.GetAllComponents<TipsCoach>().FirstOrDefault();
		if ( existing is not null )
			return existing;

		var go = scene.CreateObject();
		go.Name = "UI.Tips";

		var screen = go.Components.Create<ScreenPanel>();
		screen.ZIndex = 50; // above the HUD/plates, below modals and full-screen dev/menu overlays

		var coach = go.Components.Create<TipsCoach>();
		go.Components.Create<TipsDisplay>();
		return coach;
	}

	protected override void OnEnabled()
	{
		Load();
	}

	// ------------------------------------------------------------------
	// Signals (the consumer calls these when it observes a behaviour)
	// ------------------------------------------------------------------

	/// <summary>Latch an observed behaviour. Idempotent; once latched it stays for the session.</summary>
	public void Signal( TipSignal signal ) => _signals.Add( signal );

	/// <summary>
	/// Latch an observed behaviour named by your own string key, for games whose vocabulary does not fit
	/// the built-in <see cref="TipSignal"/> set (a builder placing a block, a racer landing a drift).
	/// Idempotent; once latched it stays for the session and reads true from
	/// <see cref="TipContext.Ever(string)"/> in your predicates. Choose stable keys, they are matched
	/// verbatim.
	/// </summary>
	public void Signal( string signal )
	{
		if ( !string.IsNullOrEmpty( signal ) )
			_customSignals.Add( signal );
	}

	/// <summary>The player drew a hostile's attention. Wire your game's "NPC aggro" event to this.</summary>
	public void SignalNpcAggro() => Signal( TipSignal.Aggroed );

	/// <summary>The player accepted / started a quest. Wire your game's "quest started" event to this.</summary>
	public void SignalQuestStarted() => Signal( TipSignal.AcceptedQuest );

	/// <summary>The player cast a spell. Wire your game's "spell cast" event to this.</summary>
	public void SignalSpellCast() => Signal( TipSignal.CastSpell );

	/// <summary>The player talked to / interacted with a character. Wire your game's "interaction" event to this.</summary>
	public void SignalInteraction() => Signal( TipSignal.Talked );

	// ------------------------------------------------------------------
	// Per-frame drive (the consumer calls this every frame)
	// ------------------------------------------------------------------

	/// <summary>
	/// Drive the coach for one frame with a freshly-built neutral context. Applies latches, decides
	/// which single tip is relevant, completes the active one when its behaviour is observed, and
	/// publishes <see cref="ActiveTip"/> / <see cref="Visible"/> / <see cref="ActiveSegments"/> for the
	/// display.
	///
	/// OPTIONAL since v0.3: a game whose tips complete only on input, signals, timers or world triggers
	/// need not call this at all; the coach self-drives from <see cref="OnUpdate"/>. Call it when your
	/// tips read live game context (vitals, modal/overlay flags, custom flags and numbers). Calling it
	/// every frame is the v0.2 pattern and keeps the coach fully consumer-driven, which is byte-identical
	/// to prior behaviour.
	/// </summary>
	public void Tick( TipContext context )
	{
		_tickedSinceSelfUpdate = true;
		_ctx = context;
		Evaluate();
	}

	/// <summary>
	/// The self-driving path (v0.3). On any frame the game did not call <see cref="Tick(TipContext)"/>,
	/// the coach evaluates on its own, reading <see cref="Input"/> directly for declarative input triggers
	/// and synthesizing a context from the <see cref="TipsWorld"/> seams. The guard makes a Tick-driven
	/// game behave exactly as before: Tick sets a flag every frame it runs, and this pass skips whenever
	/// that flag is set. It is cleared here so the next tickless frame self-drives.
	/// </summary>
	protected override void OnUpdate()
	{
		if ( _tickedSinceSelfUpdate )
		{
			_tickedSinceSelfUpdate = false;
			return;
		}

		_ctx = SynthContext();
		Evaluate();
	}

	/// <summary>
	/// The single per-frame evaluation body shared by <see cref="Tick(TipContext)"/> and the self-driving
	/// <see cref="OnUpdate"/>. Reads <see cref="_ctx"/> (already assigned by the caller).
	/// </summary>
	private void Evaluate()
	{
		ApplyLatches();

		if ( !TipsEnabled || _allDismissed || !_ctx.HasPlayer )
		{
			ClearVisible();
			return;
		}

		var suppressed = _ctx.ModalOpen || _ctx.DialogueActive || _ctx.OverlayOpen;

		// Drive the currently-active tip, or pick the next eligible one.
		if ( _activeId is not null )
		{
			var def = FindDef( _activeId );
			if ( def is null )
			{
				_activeId = null;
			}
			else
			{
				// Latch a declarative completion trigger the moment it fires, so an input-edge press
				// inside the readable window is not lost (edges are true for one frame only). The v0.2
				// CompleteWhen predicate below stays LIVE at the gate, so existing tips are unchanged.
				if ( def.Completion is not null && EvalTrigger( def.Completion ) )
					_completionSeen = true;

				if ( _activeSince > MinShowSeconds && ( _completed.Contains( def.Id ) || def.CompleteWhen( _ctx )
					|| _completionSeen || ( def.MaxShowSeconds > 0f && _activeSince > def.MaxShowSeconds ) ) )
				{
					RetireActive( def );
				}
			}
		}

		if ( _activeId is null )
			SelectNext();

		PublishVisible( suppressed );
	}

	/// <summary>
	/// Synthesize a neutral context for the self-driving path from the <see cref="TipsWorld"/> seams.
	/// Only <see cref="TipContext.HasPlayer"/> is derived (from <see cref="TipsWorld.HasLocalPlayer"/>,
	/// default true); the live game fields stay at their defaults because a game that self-drives has not
	/// pushed them. Fail-inert: a throwing seam is treated as "player present" rather than crashing.
	/// </summary>
	private static TipContext SynthContext()
	{
		var has = true;
		var seam = TipsWorld.HasLocalPlayer;
		if ( seam is not null )
		{
			try { has = seam(); }
			catch { has = true; }
		}

		return new TipContext { HasPlayer = has };
	}

	/// <summary>
	/// Evaluate a declarative <see cref="TipTrigger"/> against live <see cref="Input"/>, the pushed (or
	/// synthesized) <see cref="TipContext"/> and the active tip's timer, via the pure
	/// <see cref="TipTriggerEval"/> core. Input / Key kinds read the edge
	/// (<see cref="Input.Pressed(string)"/> / <see cref="Input.Keyboard"/>) so they must be evaluated in a
	/// per-frame pass, which both callers (Tick, OnUpdate) are.
	/// </summary>
	private bool EvalTrigger( TipTrigger t )
		=> TipTriggerEval.Evaluate( t, CurrentEnv() );

	/// <summary>Bind the trigger evaluation environment to this coach's live reads.</summary>
	private TipTriggerEnv CurrentEnv() => new()
	{
		ActionPressed = static a => Input.Pressed( a ),
		KeyPressed = static k => Input.Keyboard.Pressed( k ),
		SignalLatched = s => _customSignals.Contains( s ),
		Flag = k => _ctx.Flag( k ),
		Number = k => _ctx.Number( k ),
		Elapsed = _activeSince,
		AnalogMagnitude = static src => src == TipTriggerAnalogSource.AnalogLook
			? Input.AnalogLook.AsVector3().Length
			: Input.AnalogMove.Length,
	};

	/// <summary>
	/// Auto-latch the behaviours that can be derived from the neutral context (belt-and-suspenders, as
	/// the original coach derived these from replicated state so a non-host client still advanced), then
	/// merge every latched signal into the context the tip predicates read.
	/// </summary>
	private void ApplyLatches()
	{
		if ( _ctx.DialogueActive )
			_signals.Add( TipSignal.Talked );
		if ( _ctx.ActiveQuestCount > 0 )
			_signals.Add( TipSignal.AcceptedQuest );
		if ( _ctx.InCombat && _ctx.EnemiesNearby )
			_signals.Add( TipSignal.Aggroed );
		if ( _ctx.OverlayOpen )
			_signals.Add( TipSignal.OpenedOverlay );

		// A panel closing (Q / toggle), from open last frame to closed now.
		var anyPanelOpen = _ctx.ModalOpen || _ctx.OverlayOpen;
		if ( _anyPanelOpenLast && !anyPanelOpen )
			_signals.Add( TipSignal.ClosedPanel );
		_anyPanelOpenLast = anyPanelOpen;

		// Copy the latched flags into the context the tip predicates read.
		_ctx.EverMoved = _signals.Contains( TipSignal.Moved );
		_ctx.EverSprinted = _signals.Contains( TipSignal.Sprinted );
		_ctx.EverJumped = _signals.Contains( TipSignal.Jumped );
		_ctx.EverLightAttacked = _signals.Contains( TipSignal.LightAttacked );
		_ctx.EverHeavyAttacked = _signals.Contains( TipSignal.HeavyAttacked );
		_ctx.EverBlocked = _signals.Contains( TipSignal.Blocked );
		_ctx.EverDodged = _signals.Contains( TipSignal.Dodged );
		_ctx.EverAggroed = _signals.Contains( TipSignal.Aggroed );
		_ctx.EverTalked = _signals.Contains( TipSignal.Talked );
		_ctx.EverAcceptedQuest = _signals.Contains( TipSignal.AcceptedQuest );
		_ctx.EverOpenedSheet = _signals.Contains( TipSignal.OpenedSheet );
		_ctx.EverSpentPoint = _signals.Contains( TipSignal.SpentPoint );
		_ctx.EverCastSpell = _signals.Contains( TipSignal.CastSpell );
		_ctx.EverUsedPotion = _signals.Contains( TipSignal.UsedPotion );
		_ctx.EverOpenedOverlay = _signals.Contains( TipSignal.OpenedOverlay );
		_ctx.EverClosedPanel = _signals.Contains( TipSignal.ClosedPanel );

		// Expose the latched custom signals so TipContext.Ever(string) predicates can read them.
		_ctx.LatchedCustom = _customSignals;
	}

	/// <summary>Retire the active tip: mark it done, persist, and clear it, the next evaluation advances
	/// straight to the next eligible tip (no completion flourish; the card's rise-in animation on the new
	/// tip is transition enough).</summary>
	private void RetireActive( TipDefinition def )
	{
		if ( _completed.Add( def.Id ) )
			Save();

		_activeId = null;
		_completionSeen = false;
	}

	/// <summary>
	/// Mark a tip complete from anywhere and persist it (v0.3). This is the seam a
	/// <see cref="TipTriggerObject"/> world trigger, a cutscene, or any custom game logic uses to retire a
	/// tip by id without building a context. Idempotent: completing an already-complete tip does nothing.
	/// If the tip is the one on screen, it retires on the next evaluation, subject to the same
	/// MinShowSeconds readable-window guard as every other completion path.
	/// </summary>
	public static void Complete( string tipId )
	{
		if ( string.IsNullOrEmpty( tipId ) )
			return;

		Load();
		if ( _completed.Add( tipId ) )
			Save();
	}

	/// <summary>Has this tip already been retired? Read by the Tips Studio's list so a tip that will never
	/// show again is visibly done rather than mysteriously absent.</summary>
	public static bool IsCompleted( string tipId )
	{
		if ( string.IsNullOrEmpty( tipId ) )
			return false;

		Load();
		return _completed.Contains( tipId );
	}

	/// <summary>
	/// Mark a tip NOT complete again and persist it: the undo of <see cref="Complete(string)"/>. The seam the
	/// Tips Studio test-fire uses, because a tip you already retired this session would otherwise vanish the
	/// instant you tried to look at it again. <c>fg_tips_reset</c> is the whole-walkthrough version.
	/// </summary>
	public static void Uncomplete( string tipId )
	{
		if ( string.IsNullOrEmpty( tipId ) )
			return;

		Load();
		if ( _completed.Remove( tipId ) )
			Save();
	}

	/// <summary>Take the current tip off the card WITHOUT marking it complete, so the coach picks the next
	/// eligible one on its next evaluation. <see cref="DismissCurrent"/> is the player-facing affordance and
	/// retires the tip for good; this is the dev-tool one, for pulling a preview back off the screen.</summary>
	public void DropActive()
	{
		_activeId = null;
		_completionSeen = false;
		ClearVisible();
	}

	/// <summary>Pick the highest-priority tip that isn't done, whose prerequisites are done and whose trigger passes.</summary>
	private void SelectNext()
	{
		TipDefinition best = null;
		foreach ( var def in TipsCatalog.Active )
		{
			if ( _completed.Contains( def.Id ) )
				continue;
			if ( !PrereqsDone( def ) )
				continue;
			if ( !def.Trigger( _ctx ) )
				continue;
			// Declarative relevance NARROWS: a tip with a Relevance trigger must also pass it (v0.3).
			// Null Relevance leaves the predicate Trigger as the only gate, so v0.2 tips are unchanged.
			if ( def.Relevance is not null && !EvalTrigger( def.Relevance ) )
				continue;
			if ( best is null || def.Priority > best.Priority )
				best = def;
		}

		if ( best is null )
		{
			_activeId = null;
			return;
		}

		_activeId = best.Id;
		_activeSince = 0f;
		_completionSeen = false;
		ActiveTip = best;
		ActiveSegments = TipSegment.Parse( best.Text );
		ActivePadSegments = TipSegment.Parse( TipDeviceText.PadTextOr( best.Text, best.TextPad ) );
	}

	private bool PrereqsDone( TipDefinition def )
	{
		foreach ( var id in def.PrerequisiteTipIds )
			if ( !_completed.Contains( id ) )
				return false;
		return true;
	}

	/// <summary>
	/// A one-word live verdict for why a tip is or is not showing right now, read against this coach's
	/// last-evaluated context (whatever the game pushed with <see cref="Tick(TipContext)"/>, or the
	/// self-driven synth context). Used by the <c>fg_tips_list</c> diagnostics so a withheld tip is
	/// distinguishable from a broken bridge: "done" (completed), "active" (on screen now),
	/// "prereqs-pending" (a prerequisite is not complete), "trigger-false" / "relevance-false" (a gate is
	/// not met this frame), or "relevant" (eligible, waiting its turn by priority).
	/// </summary>
	public string RelevanceVerdict( TipDefinition def )
	{
		if ( def is null )
			return "unknown";
		if ( _completed.Contains( def.Id ) )
			return "done";
		if ( def.Id == _activeId )
			return "active";
		if ( !PrereqsDone( def ) )
			return "prereqs-pending";
		if ( !def.Trigger( _ctx ) )
			return "trigger-false";
		if ( def.Relevance is not null && !EvalTrigger( def.Relevance ) )
			return "relevance-false";
		return "relevant";
	}

	private static TipDefinition FindDef( string id )
	{
		foreach ( var def in TipsCatalog.Active )
			if ( def.Id == id )
				return def;
		return null;
	}

	private void PublishVisible( bool suppressed )
	{
		if ( _activeId is not null )
		{
			var def = FindDef( _activeId );
			ActiveTip = def;
			ActiveSegments = def is not null ? TipSegment.Parse( def.Text ) : Array.Empty<TipSegment>();
			ActivePadSegments = def is not null
				? TipSegment.Parse( TipDeviceText.PadTextOr( def.Text, def.TextPad ) )
				: Array.Empty<TipSegment>();
			Visible = def is not null && !suppressed;
			return;
		}

		ClearVisible();
	}

	private void ClearVisible()
	{
		Visible = false;
		ActiveTip = null;
		ActiveSegments = Array.Empty<TipSegment>();
		ActivePadSegments = Array.Empty<TipSegment>();
	}

	/// <summary>Force a specific tip active for preview (the <c>fg_tips_show</c> seam). Returns false when
	/// the id is not in the merged catalog. Bypasses relevance and prerequisites on purpose so a developer
	/// can preview any tip; the next evaluation resumes normal completion/selection from there.</summary>
	public bool ForceShow( string id )
	{
		var def = FindDef( id );
		if ( def is null )
			return false;

		_activeId = def.Id;
		_activeSince = 0f;
		_completionSeen = false;
		ActiveTip = def;
		ActiveSegments = TipSegment.Parse( def.Text );
		ActivePadSegments = TipSegment.Parse( TipDeviceText.PadTextOr( def.Text, def.TextPad ) );
		Visible = true;
		return true;
	}

	/// <summary>The × affordance: skip the current tip for good (marks it complete).</summary>
	public void DismissCurrent()
	{
		if ( _activeId is null )
			return;

		_completed.Add( _activeId );
		Save();
		_activeId = null;
		_completionSeen = false;
		ClearVisible();
	}

	private void ResetRuntime()
	{
		_activeId = null;
		_completionSeen = false;
		_signals.Clear();
		_customSignals.Clear();
		_anyPanelOpenLast = false;
		ClearVisible();
	}
}
