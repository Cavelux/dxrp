using System.Linq;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// The demo scene's one interactable: a marker the citizen walks up to and switches on. It owns its own
/// state (off / on) and tells the kit about the use through <see cref="TipTriggerObject.NotifyInteracted"/>,
/// which is exactly the one line a real game writes from wherever it already handles "the player used
/// this object". The proximity half of the beat is a second <see cref="TipTriggerObject"/> on the sibling
/// zone object, in <see cref="TipTriggerObject.Mode.PlayerEntered"/>, so no code is involved there at all.
///
/// Note the ordering: the marker gates on ITS OWN rule (the pawn is inside the ring and the marker is
/// still off), never on which tip is showing. Game logic drives tips, not the other way round.
///
/// Not part of the kit's runtime surface: delete <c>Code/Demo</c> and <c>Assets/demo</c> when you drop
/// the kit into your own project.
/// </summary>
[Title( "Tips Demo Marker" )]
[Category( "Field Guide Tips" )]
[Icon( "emoji_objects" )]
public sealed class TipsDemoMarker : Component
{
	/// <summary>How close the pawn has to be, in world units, before the marker accepts the switch.</summary>
	[Property] public float Radius { get; set; } = 110f;

	/// <summary>The Input.config action that switches the marker on. The demo reuses Jump so the prompt
	/// reads Space on a keyboard and A on a pad with no extra binding.</summary>
	[Property] public string SwitchAction { get; set; } = "Jump";

	[Property] public Color OffTint { get; set; } = new Color( 1f, 0.62f, 0.18f );
	[Property] public Color OnTint { get; set; } = new Color( 0.35f, 0.95f, 0.5f );

	private TipsDemoPawn _pawn;
	private ModelRenderer _renderer;
	private bool _on;

	protected override void OnStart()
	{
		_renderer = Components.Get<ModelRenderer>();
		_pawn = Scene.GetAllComponents<TipsDemoPawn>().FirstOrDefault();
		Paint();
	}

	protected override void OnUpdate()
	{
		if ( _on || !PawnInside() )
			return;
		if ( !Input.Pressed( SwitchAction ) )
			return;

		_on = true;
		Paint();

		// The world-trigger seam: the game says "this object was used" and the TipTriggerObject on it
		// retires the tip it is bound to. A game that vendors fieldguide.interaction wires the same call
		// to its InteractionPerformed event instead.
		TipTriggerObject.NotifyInteracted( GameObject );
	}

	/// <summary>True while the demo pawn stands inside the marker ring (flat distance, height ignored so a
	/// hop does not drop the player out of the ring).</summary>
	public bool PawnInside()
		=> _pawn.IsValid() && WorldPosition.WithZ( 0f ).Distance( _pawn.WorldPosition.WithZ( 0f ) ) <= Radius;

	/// <summary>Switch the marker back off (the demo's replay key).</summary>
	public void ResetMarker()
	{
		_on = false;
		Paint();
	}

	private void Paint()
	{
		if ( _renderer.IsValid() )
			_renderer.Tint = _on ? OnTint : OffTint;
	}
}
