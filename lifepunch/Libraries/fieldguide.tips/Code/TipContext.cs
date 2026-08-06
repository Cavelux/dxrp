using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// A cheap, per-frame snapshot of the local player and world that the consumer hands to
/// <see cref="TipsCoach.Tick(TipContext)"/> every frame. Each tip's
/// <see cref="TipDefinition.Trigger"/> / <see cref="TipDefinition.CompleteWhen"/> predicate reads
/// these fields.
///
/// This struct is deliberately NEUTRAL: it holds plain values (bools, ints, fractions) and no game
/// types. The consumer builds it each frame from its own player components (stat sheet, spell book,
/// quest log, inventory, modal router, ...) and pushes it in. The kit never reaches into game state.
///
/// UNIVERSAL vs GENRE-SHAPED fields. <see cref="HasPlayer"/>, <see cref="ModalOpen"/>,
/// <see cref="OverlayOpen"/> and <see cref="DialogueActive"/> apply to any game. The combat / spell /
/// quest / potion fields below are convenience shortcuts carried over from the kit's first consumer
/// (an action RPG): a game that has no such concept simply leaves them at their defaults. When your
/// game's vocabulary does not fit the named fields (a builder that mines, places or crafts; a racer
/// that drifts or boosts), reach for the GENERIC path instead: push live conditions with
/// <see cref="SetFlag"/> / <see cref="SetNumber"/> and read them in your predicates with
/// <see cref="Flag"/> / <see cref="Number"/>, and latch one-off behaviours with the string overload of
/// <see cref="TipsCoach.Signal(string)"/>, read back with <see cref="Ever(string)"/>. That way a game
/// drives the coach entirely in its own words without the kit hard-coding any genre.
///
/// The <c>Ever*</c> fields are LATCHED by <see cref="TipsCoach"/>, not set by the consumer: once a
/// behaviour is signalled it stays true for the session, so a tip completes the moment its action is
/// observed. Leave them at their defaults when you build a context; the coach fills them before it
/// evaluates any predicate.
/// </summary>
public struct TipContext
{
	/// <summary>True once a local player exists to coach. When false the coach shows nothing.</summary>
	public bool HasPlayer;

	// --- Live reads (the consumer recomputes these every frame) --------------------------------

	/// <summary>Aiming at an interactable right now (villager, pickup, ...).</summary>
	public bool HasInteractionTarget;

	/// <summary>A full-screen modal (character sheet, inventory) is up. The coach hides tips while true.</summary>
	public bool ModalOpen;

	/// <summary>
	/// The consumer sets this true while any full-screen dev/menu overlay is open. Replaces the game's
	/// old dev-overlay flag: the coach hides its tips while it is true (same as a modal) and latches
	/// <see cref="EverOpenedOverlay"/> from it. If your game has no such overlay, leave it false.
	/// </summary>
	public bool OverlayOpen;

	/// <summary>A conversation is on screen for this player. The coach hides tips while true.</summary>
	public bool DialogueActive;

	/// <summary>The player is in combat (recently dealt or took damage).</summary>
	public bool InCombat;

	/// <summary>At least one living hostile is close by.</summary>
	public bool EnemiesNearby;

	/// <summary>How many spells the player knows.</summary>
	public int KnownSpellCount;

	/// <summary>Quests currently active or ready to turn in.</summary>
	public int ActiveQuestCount;

	/// <summary>At least one action-bar slot holds a spell.</summary>
	public bool HasActionBarSpell;

	/// <summary>The player has an unspent attribute point to spend.</summary>
	public bool HasUnspentPoint;

	/// <summary>The player is carrying a consumable (potion) in their bag.</summary>
	public bool HoldingPotion;

	// --- Neutral vitals (replace the game's direct stat-sheet reference) -----------------------

	/// <summary>Current health as a 0..1 fraction of max. The consumer computes it off its own stat sheet.</summary>
	public float HealthFraction;

	/// <summary>Current mana as a 0..1 fraction of max.</summary>
	public float ManaFraction;

	/// <summary>Current stamina as a 0..1 fraction of max.</summary>
	public float StaminaFraction;

	// --- Latched behaviours ("the player has done this at least once") ------------------------
	// Set by TipsCoach from signals + neutral context, NOT by the consumer.

	public bool EverMoved;
	public bool EverSprinted;
	public bool EverJumped;
	public bool EverLightAttacked;
	public bool EverHeavyAttacked;
	public bool EverBlocked;
	public bool EverDodged;
	public bool EverAggroed;
	public bool EverTalked;
	public bool EverAcceptedQuest;
	public bool EverOpenedSheet;
	public bool EverSpentPoint;
	public bool EverCastSpell;
	public bool EverUsedPotion;
	public bool EverOpenedOverlay;
	public bool EverClosedPanel;

	// --- Generic custom state (the genre-neutral escape hatch) ---------------------------------
	// A game whose vocabulary does not fit the named fields drives the coach through these instead:
	// live bools/numbers it sets each frame, and latched string signals the coach fills.

	// Live values the consumer sets this frame. Lazily allocated so a context that never uses them
	// costs nothing. Copied by reference when the struct is passed, which is fine: it is rebuilt each
	// frame and read within the same Tick.
	private Dictionary<string, bool> _flags;
	private Dictionary<string, float> _numbers;

	// The coach's latched custom-signal set, wired in each Tick so Ever(string) can read it. Never set
	// this yourself; the coach owns it.
	internal HashSet<string> LatchedCustom;

	/// <summary>Set a live custom flag for this frame, read in a predicate with <see cref="Flag"/>. Use
	/// for genre-specific conditions the named fields do not cover (e.g. <c>"NearWorkbench"</c>,
	/// <c>"HoldingBlock"</c>).</summary>
	public void SetFlag( string key, bool value )
	{
		if ( string.IsNullOrEmpty( key ) )
			return;
		( _flags ??= new Dictionary<string, bool>() )[key] = value;
	}

	/// <summary>Read a live custom flag set this frame with <see cref="SetFlag"/>. Unknown keys read false.</summary>
	public readonly bool Flag( string key )
		=> _flags is not null && _flags.TryGetValue( key, out var v ) && v;

	/// <summary>Set a live custom number for this frame, read in a predicate with <see cref="Number"/>.
	/// Use for genre-specific quantities (e.g. <c>"BlocksPlaced"</c>, <c>"SpeedKph"</c>).</summary>
	public void SetNumber( string key, float value )
	{
		if ( string.IsNullOrEmpty( key ) )
			return;
		( _numbers ??= new Dictionary<string, float>() )[key] = value;
	}

	/// <summary>Read a live custom number set this frame with <see cref="SetNumber"/>. Unknown keys read 0.</summary>
	public readonly float Number( string key )
		=> _numbers is not null && _numbers.TryGetValue( key, out var v ) ? v : 0f;

	/// <summary>True once the given custom behaviour has been latched via
	/// <see cref="TipsCoach.Signal(string)"/> this session. The kit-genre equivalent of the
	/// <c>Ever*</c> fields, for behaviours you name yourself (e.g. <c>Ever("PlacedBlock")</c>).</summary>
	public readonly bool Ever( string signal )
		=> LatchedCustom is not null && LatchedCustom.Contains( signal );
}

/// <summary>
/// The set of observable player behaviours the coach latches. The consumer calls
/// <see cref="TipsCoach.Signal(TipSignal)"/> (or one of the named convenience wrappers) when it
/// observes one; the flag then stays latched for the session and the matching <c>Ever*</c> field on
/// <see cref="TipContext"/> reads true. A few of these are ALSO auto-latched by the coach from the
/// neutral context each tick (see <see cref="TipsCoach"/>), so a game that only pushes context still
/// advances.
/// </summary>
public enum TipSignal
{
	Moved,
	Sprinted,
	Jumped,
	LightAttacked,
	HeavyAttacked,
	Blocked,
	Dodged,
	Aggroed,
	Talked,
	AcceptedQuest,
	OpenedSheet,
	SpentPoint,
	CastSpell,
	UsedPotion,
	OpenedOverlay,
	ClosedPanel,
}
