using System;
using Sandbox;

namespace FieldGuide.Tips;

/// <summary>
/// Drop this on a GameObject (an NPC, a door, a pickup) to retire a tip when the player does something to
/// that object. Pick the tip id and the mode in the inspector; no code for the common cases. So "talk to
/// this NPC" is: add this component to the NPC, set <see cref="TipId"/>, set
/// <see cref="CompleteOn"/> = <see cref="Mode.Interacted"/>, and call <see cref="Interacted"/> (or the
/// static <see cref="NotifyInteracted"/>) from wherever your game already knows an interaction happened.
///
/// <see cref="Mode.PlayerEntered"/> and <see cref="Mode.LookedAt"/> read the <see cref="TipsWorld"/>
/// seams and are INERT until those seams are set (they never fire and never throw), so a game that skips
/// the seams still runs. Input, Signal and Timer completion do not use this component at all; they live on
/// the tip itself (declarative triggers or the coach's input pass).
/// </summary>
[Title( "Tip Trigger" )]
[Category( "Field Guide Tips" )]
[Icon( "ads_click" )]
public sealed class TipTriggerObject : Component
{
	public enum Mode
	{
		/// <summary>Your interaction code (or the fieldguide.interaction bridge) calls <see cref="Interacted"/>.</summary>
		Interacted,

		/// <summary>The local player is within <see cref="Radius"/> of this object (needs <see cref="TipsWorld.LocalPlayerPosition"/>).</summary>
		PlayerEntered,

		/// <summary>The aim ray hits this object within <see cref="Radius"/> (needs <see cref="TipsWorld.AimRay"/>).</summary>
		LookedAt,

		/// <summary>Raise a named signal instead of completing directly (fans out to every coach's Signal).</summary>
		Signal,
	}

	[Property] public string TipId { get; set; } = "";
	[Property] public Mode CompleteOn { get; set; } = Mode.Interacted;

	/// <summary>Trigger distance for PlayerEntered, and the aim-ray length for LookedAt, in world units.</summary>
	[Property] public float Radius { get; set; } = 128f;

	/// <summary>Signal name raised in <see cref="Mode.Signal"/>.</summary>
	[Property] public string SignalName { get; set; } = "";

	protected override void OnUpdate()
	{
		if ( CompleteOn == Mode.PlayerEntered && WithinPlayerRadius() )
			Fire();
		else if ( CompleteOn == Mode.LookedAt && AimRayHitsSelf() )
			Fire();
	}

	/// <summary>Call from your interaction code when this object is interacted with. Inert unless the mode
	/// is <see cref="Mode.Interacted"/>.</summary>
	public void Interacted()
	{
		if ( CompleteOn == Mode.Interacted )
			Fire();
	}

	/// <summary>
	/// Convenience for an interaction bridge: fire the <see cref="Interacted"/> trigger on a target
	/// GameObject if it carries a <see cref="TipTriggerObject"/>. Null-safe on the target and on a missing
	/// component, so a bridge can call it for every interacted object without guarding.
	/// </summary>
	public static void NotifyInteracted( GameObject target )
		=> target?.Components.Get<TipTriggerObject>( FindMode.EnabledInSelfAndDescendants )?.Interacted();

	private void Fire()
	{
		if ( CompleteOn == Mode.Signal )
		{
			if ( string.IsNullOrEmpty( SignalName ) )
				return;
			foreach ( var coach in Scene.GetAllComponents<TipsCoach>() )
				coach.Signal( SignalName );
			return;
		}

		TipsCoach.Complete( TipId );
	}

	private bool WithinPlayerRadius()
	{
		var seam = TipsWorld.LocalPlayerPosition; // fail-inert: null seam disables PlayerEntered
		if ( seam is null )
			return false;

		try
		{
			return WorldPosition.Distance( seam() ) <= Radius;
		}
		catch
		{
			return false;
		}
	}

	private bool AimRayHitsSelf()
	{
		var seam = TipsWorld.AimRay; // fail-inert: null seam disables LookedAt
		if ( seam is null )
			return false;

		try
		{
			var ray = seam();
			var tr = Scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * Radius ).Run();
			return tr.Hit && tr.GameObject.IsValid()
				&& ( tr.GameObject == GameObject || GameObject.IsDescendant( tr.GameObject ) );
		}
		catch
		{
			return false;
		}
	}
}
