using System;
using System.Linq;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// Wires the demo scene in one component so the whole kit runs from a single press of play: it points the
/// <see cref="TipsWorld"/> seams at the demo pawn, creates the coach and its display with
/// <see cref="TipsCoach.Ensure"/>, and pushes a context every frame carrying the demo flag the demo tips
/// gate on. The tips themselves are authored assets (<c>Assets/demo/*.tip</c> plus
/// <c>Assets/starter.tip</c>), not code, so the scene also shows the no-code authoring path.
///
/// THE DEMO FLAG. Every demo tip's Relevance is <c>Flag("fg_tips_demo")</c>, and only this component sets
/// that flag. A game that vendors the kit and never runs this scene therefore never sees a demo tip, even
/// if it forgets to delete the assets: the tips merge into the catalog but stay irrelevant forever. Delete
/// them anyway.
///
/// THE ENDING. The walkthrough hands the player to the authoring tool: the last tip coaches <c>T</c>, and
/// opening the Tips Studio is what retires it. Two pieces do that, both of them demo wiring rather than
/// kit runtime. This component builds the Studio's host object at boot (the same one line a game writes
/// from the README), and it raises the <see cref="StudioOpenedSignal"/> string signal while the Studio is
/// open, which is the completion <c>demo_wrap.tip</c> waits on.
///
/// Not part of the kit's runtime surface: delete <c>Code/Demo</c> and <c>Assets/demo</c> when you drop the
/// kit into your own project, and write your own bootstrap from the README instead.
/// </summary>
[Title( "Tips Demo Bootstrap" )]
[Category( "Field Guide Tips" )]
[Icon( "auto_awesome" )]
public sealed class TipsDemoBootstrap : Component
{
	/// <summary>The context flag every demo tip's Relevance reads, so demo content is inert anywhere this
	/// component is not running.</summary>
	public const string DemoFlag = "fg_tips_demo";

	/// <summary>Progress file the demo writes, kept apart from the kit default so replaying the demo never
	/// touches the progress your own game saves.</summary>
	public const string DemoSaveFile = "fieldguide_tips_demo.json";

	/// <summary>The string signal this component latches while the Tips Studio is open, and the completion
	/// <c>demo_wrap.tip</c> waits on. A demo-side name: the kit knows nothing about it, which is the point.
	/// Any game can retire a tip on its own UI the same way, with one <see cref="TipsCoach.Signal(string)"/>
	/// call and a Signal trigger on the tip.</summary>
	public const string StudioOpenedSignal = "fg_tips_studio_opened";

	/// <summary>Raw key that resets progress and replays the sequence from the top.</summary>
	[Property] public string ReplayKey { get; set; } = "r";

	private TipsCoach _coach;
	private TipsDemoPawn _pawn;
	private string _previousSaveFile;
	private Func<bool> _previousHasPlayer;
	private Func<Vector3> _previousPlayerPosition;
	private Func<Ray> _previousAimRay;

	protected override void OnStart()
	{
		_pawn = Scene.GetAllComponents<TipsDemoPawn>().FirstOrDefault();

		// Set the save file BEFORE Ensure: the first load reads whatever name is set then.
		_previousSaveFile = TipsCoach.SaveFileName;
		TipsCoach.SaveFileName = DemoSaveFile;

		// Always start the demo from the top, the way a first-time player would see it.
		TipsCoach.ResetProgress();

		// World seams. A library cannot reach into your player, so the demo hands the kit its pawn. The
		// marker zone's PlayerEntered trigger reads LocalPlayerPosition; nothing here uses LookedAt, so
		// AimRay stays null and those triggers stay inert. They are statics, so the old values are kept
		// and handed back in OnDestroy: a demo pawn that no longer exists must not answer for a game
		// scene opened later in the same session.
		_previousHasPlayer = TipsWorld.HasLocalPlayer;
		_previousPlayerPosition = TipsWorld.LocalPlayerPosition;
		_previousAimRay = TipsWorld.AimRay;

		TipsWorld.HasLocalPlayer = () => _pawn.IsValid();
		TipsWorld.LocalPlayerPosition = () => _pawn.IsValid() ? _pawn.WorldPosition : Vector3.Zero;
		TipsWorld.AimRay = null;

		_coach = TipsCoach.Ensure( Scene );
		EnsureStudioHost();

		Log.Info( "[tips] demo ready: move with W A S D or the left stick, jump with Space or A, " +
			"switch the marker on inside its ring, then press T for the Tips Studio. " +
			$"Press {ReplayKey.ToUpperInvariant()} to replay." );
	}

	protected override void OnUpdate()
	{
		if ( _coach is null )
			return;

		// Ignored while the Studio is open. The key is read raw, so without this an "r" typed into one of
		// the Studio's text boxes would restart the walkthrough under the panel, and the walkthrough now
		// ENDS in that panel. Closed, the last beat is there to walk again.
		if ( !TipsStudio.Open && Input.Keyboard.Pressed( ReplayKey ) )
			Replay();

		// The last beat. Latched while the Studio is OPEN rather than on the frame it opens: a latch is
		// idempotent, so pushing it every frame costs nothing, and it leaves no edge state to go stale
		// when the walkthrough is reset out from under it. Raised before the tick below, because the tick
		// is what reads it.
		if ( TipsStudio.Open )
			_coach.Signal( StudioOpenedSignal );

		// The context path: one small neutral struct per frame. The demo only needs two things in it, a
		// player and the demo flag; a real game fills the fields its own tips read.
		var ctx = new TipContext { HasPlayer = _pawn.IsValid() };
		ctx.SetFlag( DemoFlag, true );
		_coach.Tick( ctx );
	}

	protected override void OnDestroy()
	{
		// Hand the statics back so a game scene opened later in the same editor session sees the kit
		// exactly as it was before the demo ran.
		if ( !string.IsNullOrEmpty( _previousSaveFile ) )
			TipsCoach.SaveFileName = _previousSaveFile;

		if ( _previousHasPlayer is not null )
			TipsWorld.HasLocalPlayer = _previousHasPlayer;
		else
			TipsWorld.HasLocalPlayer = static () => true;

		TipsWorld.LocalPlayerPosition = _previousPlayerPosition;
		TipsWorld.AimRay = _previousAimRay;
	}

	/// <summary>
	/// Build the Tips Studio's host: one <see cref="ScreenPanel"/> carrying the panel, created in code so
	/// the demo scene file holds no razor reference (the same idiom the other kits' demos use). It sits
	/// above the coach's own panel, since the Studio is a modal over the card it is editing. The Studio
	/// ships CLOSED and opens on its own OpenKey, which is T; nothing here opens it.
	///
	/// Idempotent: a scene that already carries a Studio panel of its own keeps it.
	/// </summary>
	private void EnsureStudioHost()
	{
		if ( Scene.GetAllComponents<TipsStudioPanel>().Any() )
			return;

		var go = Scene.CreateObject();
		go.Name = "UI.TipsStudio";

		var screen = go.Components.Create<ScreenPanel>();
		screen.ZIndex = 60; // above the coach's card at 50

		go.Components.Create<TipsStudioPanel>();
	}

	private void Replay()
	{
		TipsCoach.ResetProgress();

		foreach ( var marker in Scene.GetAllComponents<TipsDemoMarker>() )
			marker.ResetMarker();

		_pawn?.ResetPawn();
		Log.Info( "[tips] demo replayed from the first tip." );
	}
}
